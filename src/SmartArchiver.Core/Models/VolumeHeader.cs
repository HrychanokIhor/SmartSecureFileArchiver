namespace SmartArchiver.Core.Models;

/// <summary>
/// Відкритий заголовок тому (41 байт на диску). Читається без пароля.
/// </summary>
/// <param name="Version">Версія формату.</param>
/// <param name="VolumeIndex">Номер тому, з нуля.</param>
/// <param name="VolumeCount">Скільки всього томів в архіві.</param>
/// <param name="Salt">Сіль KDF (16 байтів).</param>
/// <param name="Nonce">Nonce для AES-CTR (12 байтів), унікальний для кожного тому.</param>
/// <param name="CiphertextLength">Довжина шифротексту, байтів.</param>
public record VolumeHeader(
    byte Version,
    int VolumeIndex,
    int VolumeCount,
    byte[] Salt,
    byte[] Nonce,
    int CiphertextLength
);
