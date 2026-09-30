namespace SmartArchiver.Core.Abstractions;

public record CompressionConfig(bool ShouldCompress, int RecommendedWindowSize);

public interface IFileAnalyzer
{
    CompressionConfig Analyze(byte[] fileData);
}