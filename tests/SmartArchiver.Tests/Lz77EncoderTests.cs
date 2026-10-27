using System.Collections.Generic;
using System.Text;
using Xunit;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Models;

namespace SmartArchiver.Tests
{
    public class Lz77EncoderTests
    {
        [Fact]
        public void Encode_Decode_RoundTrip_ReturnsOriginalData()
        {
            var algorithm = new Lz77Algorithm();
            var originalString = "abcabcabcabcxyzxyz";
            var originalBytes = Encoding.UTF8.GetBytes(originalString);

            var tokens = algorithm.Encode(originalBytes);
            var decodedBytes = algorithm.Decode(tokens);

            Assert.Equal(originalBytes, decodedBytes);
        }

        [Fact]
        public void Encode_FindsCorrectMatch()
        {
            var algorithm = new Lz77Algorithm();
            var originalBytes = Encoding.UTF8.GetBytes("abcabcabc");

            var tokens = algorithm.Encode(originalBytes);
            var tokenList = new List<ILz77Token>(tokens);

            Assert.True(tokenList.Count < originalBytes.Length);
            Assert.IsType<LiteralToken>(tokenList[0]);
            Assert.IsType<LiteralToken>(tokenList[1]);
            Assert.IsType<LiteralToken>(tokenList[2]);
            Assert.IsType<MatchToken>(tokenList[3]);
            
            var match = (MatchToken)tokenList[3];
            Assert.Equal(3, match.Distance);
            Assert.Equal(6, match.Length);
        }
    }
}