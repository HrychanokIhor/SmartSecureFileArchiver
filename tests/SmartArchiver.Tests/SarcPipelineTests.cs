using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Services;
using SmartArchiver.Core.Services.Crypto;
using SmartArchiver.Core.Services.Huffman;

namespace SmartArchiver.Tests;

public class SarcPipelineTests
{
    // FORMAT_SPEC_P2 §8: tables, 14 tokens, seven stream bytes.
    private static byte[] ReferenceBlock() => Convert.FromHexString(
        "0009002003006404006504006804006C02006F03007204007703010003" +
        "000200CF0100FF01" +
        "0001000401" +
        "0000000E00000007ED06A3F3159D18");

    [Fact]
    public void DecodesPublishedBlockAndMatchesCrc()
    {
        byte[] block = ReferenceBlock();
        Assert.Equal(57, block.Length);
        var tokens = new SarcBlockCodec().Decode(block, 480);
        Assert.Equal(14, tokens.Length);
        Assert.Equal(HuffmanToken.FromMatch(258, 12), tokens[12]);
        Assert.Equal(HuffmanToken.FromMatch(210, 12), tokens[13]);
        byte[] result = new SarcPipeline().Decompress(block, 480);
        Assert.Equal(System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("hello world ", 40))), result);
        Assert.Equal(0x70208846u, Crc32.Compute(result));
    }

    [Fact]
    public void SingletonMatchesIndependentWireVector()
    {
        byte[] expected = Convert.FromHexString("000100410100000000000000030000000100");
        var codec = new SarcBlockCodec();
        HuffmanToken[] tokens = [HuffmanToken.FromLiteral(65), HuffmanToken.FromLiteral(65), HuffmanToken.FromLiteral(65)];
        Assert.Equal(expected, codec.Encode(tokens));
        Assert.Equal(tokens, codec.Decode(expected, 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16384)]
    public void RoundTripsConfiguredBlockSizes(int size)
    {
        var original = new byte[size];
        new Random(42).NextBytes(original);
        var pipeline = new SarcPipeline();
        Assert.Equal(original, pipeline.Decompress(pipeline.Compress(original), size));
    }

    [Fact]
    public void RoundTripsMatchBoundaries()
    {
        var tokens = Enumerable.Repeat(HuffmanToken.FromLiteral(42), 32768).Concat(
            [HuffmanToken.FromMatch(3, 1), HuffmanToken.FromMatch(258, 32768)]).ToArray();
        var codec = new SarcBlockCodec();
        Assert.Equal(tokens, codec.Decode(codec.Encode(tokens), 33029));
    }

    [Fact]
    public void SkewedFrequenciesKeepAllSymbolsWithinFifteenBits()
    {
        var tokens = new List<HuffmanToken>();
        int a = 1, b = 1;
        for (byte symbol = 0; symbol < 20; symbol++)
        {
            tokens.AddRange(Enumerable.Repeat(HuffmanToken.FromLiteral(symbol), a));
            (a, b) = (b, a + b);
        }
        var codec = new SarcBlockCodec();
        var block = codec.Encode(tokens);
        Assert.Equal(20, block[1]);
        for (int i = 0; i < 20; i++) Assert.InRange(block[4 + 3 * i], (byte)1, (byte)15);
        Assert.Equal(tokens.ToArray(), codec.Decode(block, tokens.Count));
    }

    [Theory]
    [InlineData(4, 16)] // overlong code
    [InlineData(4, 2)]  // wrong Kraft sum
    [InlineData(40, 16)] // DIST outside the alphabet
    [InlineData(56, 25)] // nonzero padding
    public void RejectsInvalidCodes(int position, byte replacement)
    {
        var block = ReferenceBlock();
        block[position] = replacement;
        Assert.Equal(ArchiveErrorCode.E_CODE,
            Assert.Throws<ArchiveException>(() => new SarcBlockCodec().Decode(block)).Code);
    }

    [Fact]
    public void RejectsTruncationTrailingBytesAndWrongRawLength()
    {
        var codec = new SarcBlockCodec();
        var block = ReferenceBlock();
        foreach (var bad in new[] { block[..^1], block.Concat(new byte[1]).ToArray(), new byte[] { 0 } })
            Assert.Equal(ArchiveErrorCode.E_PAYLOAD,
                Assert.Throws<ArchiveException>(() => codec.Decode(bad)).Code);
        Assert.Equal(ArchiveErrorCode.E_PAYLOAD,
            Assert.Throws<ArchiveException>(() => codec.Decode(block, 479)).Code);
    }
}
