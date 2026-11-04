using System.Buffers.Binary;
using System.Text;
using SmartArchiver.Core.Services;
using SmartArchiver.Core.Services.Huffman;
namespace SmartArchiver.Tests.Huffman;
public class HuffmanTests
{
    [Fact]
    public void RoundTripAndDeterminism()
    {
        var coder = new HuffmanCoder();
        var random = new Random(42);
        var cases = new List<byte[]> { Array.Empty<byte>(), new byte[] {42}, new byte[8192],
            Enumerable.Range(0,256).Select(i => (byte)i).ToArray(),
            Encoding.UTF8.GetBytes("Алгоритми та структури даних! Алгоритми та структури даних!") };
        for (int size = 1; size < 10000; size = size * 2 + 1)
        {
            var input = new byte[size]; random.NextBytes(input); cases.Add(input);
        }
        foreach (var input in cases)
        {
            var data = coder.Compress(input, out var header);
            Assert.Equal(input, coder.Decompress(data, header));
            Assert.Equal(data, coder.Compress(input, out var header2));
            Assert.Equal(header, header2);
        }
    }
    [Fact]
    public void HandComputedCanonicalVector()
    {
        var e = new CanonicalHuffmanCodec().Compress(new[] {0,1,2,3});
        Assert.Equal(new byte[] {0x1b}, e.Data); // 00 01 10 11
        Assert.Equal(new byte[] {0,0,2,0,1,2,0,2,2,0,3,2}, e.Header[20..]);
    }
    [Fact]
    public void SingletonCompressesIncludingHeader()
    {
        var coder = new HuffmanCoder(); var input = new byte[8192];
        var data = coder.Compress(input, out var header);
        Assert.True((data.Length + header.Length) * 4 < input.Length);
        Assert.Equal(input, coder.Decompress(data, header));
    }
    [Fact]
    public void ThreeAlphabetsPreserveTerminalMatch()
    {
        HuffmanToken[] input = [HuffmanToken.FromLiteral(65), HuffmanToken.FromLiteral(66),
            HuffmanToken.FromMatch(1024,2), HuffmanToken.FromLiteral(255), HuffmanToken.FromMatch(65535,65535)];
        var codec = new Lz77HuffmanCodec();
        Assert.Equal(input, codec.Decompress(codec.Compress(input)));
        Assert.Empty(codec.Decompress(codec.Compress(Array.Empty<HuffmanToken>())));
        Assert.Equal(input[..2], codec.Decompress(codec.Compress(input[..2])));
    }
    [Fact]
    public void RejectsBadTablesAndCounts()
    {
        var codec = new CanonicalHuffmanCodec(); var e = codec.Compress(new[] {0,1,2,3});
        Assert.Throws<InvalidDataException>(() => codec.Decompress([],e.Header));
        Assert.Throws<InvalidDataException>(() => codec.Decompress([0x1b,0],e.Header));
        Assert.Throws<InvalidDataException>(() => codec.Decompress(e.Data,e.Header[..^1]));
        Assert.Throws<InvalidDataException>(() => codec.Decompress(e.Data,e.Header,3));
        var h = (byte[])e.Header.Clone(); h[25] = 1;
        Assert.Throws<InvalidDataException>(() => codec.Decompress(e.Data,h));
        h = (byte[])e.Header.Clone(); h[22] = h[25] = h[28] = h[31] = 3;
        Assert.Throws<InvalidDataException>(() => codec.Decompress(e.Data,h));
        h = (byte[])e.Header.Clone(); h[24] = 0;
        Assert.Throws<InvalidDataException>(() => codec.Decompress(e.Data,h));
        h = (byte[])e.Header.Clone(); BinaryPrimitives.WriteInt32BigEndian(h.AsSpan(4),int.MaxValue);
        Assert.Throws<InvalidDataException>(() => codec.Decompress(e.Data,h));
    }
    [Fact]
    public void RejectsPaddingAndUnknownCode()
    {
        var codec = new CanonicalHuffmanCodec(); var e = codec.Compress(new[] {7});
        Assert.Throws<InvalidDataException>(() => codec.Decompress([1],e.Header));
        Assert.Throws<InvalidDataException>(() => codec.Decompress([128],e.Header));
    }
    [Fact]
    public void RejectsBadTokensAndMismatchedStreams()
    {
        var codec = new Lz77HuffmanCodec();
        Assert.Throws<InvalidDataException>(() => codec.Compress(new[] {HuffmanToken.FromMatch(0,1)}));
        var match = codec.Compress(new[] {HuffmanToken.FromMatch(3,1)});
        var empty = codec.Compress(Array.Empty<HuffmanToken>());
        Assert.Throws<InvalidDataException>(() => codec.Decompress(match with {Lengths = empty.Lengths}));
    }
    [Fact]
    public void WideSymbolsAndByteBoundary()
    {
        var codec = new CanonicalHuffmanCodec(); int[] input = [0,256,32768,65535,256];
        var e = codec.Compress(input); Assert.Equal(input,codec.Decompress(e.Data,e.Header));
        Assert.Throws<InvalidDataException>(() => new HuffmanCoder().Decompress(e.Data,e.Header));
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Compress(new[] {-1}));
    }
}
