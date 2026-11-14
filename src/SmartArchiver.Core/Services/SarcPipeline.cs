using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Services.Huffman;

namespace SmartArchiver.Core.Services;

/// <summary>Compresses one SARC v1 block; volume framing and raw blocks belong to the caller.</summary>
public class SarcPipeline
{
    private readonly Lz77Algorithm _lz77 = new();
    private readonly SarcBlockCodec _huffman = new();

    public byte[] Compress(byte[] input, bool lazy = true)
    {
        ArgumentNullException.ThrowIfNull(input);
        return _huffman.Encode(Lz77HuffmanAdapter.ToHuffman(lazy ? _lz77.EncodeLazy(input) : _lz77.Encode(input)));
    }

    public byte[] Decompress(byte[] compressedData, int? expectedRawLength = null)
    {
        ArgumentNullException.ThrowIfNull(compressedData);
        var tokens = _huffman.Decode(compressedData, expectedRawLength);
        return _lz77.Decode(Lz77HuffmanAdapter.FromHuffman(tokens));
    }
}
