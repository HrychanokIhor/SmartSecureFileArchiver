using SmartArchiver.Core.Services.Crypto;

namespace SmartArchiver.Tests.Crypto;

public class AesCtrCipherTests
{
    private readonly AesCtrCipher _cipher = new();

    [Fact]
    public void Transform_NistVector_MatchesExpectedCiphertext()
    {
        // NIST SP 800-38A, F.5.5 (CTR-AES256.Encrypt).
        // Початковий лічильник f0f1...fcfdfeff розкладається на nonce (12 байтів) + номер блоку.
        byte[] key = Convert.FromHexString("603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4");
        byte[] nonce = Convert.FromHexString("f0f1f2f3f4f5f6f7f8f9fafb");
        byte[] plaintext = Convert.FromHexString(
            "6bc1bee22e409f96e93d7e117393172a" +
            "ae2d8a571e03ac9c9eb76fac45af8e51");
        byte[] expected = Convert.FromHexString(
            "601ec313775789a5b7a7f504bbf3d228" +
            "f443e3ca4d62b59aca84e990cacaf5c5");

        byte[] actual = _cipher.Transform(key, nonce, plaintext, initialCounter: 0xFCFDFEFF);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(16 * 1024)]       // рівно одна порція
    [InlineData(16 * 1024 + 1)]   // порція + 1 байт
    [InlineData(100_000)]         // кілька порцій
    public void Transform_Twice_ReturnsOriginal(int length)
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] data = new byte[length];
        new Random(42).NextBytes(key);
        new Random(43).NextBytes(nonce);
        new Random(44).NextBytes(data);

        byte[] encrypted = _cipher.Transform(key, nonce, data);
        byte[] decrypted = _cipher.Transform(key, nonce, encrypted);

        Assert.Equal(data, decrypted);
        if (length > 0) Assert.NotEqual(data, encrypted);
    }

    [Fact]
    public void Transform_DifferentNonce_GivesDifferentCiphertext()
    {
        byte[] key = new byte[32];
        byte[] data = new byte[64];

        byte[] a = _cipher.Transform(key, new byte[12], data);
        byte[] b = _cipher.Transform(key, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, data);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Transform_WrongKeyLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => _cipher.Transform(new byte[16], new byte[12], new byte[4]));
    }

    [Fact]
    public void Transform_WrongNonceLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => _cipher.Transform(new byte[32], new byte[8], new byte[4]));
    }
}
