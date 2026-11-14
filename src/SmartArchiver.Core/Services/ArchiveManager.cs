using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Services;

namespace SmartArchiver.Core.Integration;

public class ArchiveManager
{
    private readonly ICryptoService _cryptoService;
    private readonly SingleFilePayloadCodec _payload;

    public ArchiveManager(ArchiverController archiverController, ICryptoService cryptoService)
    {
        ArgumentNullException.ThrowIfNull(archiverController);
        _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
        _payload = new SingleFilePayloadCodec(archiverController);
    }

    public void PackFile(string sourceFilePath, string archiveFilePath, string password)
    {
        byte[] payload = _payload.Encode(Path.GetFileName(sourceFilePath), File.ReadAllBytes(sourceFilePath));
        using var keys = _cryptoService.CreateKeys(password);
        File.WriteAllBytes(archiveFilePath, _cryptoService.SealVolume(keys, 0, 1, payload));
    }

    public void UnpackFile(string archiveFilePath, string outputFilePath, string password)
    {
        byte[] volume = File.ReadAllBytes(archiveFilePath);
        var header = _cryptoService.ReadHeader(volume);
        using var keys = _cryptoService.DeriveKeys(password, header.Salt);
        var opened = _cryptoService.OpenVolume(keys, volume);
        if (opened.Header.VolumeCount != 1)
            throw new ArchiveException(ArchiveErrorCode.E_VOLUMES, "This command requires a single-volume archive.");

        byte[] original = _payload.Decode(opened.Payload);
        File.WriteAllBytes(outputFilePath, original);
    }
}
