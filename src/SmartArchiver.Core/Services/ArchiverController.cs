using System;
using System.Collections.Generic;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Models;

namespace SmartArchiver.Core.Services
{
    public class ArchiverController
    {
        private readonly FeatureExtractor _extractor = new FeatureExtractor();
        private readonly MultilayerPerceptron? _mlp;
        private readonly DataNormalizer? _normalizer;
        private readonly Lz77Algorithm _lz77 = new Lz77Algorithm();

        // Until a trained model is supplied, use the general LZ77 path.
        public ArchiverController() { }

        public ArchiverController(MultilayerPerceptron mlp, DataNormalizer normalizer)
        {
            _mlp = mlp ?? throw new ArgumentNullException(nameof(mlp));
            _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        }

        // Повертаємо метод CompressFile для сумісності з ArchiveManager.cs (виправляє помилку CS1061)
        public IEnumerable<ILz77Token> CompressFile(byte[] inputData)
        {
            int fileType = Classify(inputData);

            if (fileType == 5)
            {
                Console.WriteLine("[MLP Classifier] Файл визначено як 'Вже стиснутий' (Клас 5). Стратегія: NO_COMPRESSION.");
                var rawTokens = new List<ILz77Token>(inputData.Length);
                foreach (byte b in inputData)
                {
                    rawTokens.Add(new LiteralToken(b));
                }
                return rawTokens;
            }

            if (fileType == 0 || fileType == 1)
            {
                Console.WriteLine($"[MLP Classifier] Файл визначено як Текст/Код (Клас {fileType}). Стратегія: Lazy LZ77.");
                return _lz77.EncodeLazy(inputData);
            }

            Console.WriteLine($"[MLP Classifier] Файл визначено як Клас {fileType}. Стратегія: Standard LZ77.");
            return _lz77.Encode(inputData);
        }

        public int Classify(byte[] inputData)
        {
            if (_mlp is null || _normalizer is null) return 6;

            var features = _extractor.Extract(inputData);
            double[] vector = features.ToVector();
            double[] normalizedVector = _normalizer.Transform(vector);
            return _mlp.Predict(normalizedVector);
        }

        // Синхронізовано з PR №20: повернення сирих байтів в обхід кодеків (coded_len == raw_len)
        public byte[] ProcessNoCompression(byte[] inputData)
        {
            Console.WriteLine("[ArchiverController] Стратегія: NO_COMPRESSION. Запуск в обхід LZ77/Huffman (coded_len == raw_len).");
            return inputData;
        }
    }
}