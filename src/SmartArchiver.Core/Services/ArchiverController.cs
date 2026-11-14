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
        private readonly MultilayerPerceptron _mlp = new MultilayerPerceptron();
        private readonly Lz77Algorithm _lz77 = new Lz77Algorithm();

        public int Classify(byte[] inputData) => _mlp.Predict(_extractor.Extract(inputData).ToVector());

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
    }
}