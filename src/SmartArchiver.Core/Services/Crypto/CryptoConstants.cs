namespace SmartArchiver.Core.Services.Crypto;

/// <summary>Фіксовані параметри криптографії (за методичкою, не предмет вибору).</summary>
public static class CryptoConstants
{
    /// <summary>Розмір солі KDF.</summary>
    public const int SaltSize = 16;

    /// <summary>Розмір nonce для AES-CTR.</summary>
    public const int NonceSize = 12;

    /// <summary>Розмір кожного з ключів (AES-256 і HMAC-SHA256).</summary>
    public const int KeySize = 32;

    /// <summary>Розмір HMAC-SHA256.</summary>
    public const int HmacSize = 32;

    /// <summary>Кількість ітерацій PBKDF2.</summary>
    public const int Pbkdf2Iterations = 100_000;
}
