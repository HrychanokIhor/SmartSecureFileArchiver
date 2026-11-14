// crypto


using SmartArchiver.Core.Models;

namespace SmartArchiver.Core.Interfaces;

/// <summary>Серіалізація та перевірка 41-байтового заголовка тому.</summary>
public interface IVolumeHeaderCodec
{
    byte[] Write(VolumeHeader header);

    /// <summary>
    /// Читає заголовок із початку тому і перевіряє, що весь том має правильну довжину.
    /// Кидає ArchiveException, якщо формат порушено.
    /// </summary>
    VolumeHeader Read(byte[] volume);
}
