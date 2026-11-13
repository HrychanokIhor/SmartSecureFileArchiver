namespace SmartArchiver.Core.Exceptions;

/// <summary>
/// Причини, з яких том не вдалося прочитати. Коли з'явиться FORMAT_SPEC_P2.md,
/// кожне значення треба зіставити з його кодом помилки (E_PAYLOAD тощо).
/// </summary>
public enum ArchiveErrorCode
{
    /// <summary>Заголовок не відповідає формату (magic, номери томів, зайві байти).</summary>
    InvalidFormat,

    /// <summary>Версія формату не підтримується.</summary>
    UnsupportedVersion,

    /// <summary>Файл коротший, ніж заявлено в заголовку (обрізаний архів).</summary>
    Truncated,

    /// <summary>HMAC не збігся: пароль невірний або дані змінено.</summary>
    AuthenticationFailed,

    /// <summary>Том належить іншому архіву (інша сіль), ніж ключі, якими його відкривають.</summary>
    VolumeMismatch
}
