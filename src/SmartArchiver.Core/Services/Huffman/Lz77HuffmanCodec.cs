namespace SmartArchiver.Core.Services.Huffman;

/// <summary>Boundary model: exactly a literal OR a match, without a trailing NextByte.</summary>
public readonly record struct HuffmanToken(byte? Literal, ushort Length, ushort Distance)
{
    public static HuffmanToken FromLiteral(byte value) => new(value, 0, 0);
    public static HuffmanToken FromMatch(ushort length, ushort distance) => new(null, length, distance);
}

/// <summary>Three independent canonical alphabets. Does not define the final SARC container.</summary>
public sealed class Lz77HuffmanCodec
{
    // 0..255 = literal, 256 = match; marker preserves the original token order.
    private const int Match = 256;
    public sealed record Block(CanonicalHuffmanCodec.Encoded Literals,
        CanonicalHuffmanCodec.Encoded Lengths, CanonicalHuffmanCodec.Encoded Distances);
    private readonly CanonicalHuffmanCodec _codec = new();

    public Block Compress(IReadOnlyList<HuffmanToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var literals = new List<int>(tokens.Count);
        var lengths = new List<int>();
        var distances = new List<int>();
        foreach (var token in tokens)
        {
            Validate(token);
            literals.Add(token.Literal is byte value ? value : Match);
            if (token.Literal is null)
            {
                lengths.Add(token.Length);
                distances.Add(token.Distance);
            }
        }
        return new Block(_codec.Compress(literals), _codec.Compress(lengths), _codec.Compress(distances));
    }

    public HuffmanToken[] Decompress(Block block, int maxTokens = 16 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(block.Literals);
        ArgumentNullException.ThrowIfNull(block.Lengths);
        ArgumentNullException.ThrowIfNull(block.Distances);
        int[] literals = _codec.Decompress(block.Literals.Data, block.Literals.Header, maxTokens);
        int matchCount = literals.Count(symbol => symbol == Match);
        int[] lengths = _codec.Decompress(block.Lengths.Data, block.Lengths.Header, matchCount);
        int[] distances = _codec.Decompress(block.Distances.Data, block.Distances.Header, matchCount);
        if (lengths.Length != matchCount || distances.Length != matchCount)
            throw new InvalidDataException("Match streams have inconsistent counts.");
        var result = new HuffmanToken[literals.Length];
        int index = 0;
        for (int i = 0; i < literals.Length; i++)
        {
            int symbol = literals[i];
            if (symbol < Match) result[i] = HuffmanToken.FromLiteral((byte)symbol);
            else if (symbol == Match)
            {
                result[i] = HuffmanToken.FromMatch((ushort)lengths[index], (ushort)distances[index]);
                index++;
                Validate(result[i]);
            }
            else throw new InvalidDataException("Invalid literal/match symbol.");
        }
        return result;
    }

    private static void Validate(HuffmanToken token)
    {
        if (token.Literal.HasValue ? token.Length != 0 || token.Distance != 0 : token.Length == 0 || token.Distance == 0)
            throw new InvalidDataException("Token must be a literal or a positive length/distance match.");
    }
}
