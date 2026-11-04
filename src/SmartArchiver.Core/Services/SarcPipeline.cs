using System.IO;
using System.Collections.Generic;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Services.Huffman;

namespace SmartArchiver.Core.Services
{
    public class SarcPipeline
    {
        private readonly Lz77Algorithm _lz77 = new();
        private readonly Lz77HuffmanCodec _huffman = new();

        public byte[] Compress(byte[] input)
        {
            // 1. LZ77 -> Адаптер (тепер це IReadOnlyList)
            var lzTokens = _lz77.EncodeLazy(input);
            var huffTokens = Lz77HuffmanAdapter.ToHuffman(lzTokens);

            // 2. Гафман
            var block = _huffman.Compress(huffTokens);

            // 3. Злиття у фінальний файл
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            WriteEncoded(writer, block.Literals);
            WriteEncoded(writer, block.Lengths);
            WriteEncoded(writer, block.Distances);

            return ms.ToArray();
        }
        public byte[] Decompress(byte[] compressedData)
        {
            using var ms = new MemoryStream(compressedData);
            using var reader = new BinaryReader(ms);

            // 1. Читаємо три алфавіти Гафмана з бінарного потоку
            var literals = ReadEncoded(reader);
            var lengths = ReadEncoded(reader);
            var distances = ReadEncoded(reader);
            var block = new Lz77HuffmanCodec.Block(literals, lengths, distances);

            // 2. Розпаковуємо Гафманом
            var huffmanTokens = _huffman.Decompress(block);

            // 3. Адаптуємо назад і декодуємо через LZ77
            var lz77Tokens = Lz77HuffmanAdapter.FromHuffman(huffmanTokens);
            return _lz77.Decode(lz77Tokens);
        }

        private CanonicalHuffmanCodec.Encoded ReadEncoded(BinaryReader reader)
        {
            int headerLength = reader.ReadInt32();
            byte[] header = reader.ReadBytes(headerLength);
            int dataLength = reader.ReadInt32();
            byte[] data = reader.ReadBytes(dataLength);
            return new CanonicalHuffmanCodec.Encoded(data, header);
        }

        private void WriteEncoded(BinaryWriter writer, CanonicalHuffmanCodec.Encoded encoded)
        {
            writer.Write(encoded.Header.Length);
            writer.Write(encoded.Header);
            writer.Write(encoded.Data.Length);
            writer.Write(encoded.Data);
        }
    }
}