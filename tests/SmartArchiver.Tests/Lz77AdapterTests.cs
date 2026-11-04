using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Models;
using SmartArchiver.Core.Services;
using SmartArchiver.Core.Services.Huffman;

namespace SmartArchiver.Tests
{
    public class Lz77HuffmanAdapterTests
    {
        [Fact]
        public void Lz77_Adapter_RoundTrip_WorksCorrectly()
        {
            var algorithm = new Lz77Algorithm();
            var originalBytes = Encoding.UTF8.GetBytes("abcabcabcxyzxyz");

            var lz77Tokens = algorithm.Encode(originalBytes);
            var huffmanTokens = Lz77HuffmanAdapter.ToHuffman(lz77Tokens);
            var restoredLz77Tokens = Lz77HuffmanAdapter.FromHuffman(huffmanTokens);
            var decodedBytes = algorithm.Decode(restoredLz77Tokens);

            Assert.Equal(originalBytes, decodedBytes);
        }

        [Fact]
        public void Adapter_ConvertsTokensCorrectly()
        {
            var lz77Tokens = new List<ILz77Token>
            {
                new LiteralToken((byte)'A'),
                new MatchToken(distance: 5, length: 10)
            };

            var huffmanTokens = Lz77HuffmanAdapter.ToHuffman(lz77Tokens).ToList();

            Assert.Equal(2, huffmanTokens.Count);
    
            Assert.True(huffmanTokens[0].Literal.HasValue);
            Assert.Equal((byte)'A', huffmanTokens[0].Literal.Value);
    

            Assert.False(huffmanTokens[1].Literal.HasValue);
            Assert.Equal(10, huffmanTokens[1].Length);
            Assert.Equal(5, huffmanTokens[1].Distance);
        }
    }
}