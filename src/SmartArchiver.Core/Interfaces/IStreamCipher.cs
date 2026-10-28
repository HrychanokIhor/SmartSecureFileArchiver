// crypto


namespace SmartArchiver.Core.Interfaces;

/// <summary>
/// Потоковий шифр: гама, накладена на дані через XOR.
/// Шифрування і розшифрування - це одна й та сама операція.
/// </summary>
public interface IStreamCipher
{
    byte[] Transform(byte[] key, byte[] nonce, ReadOnlySpan<byte> data);
}
