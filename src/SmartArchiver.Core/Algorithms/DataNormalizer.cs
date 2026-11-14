using System;
using System.IO;
using System.Text.Json;

namespace SmartArchiver.Core.Algorithms
{
    public class NormalizationParameters
    {
        public double[]? Mean { get; set; }
        public double[]? Std { get; set; }
    }

    public class DataNormalizer
    {
        private double[]? _mean;
        private double[]? _std;

        public void Fit(double[][] trainInputs)
        {
            int featureCount = trainInputs[0].Length;
            int sampleCount = trainInputs.Length;

            _mean = new double[featureCount];
            _std = new double[featureCount];

            for (int j = 0; j < featureCount; j++)
            {
                double sum = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    sum += trainInputs[i][j];
                }
                _mean[j] = sum / sampleCount;
            }

            for (int j = 0; j < featureCount; j++)
            {
                double varianceSum = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    double diff = trainInputs[i][j] - _mean[j];
                    varianceSum += diff * diff;
                }
                _std[j] = Math.Sqrt(varianceSum / sampleCount);
                if (_std[j] == 0) _std[j] = 1.0;
            }
        }

        public double[] Transform(double[] input)
        {
            if (_mean == null || _std == null)
                throw new InvalidOperationException("Normalizer has not been fitted or loaded yet.");

            double[] normalized = new double[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                normalized[i] = (input[i] - _mean[i]) / _std[i];
            }
            return normalized;
        }

        public void Save(string filePath)
        {
            var paramsObj = new NormalizationParameters { Mean = _mean, Std = _std };
            string json = JsonSerializer.Serialize(paramsObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public void Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Normalization file not found.", filePath);

            string json = File.ReadAllText(filePath);
            var paramsObj = JsonSerializer.Deserialize<NormalizationParameters>(json);
            if (paramsObj?.Mean != null && paramsObj.Std != null)
            {
                _mean = paramsObj.Mean;
                _std = paramsObj.Std;
            }
        }
    }
}