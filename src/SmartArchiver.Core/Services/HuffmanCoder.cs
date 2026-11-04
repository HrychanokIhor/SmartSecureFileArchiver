using SmartArchiver.Core.Abstractions;

namespace SmartArchiver.Core.Services;

public class HuffmanCoder : IHuffmanCoder
{
    public byte[] Compress(byte[] data, out byte[] treeHeader)
    {
        // Тимчасово: повертаємо порожній заголовок та вхідні байти "як є"
        treeHeader = Array.Empty<byte>();
        return data;
    }

    public byte[] Decompress(byte[] compressedData, byte[] treeHeader)
    {
        // Тимчасово: повертаємо стиснені байти "як є"
        return compressedData;
    }
}