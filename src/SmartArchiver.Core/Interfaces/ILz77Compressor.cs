namespace SmartArchiver.Core.Abstractions;

public interface ILz77Compressor
{
    byte[] Compress(byte[] data, int windowSize = 4096);
    byte[] Decompress(byte[] compressedData);
}