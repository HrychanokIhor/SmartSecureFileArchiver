namespace SmartArchiver.Core.Services.Crypto;

/// <summary>
/// CRC32 (поліном 0xEDB88320, як у zip/PNG). Потрібен інтеграції для контрольної суми
/// кожного файлу в таблиці файлів. Це діагностика, а не захист: від зловмисника
/// охороняє HMAC.
/// </summary>
public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return ~crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint value = i;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? (value >> 1) ^ 0xEDB88320 : value >> 1;
            }
            table[i] = value;
        }
        return table;
    }
}
