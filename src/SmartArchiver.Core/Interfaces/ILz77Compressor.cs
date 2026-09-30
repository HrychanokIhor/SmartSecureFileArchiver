namespace SmartArchiver.Core.Abstractions;

public record Lz77Token(ushort Offset, ushort Length, byte NextByte);

public interface ILz77Compressor
{
    List<Lz77Token> Compress(byte[] data, int windowSize = 4096);
    byte[] Decompress(List<Lz77Token> tokens);
}
