using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Models;
using SmartArchiver.Core.Services.Crypto;

namespace SmartArchiver.Tests.Crypto;

public class VolumeHeaderCodecTests
{
    private readonly VolumeHeaderCodec _codec = new();

    private static VolumeHeader SampleHeader(int ciphertextLength = 10) => new(
        VolumeFormat.CurrentVersion,
        VolumeIndex: 1,
        VolumeCount: 3,
        Salt: Enumerable.Range(1, 16).Select(i => (byte)i).ToArray(),
        Nonce: Enumerable.Range(100, 12).Select(i => (byte)i).ToArray(),
        CiphertextLength: ciphertextLength);

    /// <summary>Заголовок + фіктивний шифротекст + фіктивний HMAC правильної довжини.</summary>
    private byte[] BuildVolume(VolumeHeader header)
    {
        byte[] headerBytes = _codec.Write(header);
        return headerBytes
            .Concat(new byte[header.CiphertextLength])
            .Concat(new byte[CryptoConstants.HmacSize])
            .ToArray();
    }

    [Fact]
    public void Write_Produces41Bytes()
    {
        Assert.Equal(41, _codec.Write(SampleHeader()).Length);
    }

    [Fact]
    public void WriteThenRead_ReturnsSameFields()
    {
        VolumeHeader original = SampleHeader(ciphertextLength: 25);

        VolumeHeader read = _codec.Read(BuildVolume(original));

        Assert.Equal(original.Version, read.Version);
        Assert.Equal(original.VolumeIndex, read.VolumeIndex);
        Assert.Equal(original.VolumeCount, read.VolumeCount);
        Assert.Equal(original.Salt, read.Salt);
        Assert.Equal(original.Nonce, read.Nonce);
        Assert.Equal(original.CiphertextLength, read.CiphertextLength);
    }

    [Fact]
    public void Read_ForgedHugeLength_ThrowsTruncatedInsteadOfCrashing()
    {
        byte[] volume = BuildVolume(SampleHeader());
        // Підробляємо ciphertext_len = 0xFFFFFFFF.
        for (int i = 0; i < 4; i++) volume[VolumeFormat.CiphertextLengthOffset + i] = 0xFF;

        var ex = Assert.Throws<ArchiveException>(() => _codec.Read(volume));

        Assert.Equal(ArchiveErrorCode.Truncated, ex.Code);
    }

    [Fact]
    public void Read_UnknownVersion_Throws()
    {
        byte[] volume = BuildVolume(SampleHeader());
        volume[VolumeFormat.VersionOffset] = 99;

        var ex = Assert.Throws<ArchiveException>(() => _codec.Read(volume));

        Assert.Equal(ArchiveErrorCode.UnsupportedVersion, ex.Code);
    }

    [Fact]
    public void Read_IndexNotLessThanCount_Throws()
    {
        VolumeHeader header = SampleHeader();
        byte[] volume = BuildVolume(header);
        volume[VolumeFormat.VolumeIndexOffset + 1] = 3;   // index = 3 при count = 3

        var ex = Assert.Throws<ArchiveException>(() => _codec.Read(volume));

        Assert.Equal(ArchiveErrorCode.InvalidFormat, ex.Code);
    }
}
