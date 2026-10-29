using System.Buffers.Binary;

namespace SmartArchiver.Core.Services.Huffman;

/// <summary>Independent canonical Huffman alphabet. HUF1 is provisional, not FORMAT_SPEC_P2.</summary>
public sealed class CanonicalHuffmanCodec
{
    public sealed record Encoded(byte[] Data, byte[] Header);
    private sealed record Node(int Symbol, Node? Left = null, Node? Right = null);
    private readonly record struct Code(ulong Bits, int Length);
    private const int PrefixSize = 20;

    public Encoded Compress(IReadOnlyList<int> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        var frequencies = new SortedDictionary<int, long>();
        foreach (int symbol in symbols)
        {
            if ((uint)symbol > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(symbols), "Symbols must be in 0..65535.");
            frequencies[symbol] = frequencies.GetValueOrDefault(symbol) + 1;
        }

        // The minimum symbol breaks ties deterministically, independently of insertion order.
        var queue = new PriorityQueue<Node, (long Frequency, int MinSymbol)>();
        foreach (var (symbol, frequency) in frequencies)
            queue.Enqueue(new Node(symbol), (frequency, symbol));
        while (queue.Count > 1)
        {
            queue.TryDequeue(out var left, out var a);
            queue.TryDequeue(out var right, out var b);
            queue.Enqueue(new Node(-1, left, right),
                (a.Frequency + b.Frequency, Math.Min(a.MinSymbol, b.MinSymbol)));
        }

        var lengths = new SortedDictionary<int, int>();
        if (queue.Count != 0)
        {
            var stack = new Stack<(Node Node, int Depth)>();
            stack.Push((queue.Dequeue(), 0));
            while (stack.TryPop(out var item))
            {
                if (item.Node.Symbol >= 0)
                    lengths.Add(item.Node.Symbol, Math.Max(1, item.Depth));
                else
                {
                    stack.Push((item.Node.Right!, item.Depth + 1));
                    stack.Push((item.Node.Left!, item.Depth + 1));
                }
            }
        }
        var codes = BuildCodes(lengths);
        long bitCount = frequencies.Sum(p => p.Value * lengths[p.Key]);
        int byteCount = checked((int)((bitCount + 7) / 8));
        var data = new byte[byteCount];
        long position = 0;
        foreach (int symbol in symbols)
        {
            Code code = codes[symbol];
            for (int bit = code.Length - 1; bit >= 0; bit--, position++)
                if (((code.Bits >> bit) & 1) != 0)
                    data[(int)(position / 8)] |= (byte)(1 << (7 - (int)(position % 8)));
        }

        var header = new byte[PrefixSize + 3 * lengths.Count];
        "HUF1"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), symbols.Count);
        BinaryPrimitives.WriteInt64BigEndian(header.AsSpan(8), bitCount);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16), lengths.Count);
        int offset = PrefixSize;
        foreach (var (symbol, length) in lengths)
        {
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(offset), (ushort)symbol);
            header[offset + 2] = checked((byte)length);
            offset += 3;
        }
        return new Encoded(data, header);
    }

    /// <summary>Limit is checked before output allocation; the caller chooses a block budget.</summary>
    public int[] Decompress(byte[] data, byte[] header, int maxSymbols = 16 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSymbols);
        if (header.Length < PrefixSize || !header.AsSpan(0, 4).SequenceEqual("HUF1"u8))
            throw Bad("Invalid Huffman header.");
        int count = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4));
        long bitCount = BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(8));
        int alphabet = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16));
        if (count < 0 || count > maxSymbols || alphabet < 0 || alphabet > 65536 ||
            header.Length != PrefixSize + 3 * alphabet || bitCount < 0 ||
            bitCount > (long)data.Length * 8 || (bitCount + 7) / 8 != data.Length ||
            (count == 0 ? alphabet != 0 || bitCount != 0 : alphabet == 0 || alphabet > count))
            throw Bad("Invalid counts or block exceeds output limit.");
        if (count > 0 && (bitCount < count || bitCount > (long)count * 63))
            throw Bad("Impossible encoded length.");
        if (bitCount % 8 != 0 && (data[^1] & ((1 << (8 - (int)(bitCount % 8))) - 1)) != 0)
            throw Bad("Nonzero padding.");

        var lengths = new SortedDictionary<int, int>();
        int previousSymbol = -1;
        for (int i = 0; i < alphabet; i++)
        {
            int offset = PrefixSize + i * 3;
            int symbol = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(offset));
            int length = header[offset + 2];
            if (symbol <= previousSymbol || length is < 1 or > 63)
                throw Bad("Unsorted, duplicate or invalid code length.");
            lengths.Add(symbol, length);
            previousSymbol = symbol;
        }
        var codes = BuildCodes(lengths);
        var decoding = codes.ToDictionary(p => (p.Value.Bits, p.Value.Length), p => p.Key);
        var result = new int[count];
        ulong current = 0;
        int currentLength = 0, written = 0;
        int longest = lengths.Count == 0 ? 0 : lengths.Values.Max();
        for (long position = 0; position < bitCount; position++)
        {
            current = (current << 1) | (uint)((data[(int)(position / 8)] >> (7 - (int)(position % 8))) & 1);
            currentLength++;
            if (decoding.TryGetValue((current, currentLength), out int symbol))
            {
                if (written == result.Length) throw Bad("Extra encoded symbols.");
                result[written++] = symbol;
                current = 0;
                currentLength = 0;
            }
            else if (currentLength >= longest) throw Bad("Unknown code.");
        }
        if (written != count || currentLength != 0) throw Bad("Truncated encoded stream.");
        return result;
    }

    private static Dictionary<int, Code> BuildCodes(SortedDictionary<int, int> lengths)
    {
        var result = new Dictionary<int, Code>();
        ulong next = 0;
        int previousLength = 0;
        foreach (var (symbol, length) in lengths.OrderBy(p => p.Value).ThenBy(p => p.Key))
        {
            if (length is < 1 or > 63) throw Bad("Unsupported code length.");
            next <<= length - previousLength;
            if (next >= (1UL << length)) throw Bad("Oversubscribed code lengths.");
            result.Add(symbol, new Code(next, length));
            next++;
            previousLength = length;
        }
        if (lengths.Count == 1 && previousLength != 1)
            throw Bad("Singleton alphabet must use one bit.");
        if (lengths.Count > 1 && next != (1UL << previousLength))
            throw Bad("Incomplete code tree.");
        return result;
    }

    private static InvalidDataException Bad(string message) => new(message);
}
