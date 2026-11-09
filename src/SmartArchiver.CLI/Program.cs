using System;
using System.IO;
using SmartSecureFileArchiver.Core.Services;

namespace SmartArchiver.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Smart Secure File Archiver CLI ===");

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: archiver <path-to-file>");
                // Для зручності під час тестування можна вказати шлях за замовчуванням або вийти
                return;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File '{filePath}' not found.");
                return;
            }

            try
            {
                // Читаємо байти файлу
                byte[] fileData = File.ReadAllBytes(filePath);
                Console.WriteLine($"File loaded: {filePath} ({fileData.Length} bytes)");

                // Ініціалізуємо наш аналізатор
                var analyzer = new FileAnalyzer();

                Console.WriteLine("Analyzing file features and running neural network classification...");

                // Запускаємо інтелектуальний конвеєр
                var config = analyzer.Analyze(fileData);

                Console.WriteLine("Analysis complete successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during processing: {ex.Message}");
            }
        }
    }
}