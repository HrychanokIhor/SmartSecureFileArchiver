using System.Security.Cryptography;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Models;

namespace SmartArchiver.Core.Services.Crypto;

/// <summary>
/// PBKDF2-HMAC-SHA256, 100 000 ітерацій. Виводить 64 байти і ділить навпіл:
/// перші 32 - ключ шифрування, останні 32 - ключ HMAC.
/// </summary>
public class Pbkdf2KeyDeriver : IKeyDeriver
{
    public ArchiveKeys Derive(string password, byte[] salt)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentNullException.ThrowIfNull(salt);

        byte[] material = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            CryptoConstants.Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            CryptoConstants.KeySize * 2);

        try
        {
            byte[] encKey = material[..CryptoConstants.KeySize];
            byte[] macKey = material[CryptoConstants.KeySize..];
            return new ArchiveKeys(encKey, macKey, salt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }
    }
}
