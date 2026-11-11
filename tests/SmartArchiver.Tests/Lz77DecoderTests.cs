using System.Collections.Generic;
using System.Text;
using Xunit;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Models;

namespace SmartArchiver.Tests
{
    public class Lz77DecoderTests
    {
        [Fact]
        public void Decode_SimpleString_ReturnsCorrectBytes()
        {
            var decoder = new Lz77Algorithm();
            var tokens = new List<ILz77Token>
            {
                new LiteralToken((byte)'a'),
                new LiteralToken((byte)'b'),
                new LiteralToken((byte)'c'),
                new MatchToken(distance: 3, length: 6)
            };

            var expectedBytes = Encoding.UTF8.GetBytes("abcabcabc");

            var result = decoder.Decode(tokens);

            Assert.Equal(expectedBytes, result);
        }

        [Fact]
        public void Decode_OverlappingMatch_CopiesByteByByte()
        {
            var decoder = new Lz77Algorithm();
            var tokens = new List<ILz77Token>
            {
                new LiteralToken((byte)'A'),
                new MatchToken(distance: 1, length: 11)
            };

            var expectedBytes = Encoding.UTF8.GetBytes("AAAAAAAAAAAA");
            var result = decoder.Decode(tokens);
            Assert.Equal(expectedBytes, result);
        }
    }
}