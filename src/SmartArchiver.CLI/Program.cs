using System;
using System.IO;
using SmartArchiver.Core.Integration;
using SmartArchiver.Core.Services;
using SmartArchiver.Core.Services.Crypto; // Додаємо namespace, де лежить CryptoService

namespace SmartArchiver.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("=== Розумний захищений архіватор (SmartArchiver) ===");
                Console.WriteLine("Використання:");
                Console.WriteLine("  Пакування:   SmartArchiver.CLI pack <вхідний_файл> <вихідний_архів.sarc> <пароль>");
                Console.WriteLine("  Розпакування: SmartArchiver.CLI unpack <вхідний_архів.sarc> <папка_призначення> <пароль>");
                return;
            }

            string command = args[0].ToLower();
            string inputPath = args[1];
            string outputPath = args[2];
            string password = args[3];

            // 1. Ініціалізуємо модулі колег 
            // Використовуємо готовий метод від vinsst, щоб не прописувати 4 класи вручну!
            var cryptoService = CryptoService.CreateDefault();
            var archiverController = new ArchiverController();

            // 2. Передаємо їх у ваш головний менеджер
            var archiveManager = new ArchiveManager(archiverController, cryptoService);

            try
            {
                switch (command)
                {
                    case "pack":
                        Console.WriteLine($"[INFO] Пакування файлу '{inputPath}'...");
                        archiveManager.PackFile(inputPath, outputPath, password);
                        Console.WriteLine($"[OK] Успішно запаковано в '{outputPath}'!");
                        break;

                    case "unpack":
                        Console.WriteLine($"[INFO] Розпакування архіву '{inputPath}'...");
                        archiveManager.UnpackFile(inputPath, outputPath, password);
                        Console.WriteLine($"[OK] Успішно розпаковано в '{outputPath}'!");
                        break;

                    default:
                        Console.WriteLine($"[ПОМИЛКА] Невідома команда '{command}'. Використовуйте 'pack' або 'unpack'.");
                        break;
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"[ПОМИЛКА ФАЙЛУ] Не вдалося знайти файл: {inputPath}");
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"[ПОМИЛКА ДОСТУПУ] Немає прав для запису у файл: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА АРХІВАЦІЇ] {ex.Message}");
            }
        }
    }
}