using Xunit;
using SmartSecureFileArchiver.Core.Services;
using SmartSecureFileArchiver.Core.Algorithms;
using SmartSecureFileArchiver.Core.Models;

namespace SmartArchiver.Tests
{
    public class FileAnalyzerTests
    {
        [Fact]
        public void FeatureExtractor_ShouldReturnValidFeatureVector()
        {
            // Arrange
            var extractor = new FeatureExtractor();
            byte[] testData = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0x41, 0x42, 0x43 }; // довільні байти

            // Act
            FileFeatures features = extractor.Extract(testData);
            double[] vector = features.ToVector();

            // Assert
            Assert.NotNull(features);
            Assert.NotNull(vector);
            Assert.Equal(52, vector.Length); // перевіряємо, що розмірність вектора рівно 52
        }

        [Fact]
        public void FileAnalyzer_ShouldAnalyzeAndReturnCompressionConfig()
        {
            // Arrange
            var analyzer = new FileAnalyzer();
            byte[] testData = System.Text.Encoding.UTF8.GetBytes("Hello, Smart Secure File Archiver test data!");

            // Act
            CompressionConfig config = analyzer.Analyze(testData);

            // Assert
            Assert.NotNull(config);
        }
    }
}