using System.Buffers.Binary;
using System.Text;
using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Services.Crypto;

namespace SmartArchiver.Core.Services;

/// <summary>SARC v1 payload for the CLI's single-file, single-volume workflow.</summary>
internal sealed class SingleFilePayloadCodec(ArchiverController controller)
{
    private const int BlockSize = 16384;
    private const int MaxVolumeBytes = 65536; // Team T01 writer limit, not a reader limit.
    private const byte Raw = 1;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly SarcPipeline _pipeline = new();

    private sealed record Block(int RawLength, byte[] Bytes, bool IsRaw);
    private readonly record struct Entry(int RawLength, int CodedLength, int Offset, bool IsRaw);

    public byte[] Encode(string name, byte[] input)
    {
        ValidateName(name);
        byte[] encodedName = Utf8.GetBytes(name);
        if (encodedName.Length > ushort.MaxValue) throw Bad("File name is too long.");
        byte type = input.Length == 0 ? (byte)6 : (byte)controller.Classify(input);
        bool lazy = type is 0 or 1;
        var blocks = new List<Block>();
        long volumeSize = VolumeFormat.Overhead + 18 + encodedName.Length;
        int offset = 0;
        do
        {
            byte[] raw = input.AsSpan(offset, Math.Min(BlockSize, input.Length - offset)).ToArray();
            byte[] coded = raw.Length == 0 || type == 5 ? raw : _pipeline.Compress(raw, lazy);
            bool isRaw = coded.Length >= raw.Length;
            var block = new Block(raw.Length, isRaw ? raw : coded, isRaw);
            volumeSize += 15 + block.Bytes.Length;
            if (volumeSize > MaxVolumeBytes || blocks.Count == ushort.MaxValue)
                throw new ArchiveException(ArchiveErrorCode.E_VOLUMES,
                    "File requires multiple volumes, which this command does not yet support.");
            blocks.Add(block);
            offset += raw.Length;
        } while (offset < input.Length);

        using var output = new MemoryStream();
        Write16(output, 1);
        Write16(output, (ushort)0); // flags, reserved
        Write16(output, (ushort)encodedName.Length);
        output.Write(encodedName);
        Write32(output, (uint)input.Length);
        Write32(output, Crc32.Compute(input));
        output.WriteByte(type);
        output.WriteByte(blocks.All(b => b.IsRaw) ? (byte)0 : (byte)1);
        Write16(output, (ushort)blocks.Count);

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            Write16(output, 0);
            Write32(output, (uint)i);
            Write32(output, (uint)block.RawLength);
            Write32(output, (uint)block.Bytes.Length);
            output.WriteByte(block.IsRaw ? Raw : (byte)0);
        }
        foreach (var block in blocks) output.Write(block.Bytes);
        return output.ToArray();
    }

    public byte[] Decode(ReadOnlySpan<byte> payload)
    {
        if (Read16(ref payload) != 1) throw Bad("This command requires exactly one file.");
        if (Read16(ref payload) != 0) throw Bad("Unsupported flags or nonzero reserved field.");

        int nameLength = Read16(ref payload);
        string name;
        try { name = Utf8.GetString(Take(ref payload, nameLength)); }
        catch (DecoderFallbackException) { throw Bad("File name is not valid UTF-8."); }
        ValidateName(name);
        int originalSize = ReadSize(ref payload);
        uint crc = Read32(ref payload);
        if (Take(ref payload, 2)[0] > 6) throw Bad("Invalid file type.");

        int count = Read16(ref payload);
        ReadOnlySpan<byte> table = Take(ref payload, count * 15);
        var entries = new Entry?[count];
        long rawTotal = 0, codedTotal = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            if (Read16(ref table) != 0) throw Bad("Invalid file index.");
            uint seq = Read32(ref table);
            int raw = ReadSize(ref table);
            int coded = ReadSize(ref table);
            byte flags = Take(ref table, 1)[0];
            bool isRaw = flags == Raw;
            if (seq >= count || entries[(int)seq].HasValue ||
                flags > Raw || (isRaw && coded != raw))
                throw Bad("Invalid block sequence, flags or lengths.");
            if (coded > payload.Length - codedTotal) throw Bad("Truncated block data.");
            entries[(int)seq] = new Entry(raw, coded, (int)codedTotal, isRaw);
            rawTotal += raw;
            codedTotal += coded;
        }

        if (rawTotal != originalSize || codedTotal != payload.Length)
            throw Bad("Payload lengths do not match the file and block tables.");

        // Offsets follow table order; reconstruction follows block_seq.
        using var output = new MemoryStream();
        foreach (var item in entries)
        {
            var entry = item!.Value; // count distinct sequence numbers in [0,count)
            var bytes = payload.Slice(entry.Offset, entry.CodedLength);
            if (entry.IsRaw) output.Write(bytes);
            else output.Write(_pipeline.Decompress(bytes.ToArray(), entry.RawLength));
        }
        byte[] original = output.ToArray();
        if (original.Length != originalSize || Crc32.Compute(original) != crc)
            throw new ArchiveException(ArchiveErrorCode.E_CRC, "File CRC32 or size does not match.");
        return original;
    }

    private static void ValidateName(string name)
    {
        if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(['/', '\\', ':', '\0']) >= 0)
            throw Bad("Invalid archive file name.");
    }

    private static ushort Read16(ref ReadOnlySpan<byte> data) => BinaryPrimitives.ReadUInt16BigEndian(Take(ref data, 2));
    private static uint Read32(ref ReadOnlySpan<byte> data) => BinaryPrimitives.ReadUInt32BigEndian(Take(ref data, 4));
    private static int ReadSize(ref ReadOnlySpan<byte> data)
    {
        uint size = Read32(ref data);
        return size <= int.MaxValue ? (int)size : throw Bad("Section exceeds supported size.");
    }

    private static ReadOnlySpan<byte> Take(ref ReadOnlySpan<byte> data, int size)
    {
        if (size > data.Length) throw Bad("Truncated payload.");
        var field = data[..size];
        data = data[size..];
        return field;
    }

    private static void Write16(Stream stream, ushort value)
    {
        Span<byte> field = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(field, value);
        stream.Write(field);
    }

    private static void Write32(Stream stream, uint value)
    {
        Span<byte> field = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(field, value);
        stream.Write(field);
    }

    private static ArchiveException Bad(string message) => new(ArchiveErrorCode.E_PAYLOAD, message);
}
