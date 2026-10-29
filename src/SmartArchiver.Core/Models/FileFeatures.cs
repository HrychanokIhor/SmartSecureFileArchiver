using System;

namespace SmartSecureFileArchiver.Core.Models
{
    /// <summary>
    /// Модель для зберігання характеристик (ознак) файлу, 
    /// які використовуються аналізатором та нейромережею (MLP).
    /// </summary>
    public class FileFeatures
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public double Entropy { get; set; }
        public double CompressionRatioEstimate { get; set; }

        // Конструктор за замовчуванням
        public FileFeatures() { }

        // Конструктор з базовими параметрами
        public FileFeatures(string fileName, long fileSize, double entropy)
        {
            FileName = fileName;
            FileSize = fileSize;
            Entropy = entropy;
        }
    }
}