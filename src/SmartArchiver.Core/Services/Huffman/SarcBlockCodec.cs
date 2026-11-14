using System.Buffers.Binary;
using System.Numerics;
using SmartArchiver.Core.Exceptions;

namespace SmartArchiver.Core.Services.Huffman;

/// <summary>Reads and writes the compressed block defined by FORMAT_SPEC_P2 v1, section 3.</summary>
public sealed class SarcBlockCodec
{
    private const int Match = 256;
    private const int MaxBits = 15;
    private readonly record struct Code(int Bits, int Length);

    public byte[] Encode(IReadOnlyList<HuffmanToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var lit = new int[257];
        var len = new int[256];
        var dist = new int[16];
        foreach (var token in tokens)
        {
            if (token.Literal is byte b && token.Length == 0 && token.Distance == 0)
                lit[b]++;
            else if (token.Literal is null && token.Length is >= 3 and <= 258 &&
                     token.Distance is >= 1 and <= 32768)
            {
                lit[Match]++;
                len[token.Length - 3]++;
                dist[DistanceCode(token.Distance)]++;
            }
            else throw new ArgumentException("Token is outside the SARC v1 alphabet.", nameof(tokens));
        }

        var litCodes = BuildCodes(BuildLengths(lit));
        var lenCodes = BuildCodes(BuildLengths(len));
        var distCodes = BuildCodes(BuildLengths(dist));
        var bits = new BitWriter();
        foreach (var token in tokens)
        {
            bits.Write(litCodes[token.Literal is byte b ? b : Match]);
            if (token.Literal.HasValue) continue;
            bits.Write(lenCodes[token.Length - 3]);
            int code = DistanceCode(token.Distance);
            bits.Write(distCodes[code]);
            int extra = Math.Max(0, code - 1);
            bits.Write(new Code((token.Distance - 1) & ((1 << extra) - 1), extra));
        }
        byte[] stream = bits.Finish();
        using var result = new MemoryStream();
        WriteTable(result, litCodes);
        WriteTable(result, lenCodes);
        WriteTable(result, distCodes);
        WriteUInt32(result, (uint)tokens.Count);
        WriteUInt32(result, (uint)stream.Length);
        result.Write(stream);
        return result.ToArray();
    }

    /// <summary>When supplied, expectedRawLength is the validated raw_len from the block table.</summary>
    public HuffmanToken[] Decode(ReadOnlySpan<byte> block, int? expectedRawLength = null)
    {
        if (expectedRawLength < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedRawLength));

        var lit = ReadTable(ref block, 257);
        var len = ReadTable(ref block, 256);
        var dist = ReadTable(ref block, 16);
        uint tokenCount = BinaryPrimitives.ReadUInt32BigEndian(Take(ref block, 4));
        uint streamLength = BinaryPrimitives.ReadUInt32BigEndian(Take(ref block, 4));
        if (streamLength != block.Length)
            throw PayloadError("Stream length does not match block size.");
        if (tokenCount > (long)block.Length * 8 || tokenCount > int.MaxValue ||
            (expectedRawLength is int expected && tokenCount > expected))
            throw PayloadError("Token count cannot fit the stream or the declared block output.");

        var reader = new BitReader(block);
        var tokens = new HuffmanToken[(int)tokenCount];
        long produced = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            int symbol = reader.ReadSymbol(lit);
            if (symbol != Match)
            {
                tokens[i] = HuffmanToken.FromLiteral((byte)symbol);
                produced++;
            }
            else
            {
                int length = reader.ReadSymbol(len) + 3;
                int code = reader.ReadSymbol(dist);
                int distance = code switch
                {
                    0 => 1,
                    1 => 2,
                    _ => ((1 << (code - 1)) | reader.Read(code - 1)) + 1
                };
                if (distance > produced)
                    throw CodeError("Match starts before the decoded block.");
                tokens[i] = HuffmanToken.FromMatch((ushort)length, (ushort)distance);
                produced += length;
            }
            if (produced > int.MaxValue || produced > expectedRawLength)
                throw PayloadError("Decoded block exceeds the output length.");
        }
        if (reader.Remaining >= 8) throw PayloadError("Extra bytes after the token stream.");
        if (reader.Read((int)reader.Remaining) != 0) throw CodeError("Nonzero stream padding.");
        if (expectedRawLength is int size && produced != size)
            throw PayloadError("Decoded block does not match raw_len.");
        return tokens;
    }

    private static int DistanceCode(int distance) =>
        BitOperations.Log2((uint)(distance - 1)) + (distance == 1 ? 0 : 1);

    // Binary package-merge: merge leaves with paired packages at each level.
    // The cheapest 2n-2 items on the final level determine the leaf lengths.
    private sealed record Package(long Weight, int Symbol, Package? Left = null, Package? Right = null);

    private static int[] BuildLengths(int[] frequencies)
    {
        var lengths = new int[frequencies.Length];
        var leaves = frequencies.Select((weight, symbol) => new Package(weight, symbol))
            .Where(x => x.Weight != 0).OrderBy(x => x.Weight).ToArray();
        if (leaves.Length == 0) return lengths;
        if (leaves.Length == 1)
        {
            lengths[leaves[0].Symbol] = 1;
            return lengths;
        }

        var level = leaves;
        for (int depth = 1; depth < MaxBits; depth++)
        {
            var packages = new List<Package>(level.Length / 2);
            for (int i = 0; i + 1 < level.Length; i += 2)
                packages.Add(new Package(level[i].Weight + level[i + 1].Weight, -1, level[i], level[i + 1]));
            // Stable ordering: same weights prefer leaves, then creation order.
            level = leaves.Concat(packages).OrderBy(x => x.Weight).ToArray();
        }
        foreach (var item in level.Take(2 * leaves.Length - 2)) CountLeaves(item, lengths);
        return lengths;
    }

    private static void CountLeaves(Package item, int[] lengths)
    {
        if (item.Symbol >= 0) lengths[item.Symbol]++;
        else
        {
            CountLeaves(item.Left!, lengths);
            CountLeaves(item.Right!, lengths);
        }
    }

    private static Code[] BuildCodes(int[] lengths)
    {
        var counts = new int[MaxBits + 1];
        int symbols = 0;
        int kraft = 0;
        foreach (int length in lengths)
        {
            if (length == 0) continue;
            counts[length]++;
            symbols++;
            kraft += 1 << (MaxBits - length);
        }
        if ((symbols == 1 && counts[1] != 1) || (symbols > 1 && kraft != 1 << MaxBits))
            throw CodeError("Invalid canonical table: code lengths do not form a complete code.");

        var next = new int[MaxBits + 1];
        for (int i = 1; i <= MaxBits; i++) next[i] = (next[i - 1] + counts[i - 1]) << 1;
        var codes = new Code[lengths.Length];
        for (int symbol = 0; symbol < lengths.Length; symbol++)
        {
            int length = lengths[symbol];
            if (length != 0) codes[symbol] = new Code(next[length]++, length);
        }
        return codes;
    }

    private static Dictionary<Code, int> ReadTable(ref ReadOnlySpan<byte> block, int alphabet)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(Take(ref block, 2));
        if (count > alphabet) throw CodeError("Too many symbols for the alphabet.");
        var lengths = new int[alphabet];
        int previous = -1;
        for (int i = 0; i < count; i++)
        {
            var entry = Take(ref block, 3);
            int symbol = BinaryPrimitives.ReadUInt16BigEndian(entry);
            int length = entry[2];
            if (symbol <= previous || symbol >= alphabet || length is < 1 or > MaxBits)
                throw CodeError("Invalid symbol order, symbol value, or code length.");
            lengths[symbol] = length;
            previous = symbol;
        }
        var codes = BuildCodes(lengths);
        var decode = new Dictionary<Code, int>(count);
        for (int i = 0; i < codes.Length; i++)
            if (codes[i].Length != 0) decode.Add(codes[i], i);
        return decode;
    }

    private static void WriteTable(Stream target, Code[] codes)
    {
        Span<byte> field = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(field, (ushort)codes.Count(c => c.Length != 0));
        target.Write(field);
        for (int symbol = 0; symbol < codes.Length; symbol++)
        {
            if (codes[symbol].Length == 0) continue;
            BinaryPrimitives.WriteUInt16BigEndian(field, (ushort)symbol);
            target.Write(field);
            target.WriteByte((byte)codes[symbol].Length);
        }
    }

    private static ReadOnlySpan<byte> Take(ref ReadOnlySpan<byte> source, int length)
    {
        if (source.Length < length) throw PayloadError("Truncated compressed block.");
        var result = source[..length];
        source = source[length..];
        return result;
    }

    private static void WriteUInt32(Stream target, uint value)
    {
        Span<byte> field = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(field, value);
        target.Write(field);
    }

    private sealed class BitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _current;
        private int _used;

        public void Write(Code code)
        {
            for (int i = code.Length - 1; i >= 0; i--)
            {
                _current = (_current << 1) | ((code.Bits >> i) & 1);
                if (++_used == 8)
                {
                    _bytes.Add((byte)_current);
                    _used = _current = 0;
                }
            }
        }

        public byte[] Finish()
        {
            if (_used != 0) _bytes.Add((byte)(_current << (8 - _used)));
            return _bytes.ToArray();
        }
    }

    private ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> _bytes;
        private long _position;

        public BitReader(ReadOnlySpan<byte> bytes) => _bytes = bytes;

        public long Remaining => (long)_bytes.Length * 8 - _position;

        public int Read(int count)
        {
            if (count > Remaining) throw CodeError("Truncated token stream.");
            int value = 0;
            for (int i = 0; i < count; i++, _position++)
            {
                int bit = (_bytes[(int)(_position / 8)] >> (7 - (int)(_position % 8))) & 1;
                value = (value << 1) | bit;
            }
            return value;
        }

        public int ReadSymbol(Dictionary<Code, int> table)
        {
            if (table.Count == 0) throw CodeError("Required Huffman table is empty.");
            int value = 0;
            for (int length = 1; length <= MaxBits; length++)
            {
                value = (value << 1) | Read(1);
                if (table.TryGetValue(new Code(value, length), out int symbol)) return symbol;
            }
            throw CodeError("Unknown Huffman code.");
        }
    }

    private static ArchiveException CodeError(string message) => new(ArchiveErrorCode.E_CODE, message);
    private static ArchiveException PayloadError(string message) => new(ArchiveErrorCode.E_PAYLOAD, message);
}
