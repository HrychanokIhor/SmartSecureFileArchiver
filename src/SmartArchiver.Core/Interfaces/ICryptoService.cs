using SmartArchiver.Core.Models;

namespace SmartArchiver.Core.Interfaces;

/// <summary>
/// Захист архіву. Приймає payload (таблиця файлів + таблиці Гаффмана + стиснуті блоки,
/// вже зібрані в один масив байтів) і повертає готовий том для запису на диск, і навпаки.
///
/// Створення архіву:
///   using var keys = crypto.CreateKeys(password);                    // один раз
///   byte[] volume  = crypto.SealVolume(keys, index, count, payload); // для кожного тому
///
/// Читання архіву:
///   var header = crypto.ReadHeader(firstVolumeBytes);                // без пароля
///   using var keys = crypto.DeriveKeys(password, header.Salt);       // один раз
///   var opened = crypto.OpenVolume(keys, volumeBytes);               // для кожного тому
/// </summary>
public interface ICryptoService
{
    /// <summary>Новий архів: генерує випадкову сіль і виводить ключі з пароля.</summary>
    ArchiveKeys CreateKeys(string password);

    /// <summary>Існуючий архів: виводить ключі з пароля та солі, прочитаної із заголовка.</summary>
    ArchiveKeys DeriveKeys(string password, byte[] salt);

    /// <summary>Читає і перевіряє відкритий заголовок тому (пароль не потрібен).</summary>
    VolumeHeader ReadHeader(byte[] volume);

    /// <summary>
    /// Шифрує payload і пакує у том: [заголовок][шифротекст][HMAC].
    /// Для кожного виклику генерується новий nonce.
    /// </summary>
    byte[] SealVolume(ArchiveKeys keys, int volumeIndex, int volumeCount, byte[] payload);

    /// <summary>
    /// Перевіряє HMAC і лише після цього розшифровує.
    /// Кидає ArchiveException, якщо том пошкоджено, обрізано або пароль невірний.
    /// </summary>
    OpenedVolume OpenVolume(ArchiveKeys keys, byte[] volume);
}
