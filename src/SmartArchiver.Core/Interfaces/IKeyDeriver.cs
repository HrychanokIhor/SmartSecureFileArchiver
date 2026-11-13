// crypto


using SmartArchiver.Core.Models;

namespace SmartArchiver.Core.Interfaces;

/// <summary>Перетворює пароль і сіль на ключі архіву.</summary>
public interface IKeyDeriver
{
    /// <summary>Повільна операція (PBKDF2). Викликається один раз на архів.</summary>
    ArchiveKeys Derive(string password, byte[] salt);
}
