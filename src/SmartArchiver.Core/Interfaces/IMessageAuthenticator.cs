// crypto


namespace SmartArchiver.Core.Interfaces;

/// <summary>Код автентичності повідомлення (HMAC).</summary>
public interface IMessageAuthenticator
{
    /// <summary>Рахує код від заголовка та шифротексту.</summary>
    byte[] Compute(byte[] key, ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertext);

    /// <summary>Перевіряє код порівнянням постійного часу.</summary>
    bool Verify(byte[] key, ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> expectedMac);
}
