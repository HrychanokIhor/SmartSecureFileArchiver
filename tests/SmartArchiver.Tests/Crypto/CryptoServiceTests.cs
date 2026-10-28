using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Models;
using SmartArchiver.Core.Services.Crypto;

namespace SmartArchiver.Tests.Crypto;

public class CryptoServiceTests
{
    private const string Password = "correct horse battery staple";

    private readonly CryptoService _crypto = CryptoService.CreateDefault();

    private static byte[] RandomBytes(int length, int seed = 1)
    {
        byte[] data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    // ---------- Ключі ----------

    [Fact]
    public void CreateKeys_EncKeyAndMacKeyAreDifferent()
    {
        using ArchiveKeys keys = _crypto.CreateKeys(Password);

        Assert.Equal(32, keys.EncKey.Length);
        Assert.Equal(32, keys.MacKey.Length);
        Assert.NotEqual(keys.EncKey, keys.MacKey);
    }

    [Fact]
    public void CreateKeys_TwoArchives_GetDifferentSalts()
    {
        using ArchiveKeys a = _crypto.CreateKeys(Password);
        using ArchiveKeys b = _crypto.CreateKeys(Password);

        Assert.NotEqual(a.Salt, b.Salt);
        Assert.NotEqual(a.EncKey, b.EncKey);
    }

    [Fact]
    public void DeriveKeys_SamePasswordAndSalt_GivesSameKeys()
    {
        using ArchiveKeys a = _crypto.CreateKeys(Password);
        using ArchiveKeys b = _crypto.DeriveKeys(Password, a.Salt);

        Assert.Equal(a.EncKey, b.EncKey);
        Assert.Equal(a.MacKey, b.MacKey);
    }

    [Fact]
    public void CreateKeys_EmptyPassword_Throws()
    {
        Assert.Throws<ArgumentException>(() => _crypto.CreateKeys(""));
    }

    // ---------- Круговий шлях ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(100_000)]
    public void SealThenOpen_ReturnsOriginalPayload(int length)
    {
        byte[] payload = RandomBytes(length);
        using ArchiveKeys keys = _crypto.CreateKeys(Password);

        byte[] volume = _crypto.SealVolume(keys, 0, 1, payload);
        OpenedVolume opened = _crypto.OpenVolume(keys, volume);

        Assert.Equal(payload, opened.Payload);
    }

    [Fact]
    public void SealVolume_HasExpectedLayoutAndSize()
    {
        byte[] payload = RandomBytes(500);
        using ArchiveKeys keys = _crypto.CreateKeys(Password);

        byte[] volume = _crypto.SealVolume(keys, 0, 1, payload);

        Assert.Equal(VolumeFormat.Overhead + payload.Length, volume.Length);
        Assert.Equal(VolumeFormat.Magic, volume[..4]);
    }

    [Fact]
    public void SealVolume_DoesNotLeakPayloadInPlaintext()
    {
        byte[] payload = new byte[256];   // нулі: у відкритому вигляді було б 256 нулів поспіль
        using ArchiveKeys keys = _crypto.CreateKeys(Password);

        byte[] volume = _crypto.SealVolume(keys, 0, 1, payload);
        byte[] ciphertext = volume[VolumeFormat.HeaderSize..^CryptoConstants.HmacSize];

        Assert.NotEqual(payload, ciphertext);
    }

    [Fact]
    public void SealVolume_SamePayloadTwice_GivesDifferentVolumes()
    {
        byte[] payload = RandomBytes(200);
        using ArchiveKeys keys = _crypto.CreateKeys(Password);

        byte[] first = _crypto.SealVolume(keys, 0, 2, payload);
        byte[] second = _crypto.SealVolume(keys, 1, 2, payload);

        // Різні nonce => різні шифротексти.
        Assert.NotEqual(_crypto.ReadHeader(first).Nonce, _crypto.ReadHeader(second).Nonce);
        Assert.NotEqual(first[VolumeFormat.HeaderSize..], second[VolumeFormat.HeaderSize..]);
    }

    // ---------- Відкриття у "новому процесі": ключі виводяться із заголовка ----------

    [Fact]
    public void OpenWithKeysDerivedFromHeaderSalt_Works()
    {
        byte[] payload = RandomBytes(300);
        byte[] volume;
        using (ArchiveKeys keys = _crypto.CreateKeys(Password))
        {
            volume = _crypto.SealVolume(keys, 0, 1, payload);
        }

        VolumeHeader header = _crypto.ReadHeader(volume);
        using ArchiveKeys readerKeys = _crypto.DeriveKeys(Password, header.Salt);
        OpenedVolume opened = _crypto.OpenVolume(readerKeys, volume);

        Assert.Equal(payload, opened.Payload);
    }

    [Fact]
    public void OpenVolume_ReturnsVolumeNumbering()
    {
        using ArchiveKeys keys = _crypto.CreateKeys(Password);
        byte[] volume = _crypto.SealVolume(keys, 2, 5, RandomBytes(10));

        OpenedVolume opened = _crypto.OpenVolume(keys, volume);

        Assert.Equal(2, opened.Header.VolumeIndex);
        Assert.Equal(5, opened.Header.VolumeCount);
    }

    // ---------- Відмови ----------

    [Fact]
    public void OpenVolume_WrongPassword_Throws()
    {
        using ArchiveKeys keys = _crypto.CreateKeys(Password);
        byte[] volume = _crypto.SealVolume(keys, 0, 1, RandomBytes(100));

        using ArchiveKeys wrong = _crypto.DeriveKeys("wrong password", keys.Salt);
        var ex = Assert.Throws<ArchiveException>(() => _crypto.OpenVolume(wrong, volume));

        Assert.Equal(ArchiveErrorCode.AuthenticationFailed, ex.Code);
    }

    [Fact]
    public void OpenVolume_AnySingleByteChanged_IsRejected()
    {
        byte[] payload = RandomBytes(64);
        using ArchiveKeys keys = _crypto.CreateKeys(Password);
        byte[] volume = _crypto.SealVolume(keys, 0, 1, payload);

        // Міняємо по одному байту у кожній позиції тому: заголовок, шифротекст, HMAC.
        for (int i = 0; i < volume.Length; i++)
        {
            byte[] tampered = (byte[])volume.Clone();
            tampered[i] ^= 0x01;

            Assert.Throws<ArchiveException>(() => _crypto.OpenVolume(keys, tampered));
        }
    }

    [Fact]
    public void OpenVolume_ChangedCiphertextByte_FailsOnHmac()
    {
        using ArchiveKeys keys = _crypto.CreateKeys(Password);
        byte[] volume = _crypto.SealVolume(keys, 0, 1, RandomBytes(64));
        volume[VolumeFormat.HeaderSize + 10] ^= 0xFF;

        var ex = Assert.Throws<ArchiveException>(() => _crypto.OpenVolume(keys, volume));

        Assert.Equal(ArchiveErrorCode.AuthenticationFailed, ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(VolumeFormat.HeaderSize)]
    [InlineData(VolumeFormat.HeaderSize + 20)]
    public void OpenVolume_TruncatedVolume_ThrowsArchiveException(int keepBytes)
    {
        using ArchiveKeys keys = _crypto.CreateKeys(Password);
        byte[] volume = _crypto.SealVolume(keys, 0, 1, RandomBytes(100));
        byte[] truncated = volume[..keepBytes];

        var ex = Assert.Throws<ArchiveException>(() => _crypto.OpenVolume(keys, truncated));

        Assert.Equal(ArchiveErrorCode.Truncated, ex.Code);
    }

    [Fact]
    public void OpenVolume_TrailingGarbage_IsRejected()
    {
        using ArchiveKeys keys = _crypto.CreateKeys(Password);
        byte[] volume = _crypto.SealVolume(keys, 0, 1, RandomBytes(100));
        byte[] extended = volume.Concat(new byte[] { 0 }).ToArray();

        Assert.Throws<ArchiveException>(() => _crypto.OpenVolume(keys, extended));
    }

    [Fact]
    public void OpenVolume_BadMagic_Throws()
    {
        using ArchiveKeys keys = _crypto.CreateKeys(Password);
        byte[] volume = _crypto.SealVolume(keys, 0, 1, RandomBytes(100));
        volume[0] = (byte)'X';

        var ex = Assert.Throws<ArchiveException>(() => _crypto.OpenVolume(keys, volume));

        Assert.Equal(ArchiveErrorCode.InvalidFormat, ex.Code);
    }

    [Fact]
    public void OpenVolume_VolumeFromAnotherArchive_Throws()
    {
        using ArchiveKeys archiveA = _crypto.CreateKeys(Password);
        using ArchiveKeys archiveB = _crypto.CreateKeys(Password);
        byte[] volumeFromB = _crypto.SealVolume(archiveB, 0, 1, RandomBytes(100));

        var ex = Assert.Throws<ArchiveException>(() => _crypto.OpenVolume(archiveA, volumeFromB));

        Assert.Equal(ArchiveErrorCode.VolumeMismatch, ex.Code);
    }

    [Fact]
    public void ReadHeader_RandomGarbage_NeverThrowsUnexpectedException()
    {
        var random = new Random(7);
        for (int length = 0; length < 200; length++)
        {
            byte[] garbage = new byte[length];
            random.NextBytes(garbage);

            // Допустима лише контрольована помилка.
            try { _crypto.ReadHeader(garbage); }
            catch (ArchiveException) { }
        }
    }
}
