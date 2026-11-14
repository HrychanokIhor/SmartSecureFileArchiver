using System;
using System.IO;
using System.Collections.Generic;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Services;
using SmartArchiver.Core.Services.Huffman;

namespace SmartArchiver.Core.Integration
{
    public class ArchiveManager
    {
        private readonly ArchiverController _archiverController;
        private readonly ICryptoService _cryptoService;
        private readonly Lz77HuffmanCodec _huffmanCodec = new Lz77HuffmanCodec();
        private readonly Lz77Algorithm _lz77 = new Lz77Algorithm();

        public ArchiveManager(ArchiverController archiverController, ICryptoService cryptoService)
        {
            _archiverController = archiverController ?? throw new ArgumentNullException(nameof(archiverController));
            _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
        }

        public void PackFile(string sourceFilePath, string archiveFilePath, string password)
        {
            byte[] inputData = File.ReadAllBytes(sourceFilePath);

            // 1. MLP класифікація та LZ77 токенізація
            IEnumerable<ILz77Token> tokens = _archiverController.CompressFile(inputData);

            // 2. Кодування Гаффманом у байтовий payload (заглушку прибрано!)
            byte[] payload = ConvertTokensToBytes(tokens);

            // 3. Криптографічний захист (Enc-then-MAC) та запис тому
            using var keys = _cryptoService.CreateKeys(password);
            byte[] volume = _cryptoService.SealVolume(keys, 0, 1, payload);

            File.WriteAllBytes(archiveFilePath, volume);
        }

        public void UnpackFile(string archiveFilePath, string outputFilePath, string password)
        {
            byte[] volumeBytes = File.ReadAllBytes(archiveFilePath);

            // 1. Читання заголовка, перевірка HMAC та розшифрування
            var header = _cryptoService.ReadHeader(volumeBytes);
            using var keys = _cryptoService.DeriveKeys(password, header.Salt);
            var opened = _cryptoService.OpenVolume(keys, volumeBytes);

            // 2. Зворотне декодування Гаффмана та LZ77 в оригінальні байти
            byte[] originalData = ConvertBytesToData(opened.Payload);

            File.WriteAllBytes(outputFilePath, originalData);
        }

        private byte[] ConvertTokensToBytes(IEnumerable<ILz77Token> tokens)
        {
            var huffTokens = Lz77HuffmanAdapter.ToHuffman(tokens);
            var block = _huffmanCodec.Compress(huffTokens);

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            WriteEncoded(writer, block.Literals);
            WriteEncoded(writer, block.Lengths);
            WriteEncoded(writer, block.Distances);

            return ms.ToArray();
        }

        private byte[] ConvertBytesToData(byte[] payload)
        {
            using var ms = new MemoryStream(payload);
            using var reader = new BinaryReader(ms);

            var literals = ReadEncoded(reader);
            var lengths = ReadEncoded(reader);
            var distances = ReadEncoded(reader);
            var block = new Lz77HuffmanCodec.Block(literals, lengths, distances);

            var huffTokens = _huffmanCodec.Decompress(block);
            var lzTokens = Lz77HuffmanAdapter.FromHuffman(huffTokens);

            return _lz77.Decode(lzTokens);
        }

        private CanonicalHuffmanCodec.Encoded ReadEncoded(BinaryReader reader)
        {
            int headerLength = reader.ReadInt32();
            byte[] header = reader.ReadBytes(headerLength);
            int dataLength = reader.ReadInt32();
            byte[] data = reader.ReadBytes(dataLength);
            return new CanonicalHuffmanCodec.Encoded(data, header);
        }

        private void WriteEncoded(BinaryWriter writer, CanonicalHuffmanCodec.Encoded encoded)
        {
            writer.Write(encoded.Header.Length);
            writer.Write(encoded.Header);
            writer.Write(encoded.Data.Length);
            writer.Write(encoded.Data);
        }
    }
}