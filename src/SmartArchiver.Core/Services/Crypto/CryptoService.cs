using System.Security.Cryptography;
using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Models;
 
namespace SmartArchiver.Core.Services.Crypto;
 
/// <summary>
/// Фасад захисту: схема Encrypt-then-MAC.
/// Сам нічого не рахує, а лише викликає спеціалізовані класи в правильному порядку.
/// </summary>
public class CryptoService : ICryptoService
{
    private readonly IKeyDeriver _keyDeriver;
    private readonly IStreamCipher _cipher;
    private readonly IMessageAuthenticator _authenticator;
    private readonly IVolumeHeaderCodec _headerCodec;
 
    public CryptoService(
        IKeyDeriver keyDeriver,
        IStreamCipher cipher,
        IMessageAuthenticator authenticator,
        IVolumeHeaderCodec headerCodec)
    {
        _keyDeriver = keyDeriver;
        _cipher = cipher;
        _authenticator = authenticator;
        _headerCodec = headerCodec;
    }
 
    /// <summary>Збирає сервіс зі стандартних реалізацій.</summary>
    public static CryptoService CreateDefault()
    {
        return new CryptoService(
            new Pbkdf2KeyDeriver(),
            new AesCtrCipher(),
            new HmacAuthenticator(),
            new VolumeHeaderCodec());
    }
 
    public ArchiveKeys CreateKeys(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(CryptoConstants.SaltSize);
        return _keyDeriver.Derive(password, salt);
    }
 
    public ArchiveKeys DeriveKeys(string password, byte[] salt)
    {
        return _keyDeriver.Derive(password, salt);
    }
 
    public VolumeHeader ReadHeader(byte[] volume)
    {
        return _headerCodec.Read(volume);
    }
 
    public byte[] SealVolume(ArchiveKeys keys, int volumeIndex, int volumeCount, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(payload);
 
        // 1. Отримуємо свіжий рандомний Nonce на 12 байт (лише для цього тому!)
        byte[] nonce = RandomNumberGenerator.GetBytes(CryptoConstants.NonceSize);
 
        // 2. Заголовок. CTR не додає padding, тому довжина шифротексту = довжині payload.
        var header = new VolumeHeader(
            VolumeFormat.CurrentVersion, volumeIndex, volumeCount, keys.Salt, nonce, payload.Length);
        byte[] headerBytes = _headerCodec.Write(header);
 
        // 3. Проганяємо payload через AES-CTR
        byte[] ciphertext = _cipher.Transform(keys.EncKey, nonce, payload);
 
        // 4. Підписуємо заголовок + шифротекст.
        byte[] mac = _authenticator.Compute(keys.MacKey, headerBytes, ciphertext);
 
        // 5. Збираємо том: [заголовок][шифротекст][HMAC].
        byte[] volume = new byte[VolumeFormat.Overhead + ciphertext.Length];
        headerBytes.CopyTo(volume, 0);
        ciphertext.CopyTo(volume, VolumeFormat.HeaderSize);
        mac.CopyTo(volume, VolumeFormat.HeaderSize + ciphertext.Length);
 
        return volume;
    }
 
    public OpenedVolume OpenVolume(ArchiveKeys keys, byte[] volume)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(volume);
 
        // 1. Структура: magic, версія, довжини. Обрізаний файл відсікається тут.
        VolumeHeader header = _headerCodec.Read(volume);
 
        // 2. Том з іншого архіву: інша сіль означає інші ключі, HMAC однаково не збіжиться.
        //    Окрема помилка зрозуміліша для користувача. Сіль не секрет, тому звичайне порівняння ок.
        if (!header.Salt.AsSpan().SequenceEqual(keys.Salt))
            throw new ArchiveException(ArchiveErrorCode.VolumeMismatch, "Volume belongs to a different archive.");
 
        ReadOnlySpan<byte> headerBytes = volume.AsSpan(0, VolumeFormat.HeaderSize);
        ReadOnlySpan<byte> ciphertext = volume.AsSpan(VolumeFormat.HeaderSize, header.CiphertextLength);
        ReadOnlySpan<byte> mac = volume.AsSpan(VolumeFormat.HeaderSize + header.CiphertextLength, CryptoConstants.HmacSize);
 
        // 3. Спочатку автентичність. Без цього розшифровувати не можна.
        if (!_authenticator.Verify(keys.MacKey, headerBytes, ciphertext, mac))
            throw new ArchiveException(ArchiveErrorCode.AuthenticationFailed, "Wrong password or the archive is corrupted.");
 
        // 4. Лише тепер розшифровуємо.
        byte[] payload = _cipher.Transform(keys.EncKey, header.Nonce, ciphertext);
 
        return new OpenedVolume(header, payload);
    }
}
