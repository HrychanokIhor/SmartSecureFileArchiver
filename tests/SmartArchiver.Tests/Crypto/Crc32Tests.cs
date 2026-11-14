using System.Text;
using SmartArchiver.Core.Services.Crypto;

namespace SmartArchiver.Tests.Crypto;

public class Crc32Tests
{
    [Fact]
    public void Compute_KnownVector_ReturnsStandardCrc()
    {
        // Стандартний контрольний рядок для CRC32.
        uint crc = Crc32.Compute(Encoding.ASCII.GetBytes("123456789"));

        Assert.Equal(0xCBF43926u, crc);
    }

    [Fact]
    public void Compute_EmptyInput_ReturnsZero()
    {
        Assert.Equal(0u, Crc32.Compute(ReadOnlySpan<byte>.Empty));
    }
}
