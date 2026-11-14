using System.Buffers.Binary;
using System.Text;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Services.Crypto;
using SmartArchiver.Optimization;

namespace SmartArchiver.Core.Services;

/// <summary>Version 1 archive orchestration, independent of the CLI and filesystem.</summary>
public sealed class SarcArchive(ICryptoService crypto, ArchiverController controller)
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public sealed record Input(string Name, byte[] Data);
    public sealed record PackResult(IReadOnlyList<byte[]> Volumes, long FirstVolumeValue, long FirstGreedyValue);
    private sealed record FileEntry(string Name, uint Size, uint Crc, byte Type, byte Strategy);
    private sealed record Block(int File, int Sequence, int RawLength, byte[] Data, bool Raw);
    private sealed record Volume(FileEntry[] Files, Block[] Blocks);

    public PackResult Pack(IReadOnlyList<Input> inputs, string password, TeamOptions options,
        ArchiverParameters? parameters = null, CompressionMode? mode = null, bool greedyPacking = false)
    {
        if (inputs.Count is 0 or > ushort.MaxValue) throw Bad("Invalid file count.");
        var files = new List<FileEntry>();
        var pending = new List<Block>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pipeline = new SarcPipeline(parameters is null ? new Lz77Algorithm() :
            new Lz77Algorithm { WindowSize = parameters.WindowSize, MinMatch = parameters.MinMatchLength });
        int threshold = parameters?.NoCompressionThreshold ?? 0;
        if (threshold is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(parameters));

        for (int file = 0; file < inputs.Count; file++)
        {
            var input = inputs[file];
            ValidateName(input.Name);
            if (!names.Add(input.Name)) throw Bad("Duplicate file name.");
            byte type = input.Data.Length == 0 ? (byte)6 : checked((byte)controller.Classify(input.Data));
            var compression = mode ?? (type is 0 or 1 ? CompressionMode.Lazy : CompressionMode.Greedy);
            bool anyCompressed = false;
            int offset = 0, sequence = 0;
            do
            {
                byte[] raw = input.Data.AsSpan(offset, Math.Min(options.BlockSize, input.Data.Length - offset)).ToArray();
                byte[] coded = raw.Length == 0 || type == 5 ? raw : pipeline.Compress(raw, compression);
                bool plain = coded.Length >= raw.Length ||
                    (long)coded.Length * 100 > (long)raw.Length * (100 - threshold);
                pending.Add(new Block(file, sequence++, raw.Length, plain ? raw : coded, plain));
                anyCompressed |= !plain;
                offset += raw.Length;
            } while (offset < input.Data.Length);
            files.Add(new FileEntry(input.Name, (uint)input.Data.Length, Crc32.Compute(input.Data), type,
                anyCompressed ? (byte)1 : (byte)0));
        }

        var payloads = new List<byte[]>();
        long first = 0, firstGreedy = 0;
        while (pending.Count > 0)
        {
            if (payloads.Count == ushort.MaxValue) throw Volumes("Too many volumes.");
            int[] candidates = pending.Select(x => x.File).Distinct().ToArray();
            var entries = candidates.Select(i => files[i]).ToArray();
            long available = options.MaxVolumeBytes - VolumeFormat.Overhead - 6L -
                entries.Sum(f => 12L + Utf8.GetByteCount(f.Name));
            if (available <= 0) throw Volumes("File table leaves no room for blocks.");
            var weights = pending.Select(b => new VolumePacker.Item(checked(b.Data.Length + 15),
                options.Priorities[files[b.File].Type])).ToArray();
            int[] selected = VolumePacker.Select(weights, (int)available, greedyPacking);
            // The table's block_count is uint16. The supplied 64 KiB limit fits far fewer records.
            if (selected.Length is 0 or > ushort.MaxValue) throw Volumes("Blocks do not fit in a volume.");
            if (payloads.Count == 0)
            {
                first = selected.Sum(i => (long)weights[i].Value);
                firstGreedy = VolumePacker.Select(weights, (int)available, true).Sum(i => (long)weights[i].Value);
            }
            var indices = candidates.Select((file, index) => (file, index)).ToDictionary(x => x.file, x => x.index);
            var blocks = selected.Select(i => pending[i] with { File = indices[pending[i].File] }).ToArray();
            payloads.Add(WritePayload(entries, blocks));
            foreach (int i in selected.OrderDescending()) pending.RemoveAt(i);
        }

        using var keys = crypto.CreateKeys(password);
        return new PackResult(payloads.Select((payload, i) => crypto.SealVolume(keys, i, payloads.Count, payload)).ToArray(),
            first, firstGreedy);
    }

    public IReadOnlyList<Input> Unpack(IReadOnlyList<byte[]> volumes, string password)
    {
        if (volumes.Count == 0) throw Volumes("No volumes supplied.");
        var first = ReadHeader(volumes[0]);
        using var keys = crypto.DeriveKeys(password, first.Salt);
        var indexes = new HashSet<int>();
        var nonces = new HashSet<string>();
        var files = new Dictionary<string, (FileEntry Entry, SortedDictionary<int, byte[]> Blocks)>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pipeline = new SarcPipeline();
        foreach (var bytes in volumes)
        {
            var header = ReadHeader(bytes);
            if (header.VolumeCount != first.VolumeCount || !header.Salt.AsSpan().SequenceEqual(first.Salt))
                throw Volumes("Volumes do not belong to one archive.");
            byte[] payload;
            try { payload = crypto.OpenVolume(keys, bytes).Payload; }
            catch (ArchiveException e) when (e.Code == ArchiveErrorCode.AuthenticationFailed)
            { throw new ArchiveException(ArchiveErrorCode.E_HMAC, e.Message); }
            if (!indexes.Add(header.VolumeIndex) || !nonces.Add(Convert.ToHexString(header.Nonce)))
                throw Volumes("Duplicate volume index or nonce.");

            var volume = ReadPayload(payload);
            foreach (var entry in volume.Files)
            {
                if (files.TryGetValue(entry.Name, out var existing))
                {
                    if (existing.Entry != entry) throw Bad("Inconsistent file metadata between volumes.");
                }
                else
                {
                    if (!names.Add(entry.Name)) throw Bad("Conflicting file names.");
                    files.Add(entry.Name, (entry, new SortedDictionary<int, byte[]>()));
                }
            }
            foreach (var block in volume.Blocks)
            {
                var file = files[volume.Files[block.File].Name];
                if (block.RawLength > file.Entry.Size || file.Blocks.ContainsKey(block.Sequence))
                    throw Bad("Conflicting block length or sequence.");
                byte[] data = block.Raw ? block.Data : pipeline.Decompress(block.Data, block.RawLength);
                file.Blocks.Add(block.Sequence, data);
            }
        }
        if (indexes.Count != first.VolumeCount) throw Volumes("Incomplete set of volumes.");
        var result = new List<Input>();
        foreach (var (name, file) in files)
        {
            if (file.Blocks.Count == 0 || file.Blocks.Keys.Where((seq, index) => seq != index).Any() ||
                file.Blocks.Values.Sum(x => (long)x.Length) != file.Entry.Size)
                throw Bad("Missing blocks or incorrect reconstructed file size.");
            byte[] data = file.Blocks.Values.SelectMany(x => x).ToArray();
            if (Crc32.Compute(data) != file.Entry.Crc)
                throw new ArchiveException(ArchiveErrorCode.E_CRC, "File CRC32 does not match.");
            result.Add(new Input(name, data));
        }
        return result;
    }

    private SmartArchiver.Core.Models.VolumeHeader ReadHeader(byte[] volume)
    {
        if (volume.Length < VolumeFormat.Overhead)
            throw new ArchiveException(ArchiveErrorCode.E_SHORT, "Volume is too short.");
        if (!volume.AsSpan(0, 4).SequenceEqual("SARC"u8) || volume[4] != 1)
            throw new ArchiveException(ArchiveErrorCode.E_MAGIC, "Invalid magic or version.");
        if (BinaryPrimitives.ReadUInt32BigEndian(volume.AsSpan(37)) != volume.Length - VolumeFormat.Overhead)
            throw new ArchiveException(ArchiveErrorCode.E_LENGTH, "Volume length does not match header.");
        if (BinaryPrimitives.ReadUInt16BigEndian(volume.AsSpan(5)) >= BinaryPrimitives.ReadUInt16BigEndian(volume.AsSpan(7)))
            throw Volumes("Invalid volume index or count.");
        return crypto.ReadHeader(volume);
    }

    private static byte[] WritePayload(FileEntry[] files, Block[] blocks)
    {
        using var output = new MemoryStream();
        U16(output, files.Length);
        U16(output, 0);
        foreach (var entry in files)
        {
            byte[] name = Utf8.GetBytes(entry.Name);
            U16(output, name.Length);
            output.Write(name);
            U32(output, entry.Size);
            U32(output, entry.Crc);
            output.WriteByte(entry.Type);
            output.WriteByte(entry.Strategy);
        }
        U16(output, blocks.Length);
        foreach (var block in blocks)
        {
            U16(output, block.File);
            U32(output, (uint)block.Sequence);
            U32(output, (uint)block.RawLength);
            U32(output, (uint)block.Data.Length);
            output.WriteByte(block.Raw ? (byte)1 : (byte)0);
        }
        foreach (var block in blocks) output.Write(block.Data);
        return output.ToArray();
    }

    private static Volume ReadPayload(ReadOnlySpan<byte> payload)
    {
        int count = R16(ref payload);
        if (R16(ref payload) != 0) throw Bad("Unsupported flags or nonzero reserved field.");
        var files = new FileEntry[count];
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < count; i++)
        {
            int nameSize = R16(ref payload);
            string name;
            try { name = Utf8.GetString(Take(ref payload, nameSize)); }
            catch (DecoderFallbackException) { throw Bad("Invalid UTF-8 file name."); }
            ValidateName(name);
            uint size = R32(ref payload), crc = R32(ref payload);
            var fields = Take(ref payload, 2);
            if (!names.Add(name) || fields[0] > 6) throw Bad("Invalid file metadata.");
            files[i] = new FileEntry(name, size, crc, fields[0], fields[1]);
        }

        int blockCount = R16(ref payload);
        var table = Take(ref payload, blockCount * 15);
        var blocks = new Block[blockCount];
        for (int i = 0; i < blockCount; i++)
        {
            int file = R16(ref table), seq = Size(ref table), raw = Size(ref table), coded = Size(ref table);
            byte flags = Take(ref table, 1)[0];
            if (file >= count || flags > 1 || (flags == 1 && raw != coded))
                throw Bad("Invalid block metadata.");
            blocks[i] = new Block(file, seq, raw, Take(ref payload, coded).ToArray(), flags == 1);
        }
        if (!payload.IsEmpty) throw Bad("Extra bytes after block data.");
        return new Volume(files, blocks);
    }

    private static void ValidateName(string name)
    {
        if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(['/', '\\', ':', '\0']) >= 0 ||
            Utf8.GetByteCount(name) > ushort.MaxValue)
            throw Bad("Invalid file name.");
    }

    private static ReadOnlySpan<byte> Take(ref ReadOnlySpan<byte> bytes, int n)
    {
        if (n > bytes.Length) throw Bad("Truncated payload.");
        var result = bytes[..n];
        bytes = bytes[n..];
        return result;
    }

    private static int R16(ref ReadOnlySpan<byte> s) => BinaryPrimitives.ReadUInt16BigEndian(Take(ref s, 2));
    private static uint R32(ref ReadOnlySpan<byte> s) => BinaryPrimitives.ReadUInt32BigEndian(Take(ref s, 4));
    private static int Size(ref ReadOnlySpan<byte> s)
    {
        uint n = R32(ref s);
        return n <= int.MaxValue ? (int)n : throw Bad("Section exceeds supported size.");
    }
    private static void U16(Stream output, int n)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, checked((ushort)n));
        output.Write(bytes);
    }
    private static void U32(Stream output, uint n)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, n);
        output.Write(bytes);
    }
    private static ArchiveException Bad(string message) => new(ArchiveErrorCode.E_PAYLOAD, message);
    private static ArchiveException Volumes(string message) => new(ArchiveErrorCode.E_VOLUMES, message);
}
