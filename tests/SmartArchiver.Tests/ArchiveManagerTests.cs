using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Integration;
using SmartArchiver.Core.Services;
using SmartArchiver.Core.Services.Crypto;

namespace SmartArchiver.Tests;

public class ArchiveManagerTests
{
    [Fact]
    public void PacksAndRestoresMultipleBlocksInOneVolume()
    {
        var directory = Directory.CreateTempSubdirectory("sarc-roundtrip-");
        try
        {
            string input = Path.Combine(directory.FullName, "source.txt");
            string archive = Path.Combine(directory.FullName, "archive.sarc");
            string output = Path.Combine(directory.FullName, "restored.txt");
            byte[] original = Enumerable.Repeat((byte)65, 20000).ToArray();
            File.WriteAllBytes(input, original);
            var crypto = CryptoService.CreateDefault();
            var manager = new ArchiveManager(new ArchiverController(), crypto);
            manager.PackFile(input, archive, "test-password");
            manager.UnpackFile(archive, output, "test-password");
            Assert.Equal(original, File.ReadAllBytes(output));

            File.Delete(output);
            Assert.Equal(ArchiveErrorCode.AuthenticationFailed,
                Assert.Throws<ArchiveException>(() => manager.UnpackFile(archive, output, "wrong-password")).Code);
            Assert.False(File.Exists(output));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void ReadsAuthenticatedReferenceLayout()
    {
        var directory = Directory.CreateTempSubdirectory("sarc-reference-");
        try
        {
            // Decrypted ref_01 payload from FORMAT_SPEC_P2 §8.
            byte[] payload = Convert.FromHexString(
                "00010000000968656C6C6F2E747874000001E07020884600010001" +
                "000000000000000001E00000003900" +
                "0009002003006404006504006804006C02006F03007204007703010003" +
                "000200CF0100FF0100010004010000000E00000007ED06A3F3159D18");
            string archive = Path.Combine(directory.FullName, "archive.sarc");
            string output = Path.Combine(directory.FullName, "restored.txt");
            var crypto = CryptoService.CreateDefault();
            using var keys = crypto.CreateKeys("test-password");
            var manager = new ArchiveManager(new ArchiverController(), crypto);
            File.WriteAllBytes(archive, crypto.SealVolume(keys, 0, 1, payload));
            manager.UnpackFile(archive, output, "test-password");
            Assert.Equal(480, File.ReadAllBytes(output).Length);
            Assert.Equal(0x70208846u, Crc32.Compute(File.ReadAllBytes(output)));

            File.Delete(output);
            payload[19] ^= 1; // Change the expected CRC while keeping the volume authenticated.
            File.WriteAllBytes(archive, crypto.SealVolume(keys, 0, 1, payload));
            Assert.Equal(ArchiveErrorCode.E_CRC,
                Assert.Throws<ArchiveException>(() => manager.UnpackFile(archive, output, "test-password")).Code);
            Assert.False(File.Exists(output));
        }
        finally { directory.Delete(recursive: true); }
    }
}
