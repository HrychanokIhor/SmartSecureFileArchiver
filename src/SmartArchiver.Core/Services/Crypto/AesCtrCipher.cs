using System.Buffers.Binary;
using System.Security.Cryptography;
using SmartArchiver.Core.Interfaces;

namespace SmartArchiver.Core.Services.Crypto;

/// <summary>
/// AES-256 у режимі CTR, реалізований вручну (у .NET цього режиму немає).
///
/// Блок лічильника: nonce (12 байтів) + номер блоку (uint32, big-endian), нумерація з 1.
/// Для кожного блоку: гама = AES(ключ, блок лічильника); результат = дані XOR гама.
/// Шифрування і розшифрування збігаються.
/// </summary>
public class AesCtrCipher : IStreamCipher

// коротко по класу
// впринципі замість стандартної логіки ми aes робим на 16кб одним викликом
// під капотом аес точно так само як вчив (по 16 байт)
// ксор так само раз на 16 кб

// цикл 

{
    private const int BlockSize = 16;

    // Гаму рахуємо порціями по 1024 блоки (16 КБ), щоб не викликати AES на кожні 16 байтів.
    private const int BlocksPerChunk = 1024;

    public byte[] Transform(byte[] key, byte[] nonce, ReadOnlySpan<byte> data)
    {
        return Transform(key, nonce, data, initialCounter: 1);
    }

    /// <param name="initialCounter">
    /// Номер першого блоку. За форматом це 1; інше значення потрібне лише для тестів
    /// на еталонних векторах.
    /// </param>
    public byte[] Transform(byte[] key, byte[] nonce, ReadOnlySpan<byte> data, uint initialCounter)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(nonce);

        if (key.Length != CryptoConstants.KeySize)
            throw new ArgumentException($"Key must be {CryptoConstants.KeySize} bytes.", nameof(key));
        if (nonce.Length != CryptoConstants.NonceSize)
            throw new ArgumentException($"Nonce must be {CryptoConstants.NonceSize} bytes.", nameof(nonce));

        // Лічильник 32-бітний: не можна, щоб він переповнився і гама повторилась.
        long totalBlocks = ((long)data.Length + BlockSize - 1) / BlockSize;
        if (initialCounter + totalBlocks - 1 > uint.MaxValue)
            throw new ArgumentException("Data is too long for a 32-bit block counter.", nameof(data));

        byte[] result = new byte[data.Length];
        // Виділяємо буфер на 16 КБ (1024 блоки по 16 байт) для наших лічильників
        byte[] counterBlocks = new byte[BlocksPerChunk * BlockSize];

        using Aes aes = Aes.Create();
        aes.Key = key;

        uint counter = initialCounter;
        int offset = 0; // Вказує, на якому байті вхідного файлу ми зараз знаходимося

// цикл геть на всю data

        while (offset < data.Length)
        {
            // .Min(16 кб, data - скільки обробили )
            int chunkBytes = Math.Min(BlocksPerChunk * BlockSize, data.Length - offset);
            // Скільки 16-байтових блоків нам потрібно для цієї кількості байтів?
            int chunkBlocks = (chunkBytes + BlockSize - 1) / BlockSize;

            // 1. Будуємо блоки лічильника: nonce || номер блоку.
//              тобто цикл робить
// 12 nonce 4 couner 12 nonce 4 counter... і це просто одним масивом
            for (int i = 0; i < chunkBlocks; i++)
            {
                // Беремо "шматок" у 16 байтів з нашого буфера
                Span<byte> block = counterBlocks.AsSpan(i * BlockSize, BlockSize);
                nonce.CopyTo(block);
                BinaryPrimitives.WriteUInt32BigEndian(block[CryptoConstants.NonceSize..], counter);
                counter++;
            }

            // 2. Шифруємо лічильники AES-ом: отримуємо гаму. ==== keystream
            // ECB тут лише спосіб зашифрувати окремі 16-байтові блоки
            byte[] keystream = aes.EncryptEcb(
                counterBlocks.AsSpan(0, chunkBlocks * BlockSize),
                PaddingMode.None);

            // 3. XOR гами з даними.
            for (int i = 0; i < chunkBytes; i++)
            {
                result[offset + i] = (byte)(data[offset + i] ^ keystream[i]);
            }

            CryptographicOperations.ZeroMemory(keystream);
            offset += chunkBytes;
        }

        return result;
    }
}
