using SmartArchiver.Core.Abstractions;
using SmartArchiver.Core.Services.Huffman;

namespace SmartArchiver.Core.Services;

/// <summary>Byte adapter for the existing interface. Header format is provisional HUF1.</summary>
public sealed class HuffmanCoder : IHuffmanCoder
{
    private readonly CanonicalHuffmanCodec _codec = new();

    public byte[] Compress(byte[] data, out byte[] treeHeader)
    {
        ArgumentNullException.ThrowIfNull(data);
        var encoded = _codec.Compress(Array.ConvertAll(data, b => (int)b));
        treeHeader = encoded.Header;
        return encoded.Data;
    }

    public byte[] Decompress(byte[] compressedData, byte[] treeHeader)
    {
        int[] symbols = _codec.Decompress(compressedData, treeHeader);
        var result = new byte[symbols.Length];
        for (int i = 0; i < symbols.Length; i++)
        {
            if ((uint)symbols[i] > byte.MaxValue)
                throw new InvalidDataException("Non-byte symbol in byte Huffman stream.");
            result[i] = (byte)symbols[i];
        }
        return result;
    }
}
