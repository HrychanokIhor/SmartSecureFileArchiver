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
        private readonly MultilayerPerceptron _mlp;
        private readonly DataNormalizer _normalizer;
        private readonly Lz77Algorithm _lz77 = new Lz77Algorithm();

        // Конструктор за замовчуванням (щоб Program.cs не видавав помилку CS7036)
        public ArchiverController()
        {
            _mlp = new MultilayerPerceptron();
            _normalizer = new DataNormalizer();
        }

        // Конструктор із параметрами (для навченої моделі та нормалізатора)
        public ArchiverController(MultilayerPerceptron mlp, DataNormalizer normalizer)
        {
            _mlp = mlp ?? throw new ArgumentNullException(nameof(mlp));
            _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        }

        public IEnumerable<ILz77Token> CompressFile(byte[] inputData)
        {
            int fileType = Classify(inputData);

            // Стратегія NO_COMPRESSION для класу 5 (coded_len == raw_len в обхід кодеків)
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
            var features = _extractor.Extract(inputData);
            double[] vector = features.ToVector();

            // Якщо нормалізатор ще не був навчений/завантажений, обережно пропускаємо або трансформуємо
            try
            {
                vector = _normalizer.Transform(vector);
            }
            catch (InvalidOperationException)
            {
                // Якщо нормування не завантажене, використовуємо сирий вектор
            }

            return _mlp.Predict(vector);
        }
    }
}