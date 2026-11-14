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
        private readonly Lz77Algorithm _lz77 = new();
        private readonly SarcBlockCodec _codec = new();

        public byte[] Compress(byte[] input, CompressionMode mode = CompressionMode.Lazy)
        {
            var lzTokens = mode == CompressionMode.Lazy 
                ? _lz77.EncodeLazy(input) 
                : _lz77.Encode(input);
                
            var huffTokens = Lz77HuffmanAdapter.ToHuffman(lzTokens);
            return _codec.EncodeBlock(huffTokens);
        }

        public byte[] Decompress(byte[] compressedData, int expectedRawLength)
        {
            var huffTokens = _codec.DecodeBlock(compressedData, expectedRawLength);
            var lz77Tokens = Lz77HuffmanAdapter.FromHuffman(huffTokens);
            return _lz77.Decode(lz77Tokens);
        }
    }
}