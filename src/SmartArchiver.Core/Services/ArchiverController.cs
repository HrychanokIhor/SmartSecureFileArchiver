using System;
using System.Collections.Generic;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Interfaces;

namespace SmartArchiver.Core.Services
{
    public class ArchiverController
    {
        private readonly FeatureExtractor _extractor = new FeatureExtractor();
        private readonly MultilayerPerceptron _mlp = new MultilayerPerceptron();
        private readonly Lz77Algorithm _lz77 = new Lz77Algorithm();

        public IEnumerable<ILz77Token> CompressFile(byte[] inputData)
        {
            // 1. Витягуємо ознаки файлу
            var features = _extractor.Extract(inputData);
            double[] vector = features.ToVector();

            // 2. Класифікуємо файл нейромережею (0..6)
            int fileType = _mlp.Predict(vector);

            // 3. Динамічний вибір стратегії:
            // Клас 5: Вже стиснуті дані (JPEG, ZIP, MP3) -> Відмова від стиснення (NO_COMPRESSION)
            if (fileType == 5)
            {
                Console.WriteLine("[MLP Classifier] Файл визначено як 'Вже стиснутий' (Клас 5). Стратегія: NO_COMPRESSION.");
                var rawTokens = new List<ILz77Token>();
                foreach (byte b in inputData)
                    rawTokens.Add(new LiteralToken(b));
                return rawTokens;
            }

            // Клас 0 (Текст) або 1 (Код) -> Застосовуємо ліниву жадібність (EncodeLazy)
            if (fileType == 0 || fileType == 1)
            {
                Console.WriteLine($"[MLP Classifier] Файл визначено як Текст/Код (Клас {fileType}). Стратегія: Lazy LZ77.");
                return _lz77.EncodeLazy(inputData);
            }

            // Для інших класів -> Звичайний жадібний LZ77
            Console.WriteLine($"[MLP Classifier] Файл визначено як Клас {fileType}. Стратегія: Standard LZ77.");
            return _lz77.Encode(inputData);
        }
    }
}
