using System;
using System.IO;
using System.Text;
using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Integration;
using SmartArchiver.Core.Services;
using SmartArchiver.Core.Services.Crypto;

namespace SmartArchiver.CLI
{
    class Program
    {
        static int Main(string[] args)
        {
            // Вмикаємо UTF-8, щоб у консолі коректно відображалася українська мова
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length < 4)
            {
                Console.WriteLine("=== Розумний захищений архіватор (SmartArchiver) ===");
                Console.WriteLine("Використання:");
                Console.WriteLine("  Пакування:    SmartArchiver.CLI pack <вхідний_файл> <вихідний_архів.sarc> <пароль>");
                Console.WriteLine("  Розпакування: SmartArchiver.CLI unpack <вхідний_архів.sarc> <вихідний_файл> <пароль>");
                return 1;
            }

            string command = args[0].ToLowerInvariant();
            string inputPath = args[1];
            string outputPath = args[2];
            string password = args[3];

            // 1. Ініціалізуємо модулі колег
            var cryptoService = CryptoService.CreateDefault();
            var archiverController = new ArchiverController();

            // 2. Передаємо їх у головний менеджер
            var archiveManager = new ArchiveManager(archiverController, cryptoService);

            try
            {
                switch (command)
                {
                    case "pack":
                        Console.WriteLine($"[INFO] Пакування файлу '{inputPath}'...");
                        archiveManager.PackFile(inputPath, outputPath, password);
                        Console.WriteLine($"[OK] Успішно запаковано в '{outputPath}'!");
                        return 0;

                    case "unpack":
                        Console.WriteLine($"[INFO] Розпакування архіву '{inputPath}'...");
                        archiveManager.UnpackFile(inputPath, outputPath, password);
                        Console.WriteLine($"[OK] Успішно розпаковано в '{outputPath}'!");
                        return 0;

                    default:
                        Console.WriteLine($"[ПОМИЛКА] Невідома команда '{command}'. Використовуйте 'pack' або 'unpack'.");
                        return 1;
                }
            }
            catch (ArchiveException ex)
            {
                Console.WriteLine($"[ПОМИЛКА АРХІВУ: {ex.Code}] {ex.Message}");
                return 1;
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"[ПОМИЛКА ФАЙЛУ] Не вдалося знайти файл: {inputPath}");
                return 1;
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"[ПОМИЛКА ДОСТУПУ] Немає прав для доступу або вказано папку замість файлу: {outputPath}");
                return 1;
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[ПОМИЛКА ВВОДУ/ВИВОДУ] {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА АРХІВАЦІЇ] {ex.Message}");
                return 1;
            }
        }
    }
}