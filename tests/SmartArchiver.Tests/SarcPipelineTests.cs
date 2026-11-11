using System.Text;
using Xunit;
using SmartArchiver.Core.Services;

namespace SmartArchiver.Tests
{
    public class SarcPipelineTests
    {
        [Fact]
        public void SarcPipeline_FullCycle_ReturnsOriginalData()
        {
            var pipeline = new SarcPipeline();
            byte[] original = Encoding.UTF8.GetBytes("Тестування повного циклу LZ77 та Huffman! Тестування повного циклу!");

            byte[] compressed = pipeline.Compress(original);
            byte[] decompressed = pipeline.Decompress(compressed);

            Assert.Equal(original, decompressed);
        }
    }
}