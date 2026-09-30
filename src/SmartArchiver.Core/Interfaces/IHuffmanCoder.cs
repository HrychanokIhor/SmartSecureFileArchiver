namespace SmartArchiver.Core.Abstractions;

public interface IHuffmanCoder
{
    byte[] Compress(byte[] data, out byte[] treeHeader);
    byte[] Decompress(byte[] compressedData, byte[] treeHeader);
}