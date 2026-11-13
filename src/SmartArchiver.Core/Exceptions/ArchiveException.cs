namespace SmartArchiver.Core.Exceptions;

/// <summary>
/// Контрольована помилка роботи з архівом. Кидається замість "сирих" винятків
/// (IndexOutOfRange тощо), щоб CLI міг показати зрозуміле повідомлення.
/// У повідомленнях ніколи не має бути пароля чи ключів.
/// </summary>
public class ArchiveException : Exception
{
    public ArchiveErrorCode Code { get; }

    public ArchiveException(ArchiveErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }
}
