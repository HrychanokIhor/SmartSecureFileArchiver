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
        [Fact]
        public void Decompress_WithCorruptedLength_ThrowsInvalidDataException()
        {
            // Arrange
            var pipeline = new SarcPipeline();
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
    
            // Пишемо фейковий блок: довжина заголовка 9999999 (завелика), але реальних даних немає
            writer.Write(9999999); 
            writer.Write(new byte[10]); 
    
            byte[] corruptedData = ms.ToArray();

            // Act & Assert
            Assert.Throws<System.IO.InvalidDataException>(() => pipeline.Decompress(corruptedData));
        }
    }
}