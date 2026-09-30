namespace SmartArchiver.Core.Abstractions;

public record CryptoResult(
    byte[] EncryptedData,
    byte[] Salt,
    byte[] Iv,
    byte[] Hmac
);

public interface ICryptoService
{
    /// <summary>
    /// Зашифровує дані за допомогою пароля (PBKDF2 + AES-256-CTR + HMAC-SHA256).
    /// </summary>
    CryptoResult Encrypt(byte[] data, string password);

    /// <summary>
    /// Расшифровує дані. Викидає виключення, якщо HMAC не збігається (пароль невірний / дані пошкоджено).
    /// </summary>
    byte[] Decrypt(byte[] encryptedData, string password, byte[] salt, byte[] iv, byte[] hmac);
}