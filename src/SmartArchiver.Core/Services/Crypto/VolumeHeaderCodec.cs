using System.Buffers.Binary;
using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Models;

namespace SmartArchiver.Core.Services.Crypto;

/// <summary>
/// Записує і читає відкритий заголовок тому. Розкладку полів задає <see cref="VolumeFormat"/>.
/// Усі перевірки зроблено тут, щоб зіпсований файл давав ArchiveException,
/// а не IndexOutOfRangeException.
/// </summary>
public class VolumeHeaderCodec : IVolumeHeaderCodec
{
    public byte[] Write(VolumeHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);

        if (header.Salt.Length != CryptoConstants.SaltSize)
            throw new ArgumentException("Salt has wrong length.", nameof(header));
        if (header.Nonce.Length != CryptoConstants.NonceSize)
            throw new ArgumentException("Nonce has wrong length.", nameof(header));
        if (header.VolumeCount < 1 || header.VolumeCount > VolumeFormat.MaxVolumeCount)
            throw new ArgumentException("Volume count is out of range.", nameof(header));
        if (header.VolumeIndex < 0 || header.VolumeIndex >= header.VolumeCount)
            throw new ArgumentException("Volume index is out of range.", nameof(header));
        if (header.CiphertextLength < 0)
            throw new ArgumentException("Ciphertext length is negative.", nameof(header));

        byte[] bytes = new byte[VolumeFormat.HeaderSize];
        Span<byte> span = bytes;

        VolumeFormat.Magic.CopyTo(span[VolumeFormat.MagicOffset..]);
        span[VolumeFormat.VersionOffset] = header.Version;
        BinaryPrimitives.WriteUInt16BigEndian(span[VolumeFormat.VolumeIndexOffset..], (ushort)header.VolumeIndex);
        BinaryPrimitives.WriteUInt16BigEndian(span[VolumeFormat.VolumeCountOffset..], (ushort)header.VolumeCount);
        header.Salt.CopyTo(span[VolumeFormat.SaltOffset..]);
        header.Nonce.CopyTo(span[VolumeFormat.NonceOffset..]);
        BinaryPrimitives.WriteUInt32BigEndian(span[VolumeFormat.CiphertextLengthOffset..], (uint)header.CiphertextLength);

        return bytes;
    }

    public VolumeHeader Read(byte[] volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        if (volume.Length < VolumeFormat.HeaderSize)
            throw new ArchiveException(ArchiveErrorCode.Truncated, "Volume is too short to contain a header.");

        ReadOnlySpan<byte> span = volume;

        if (!span.Slice(VolumeFormat.MagicOffset, VolumeFormat.Magic.Length).SequenceEqual(VolumeFormat.Magic))
            throw new ArchiveException(ArchiveErrorCode.InvalidFormat, "Not a SmartArchiver volume (bad magic).");

        byte version = span[VolumeFormat.VersionOffset];
        if (version != VolumeFormat.CurrentVersion)
            throw new ArchiveException(ArchiveErrorCode.UnsupportedVersion, $"Unsupported format version: {version}.");

        int volumeIndex = BinaryPrimitives.ReadUInt16BigEndian(span[VolumeFormat.VolumeIndexOffset..]);
        int volumeCount = BinaryPrimitives.ReadUInt16BigEndian(span[VolumeFormat.VolumeCountOffset..]);
        if (volumeCount < 1 || volumeIndex >= volumeCount)
            throw new ArchiveException(ArchiveErrorCode.InvalidFormat, "Invalid volume numbering in header.");

        byte[] salt = span.Slice(VolumeFormat.SaltOffset, CryptoConstants.SaltSize).ToArray();
        byte[] nonce = span.Slice(VolumeFormat.NonceOffset, CryptoConstants.NonceSize).ToArray();

        uint ciphertextLength = BinaryPrimitives.ReadUInt32BigEndian(span[VolumeFormat.CiphertextLengthOffset..]);

        // ulong, щоб сума не переповнилась на підробленому заголовку.
        ulong expectedSize = (ulong)VolumeFormat.Overhead + ciphertextLength;

        if ((ulong)volume.Length < expectedSize)
            throw new ArchiveException(ArchiveErrorCode.Truncated, "Volume is truncated.");
        if ((ulong)volume.Length > expectedSize)
            throw new ArchiveException(ArchiveErrorCode.InvalidFormat, "Volume has unexpected extra data.");

        return new VolumeHeader(version, volumeIndex, volumeCount, salt, nonce, (int)ciphertextLength);
    }
}
