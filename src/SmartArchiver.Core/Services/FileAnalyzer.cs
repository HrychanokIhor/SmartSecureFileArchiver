using SmartArchiver.Core.Abstractions;
using SmartArchiver.Core.Algorithms;
using SmartSecureFileArchiver.Core.Algorithms;
using SmartSecureFileArchiver.Core.Models;

namespace SmartSecureFileArchiver.Core.Services
{
    public class FileAnalyzer : IFileAnalyzer
    {
        private readonly FeatureExtractor _featureExtractor;
        private readonly MultilayerPerceptron _mlp;

        public FileAnalyzer()
        {
            _featureExtractor = new FeatureExtractor();
            _mlp = new MultilayerPerceptron(inputSize: 52, hiddenSize: 32, outputSize: 7);
        }

        public CompressionConfig Analyze(byte[] fileData)
        {
            // 1. Витягуємо ознаки з файлу
            FileFeatures features = _featureExtractor.Extract(fileData);

            // 2. Перетворюємо в числовий вектор розмірністю 52
            double[] vector = features.ToVector();

            // 3. Передаємо у нейромережу для класифікації (отримуємо індекс класу від 0 до 6)
            int predictedClass = _mlp.Predict(vector);

            // 4. Створюємо конфігурацію стиснення, передаючи необхідні параметри в конструктор
            // (вказуємо true для ShouldCompress та передбачений клас)
            return new CompressionConfig(true, predictedClass);
        }
    }
}