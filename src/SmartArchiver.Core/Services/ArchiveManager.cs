using System;
using System.IO;
using System.Collections.Generic;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Services;

namespace SmartArchiver.Core.Integration
{
    public class ArchiveManager
    {
        private readonly ArchiverController _archiverController;
        private readonly ICryptoService _cryptoService;

        public ArchiveManager(ArchiverController archiverController, ICryptoService cryptoService)
        {
            _archiverController = archiverController ?? throw new ArgumentNullException(nameof(archiverController));
            _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
        }

        public void PackFile(string sourceFilePath, string archiveFilePath, string password)
        {
            try
            {
                byte[] inputData = File.ReadAllBytes(sourceFilePath);

                IEnumerable<ILz77Token> tokens = _archiverController.CompressFile(inputData);

                byte[] payload = ConvertTokensToBytes(tokens);

                using var keys = _cryptoService.CreateKeys(password);

                byte[] volume = _cryptoService.SealVolume(keys, 0, 1, payload);

                File.WriteAllBytes(archiveFilePath, volume);
                Console.WriteLine("Архів успішно створено та зашифровано!");
            }
            catch (ArchiveException ex)
            {
                // Виправлено розрив рядка тут
                Console.WriteLine($"[Помилка Архіватора]: {ex.Code} - {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Системна помилка]: {ex.Message}");
            }
        }

        public void UnpackFile(string archiveFilePath, string outputFilePath, string password)
        {
            try
            {
                byte[] volumeBytes = File.ReadAllBytes(archiveFilePath);

                var header = _cryptoService.ReadHeader(volumeBytes);

                using var keys = _cryptoService.DeriveKeys(password, header.Salt);

                var opened = _cryptoService.OpenVolume(keys, volumeBytes);

                byte[] originalData = opened.Payload;

                File.WriteAllBytes(outputFilePath, originalData);
                Console.WriteLine("Архів успішно розпаковано!");
            }
            catch (ArchiveException ex)
            {
                // Виправлено розрив рядка тут
                Console.WriteLine($"[Помилка доступу]: {ex.Code} - {ex.Message}");
            }
        }

        private byte[] ConvertTokensToBytes(IEnumerable<ILz77Token> tokens)
        {
            return new byte[0];
        }
    }
}