using System.Security.Cryptography;
using SmartArchiver.Core.Interfaces;

namespace SmartArchiver.Core.Services.Crypto;

/// <summary>HMAC-SHA256 від (заголовок + шифротекст).</summary>
public class HmacAuthenticator : IMessageAuthenticator
{
    public byte[] Compute(byte[] key, ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertext)
    {
        ArgumentNullException.ThrowIfNull(key);

        using IncrementalHash hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
        hmac.AppendData(header);
        hmac.AppendData(ciphertext);
        return hmac.GetHashAndReset();
    }

    public bool Verify(byte[] key, ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> expectedMac)
    {
        byte[] actual = Compute(key, header, ciphertext);

        // Звичайне порівняння зупиняється на першому різному байті і так витікає інформація.
        // FixedTimeEquals працює однаковий час.
        return CryptographicOperations.FixedTimeEquals(actual, expectedMac);
    }
}
