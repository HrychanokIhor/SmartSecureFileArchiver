using System;
using System.Collections.Generic;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Services.Huffman;

namespace SmartArchiver.Core.Services
{
    public enum CompressionMode
    {
        Greedy,
        Lazy
    }

    public class SarcPipeline
    {
        private readonly Lz77Algorithm _lz77;

        public SarcPipeline() : this(new Lz77Algorithm()) { }

        public SarcPipeline(Lz77Algorithm lz77) => _lz77 = lz77 ?? throw new ArgumentNullException(nameof(lz77));
        private readonly SarcBlockCodec _codec = new();

        public byte[] Compress(byte[] input, CompressionMode mode = CompressionMode.Lazy)
        {
            var lzTokens = mode switch
            {
                CompressionMode.Lazy => _lz77.EncodeLazy(input),
                CompressionMode.Greedy => _lz77.Encode(input),
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };

            var huffTokens = Lz77HuffmanAdapter.ToHuffman(lzTokens);

            // Викликаємо точну назву методу Ігоря: Encode
            return _codec.Encode(huffTokens);
        }

        public byte[] Decompress(byte[] compressedData, int expectedRawLength)
        {
            // Викликаємо точну назву методу Ігоря: Decode
            var huffTokens = _codec.Decode(compressedData, expectedRawLength);

            var lz77Tokens = Lz77HuffmanAdapter.FromHuffman(huffTokens);
            return _lz77.Decode(lz77Tokens);
        }
    }
}
