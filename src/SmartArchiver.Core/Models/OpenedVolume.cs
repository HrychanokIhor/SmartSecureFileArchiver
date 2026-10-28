namespace SmartArchiver.Core.Models;

/// <summary>
/// Результат успішного відкриття тому: заголовок (щоб інтеграція знала номер тому
/// й загальну кількість) та розшифрований payload.
/// </summary>
public record OpenedVolume(VolumeHeader Header, byte[] Payload);
