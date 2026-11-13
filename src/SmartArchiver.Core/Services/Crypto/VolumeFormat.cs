namespace SmartArchiver.Core.Services.Crypto;

/// <summary>
/// Розкладка тому на диску:
/// [заголовок 41 байт][шифротекст][HMAC 32 байти].
///
/// УВАГА: розподіл 41 байта по полях - припущення (4+1+2+2+16+12+4).
/// Коли з'явиться FORMAT_SPEC_P2.md, зміни тільки цей файл.
/// Числа записуються у big-endian.
/// </summary>
public static class VolumeFormat
{
    public static readonly byte[] Magic = { (byte)'S', (byte)'A', (byte)'R', (byte)'C' };

    public const byte CurrentVersion = 1;

    // Зміщення полів усередині заголовка.
    public const int MagicOffset = 0;
    public const int VersionOffset = 4;
    public const int VolumeIndexOffset = 5;       // uint16
    public const int VolumeCountOffset = 7;       // uint16
    public const int SaltOffset = 9;              // 16 байтів
    public const int NonceOffset = 25;            // 12 байтів
    public const int CiphertextLengthOffset = 37; // uint32

    /// <summary>Розмір відкритого заголовка.</summary>
    public const int HeaderSize = 41;

    /// <summary>
    /// Службові витрати тому понад шифротекст (заголовок + HMAC).
    /// Потрібно пакувальнику томів при розрахунку місткості.
    /// </summary>
    public const int Overhead = HeaderSize + CryptoConstants.HmacSize;

    /// <summary>Максимальна кількість томів (поле uint16).</summary>
    public const int MaxVolumeCount = ushort.MaxValue;
}
