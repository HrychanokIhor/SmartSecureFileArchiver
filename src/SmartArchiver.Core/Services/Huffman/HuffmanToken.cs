namespace SmartArchiver.Core.Services.Huffman
{
    public class HuffmanToken
    {
        public bool IsLiteral { get; set; }
        public byte LiteralValue { get; set; }
        public ushort MatchLength { get; set; }
        public ushort MatchDistance { get; set; }

        public static HuffmanToken FromLiteral(byte value) => new HuffmanToken { IsLiteral = true, LiteralValue = value };
        public static HuffmanToken FromMatch(ushort length, ushort distance) => new HuffmanToken { IsLiteral = false, MatchLength = length, MatchDistance = distance };
    }
}