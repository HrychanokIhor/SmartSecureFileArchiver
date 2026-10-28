using System.Security.Cryptography;
using SmartArchiver.Core.Services.Crypto;

namespace SmartArchiver.Core.Models;

/// <summary>
/// Ключі одного архіву. Рахуються один раз із пароля та солі й використовуються
/// для всіх томів. Після роботи викликай Dispose, щоб занулити ключі в пам'яті.
/// </summary>
public sealed class ArchiveKeys : IDisposable
{
    /// <summary>Ключ шифрування (AES-256), 32 байти.</summary>
    public byte[] EncKey { get; }

    /// <summary>Ключ автентифікації (HMAC-SHA256), 32 байти. Інший, ніж EncKey.</summary>
    public byte[] MacKey { get; }

    /// <summary>Сіль KDF, 16 байтів. Не секрет: записується у заголовок кожного тому.</summary>
    public byte[] Salt { get; }

    public ArchiveKeys(byte[] encKey, byte[] macKey, byte[] salt)
    {
        ArgumentNullException.ThrowIfNull(encKey);
        ArgumentNullException.ThrowIfNull(macKey);
        ArgumentNullException.ThrowIfNull(salt);

        if (encKey.Length != CryptoConstants.KeySize)
            throw new ArgumentException($"EncKey must be {CryptoConstants.KeySize} bytes.", nameof(encKey));
        if (macKey.Length != CryptoConstants.KeySize)
            throw new ArgumentException($"MacKey must be {CryptoConstants.KeySize} bytes.", nameof(macKey));
        if (salt.Length != CryptoConstants.SaltSize)
            throw new ArgumentException($"Salt must be {CryptoConstants.SaltSize} bytes.", nameof(salt));

        EncKey = encKey;
        MacKey = macKey;
        Salt = (byte[])salt.Clone();
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(EncKey);
        CryptographicOperations.ZeroMemory(MacKey);
    }
}
