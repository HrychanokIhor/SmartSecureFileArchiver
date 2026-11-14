using System;
using System.Collections.Generic;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Services.Huffman;

namespace SmartArchiver.Core.Services
{
    // Додаємо перерахування для вибору режиму
    public enum CompressionMode
    {
        Greedy,
        Lazy
    }

    public class SarcPipeline
    {
        private readonly Lz77Algorithm _lz77 = new();
        // Використовуємо новий кодек команди замість старого Lz77HuffmanCodec
        private readonly SarcBlockCodec _codec = new();

        public byte[] Compress(byte[] input, CompressionMode mode = CompressionMode.Lazy)
        {
            // Вибір режиму змінює лише виклик кодування LZ77
            var lzTokens = mode == CompressionMode.Lazy 
                ? _lz77.EncodeLazy(input) 
                : _lz77.Encode(input);
                
            var huffTokens = Lz77HuffmanAdapter.ToHuffman(lzTokens);

            // Запис блоку SARC v1 тепер делеговано новому кодеку
            return _codec.EncodeBlock(huffTokens);
        }

        // Збережено декодування з expectedRawLength за вимогою тімліда
        public byte[] Decompress(byte[] compressedData, int expectedRawLength)
        {
            // Читання еталонного блоку через новий кодек
            var huffTokens = _codec.DecodeBlock(compressedData, expectedRawLength);

            var lz77Tokens = Lz77HuffmanAdapter.FromHuffman(huffTokens);
            return _lz77.Decode(lz77Tokens);
        }
    }
}