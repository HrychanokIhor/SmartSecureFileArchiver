using System;
using System.IO;
using System.Text.Json;

namespace SmartArchiver.Core.Algorithms
{
    public class MlpWeightsData
    {
        public int InputSize { get; set; }
        public int HiddenSize { get; set; }
        public int OutputSize { get; set; }
        public double[][]? W1 { get; set; }
        public double[]? B1 { get; set; }
        public double[][]? W2 { get; set; }
        public double[]? B2 { get; set; }
    }

    public class MultilayerPerceptron
    {
        private int _inputSize;
        private int _hiddenSize;
        private int _outputSize;

        private double[][] _w1 = Array.Empty<double[]>();
        private double[] _b1 = Array.Empty<double>();
        private double[][] _w2 = Array.Empty<double[]>();
        private double[] _b2 = Array.Empty<double>();

        private readonly Random _rnd;

        public MultilayerPerceptron(int inputSize = 52, int hiddenSize = 32, int outputSize = 7, int seed = 42)
        {
            _inputSize = inputSize;
            _hiddenSize = hiddenSize;
            _outputSize = outputSize;
            _rnd = new Random(seed);

            InitWeights();
        }

        private void InitWeights()
        {
            _w1 = new double[_inputSize][];
            for (int i = 0; i < _inputSize; i++)
            {
                _w1[i] = new double[_hiddenSize];
                for (int j = 0; j < _hiddenSize; j++)
                    _w1[i][j] = (_rnd.NextDouble() - 0.5) * Math.Sqrt(2.0 / _inputSize);
            }
            _b1 = new double[_hiddenSize];

            _w2 = new double[_hiddenSize][];
            for (int i = 0; i < _hiddenSize; i++)
            {
                _w2[i] = new double[_outputSize];
                for (int j = 0; j < _outputSize; j++)
                    _w2[i][j] = (_rnd.NextDouble() - 0.5) * Math.Sqrt(2.0 / _hiddenSize);
            }
            _b2 = new double[_outputSize];
        }

        public double[] Forward(double[] input, out double[] hidden)
        {
            if (input.Length != _inputSize)
                throw new ArgumentException($"Input size ({input.Length}) does not match MLP input size ({_inputSize}).");

            hidden = new double[_hiddenSize];
            for (int j = 0; j < _hiddenSize; j++)
            {
                double sum = _b1[j];
                for (int i = 0; i < _inputSize; i++)
                    sum += input[i] * _w1[i][j];
                hidden[j] = Math.Max(0, sum); // ReLU
            }

            double[] output = new double[_outputSize];
            double max = double.MinValue;
            for (int k = 0; k < _outputSize; k++)
            {
                double sum = _b2[k];
                for (int j = 0; j < _hiddenSize; j++)
                    sum += hidden[j] * _w2[j][k];
                output[k] = sum;
                if (sum > max) max = sum;
            }

            double expSum = 0;
            for (int k = 0; k < _outputSize; k++)
            {
                output[k] = Math.Exp(output[k] - max);
                expSum += output[k];
            }
            for (int k = 0; k < _outputSize; k++)
                output[k] /= expSum;

            return output;
        }

        public int Predict(double[] input)
        {
            var probs = Forward(input, out _);
            int bestClass = 0;
            double maxP = probs[0];
            for (int i = 1; i < _outputSize; i++)
            {
                if (probs[i] > maxP)
                {
                    maxP = probs[i];
                    bestClass = i;
                }
            }
            return bestClass;
        }

        public void TrainEpoch(double[][] inputs, int[] targets, double learningRate)
        {
            for (int sample = 0; sample < inputs.Length; sample++)
            {
                double[] x = inputs[sample];
                int target = targets[sample];

                double[] probs = Forward(x, out double[] hidden);

                double[] dO = new double[_outputSize];
                for (int k = 0; k < _outputSize; k++)
                    dO[k] = probs[k] - (k == target ? 1.0 : 0.0);

                double[] dH = new double[_hiddenSize];
                for (int j = 0; j < _hiddenSize; j++)
                {
                    double err = 0;
                    for (int k = 0; k < _outputSize; k++)
                        err += dO[k] * _w2[j][k];
                    dH[j] = hidden[j] > 0 ? err : 0;
                }

                for (int j = 0; j < _hiddenSize; j++)
                    for (int k = 0; k < _outputSize; k++)
                        _w2[j][k] -= learningRate * dO[k] * hidden[j];

                for (int k = 0; k < _outputSize; k++)
                    _b2[k] -= learningRate * dO[k];

                for (int i = 0; i < _inputSize; i++)
                    for (int j = 0; j < _hiddenSize; j++)
                        _w1[i][j] -= learningRate * dH[j] * x[i];

                for (int j = 0; j < _hiddenSize; j++)
                    _b1[j] -= learningRate * dH[j];
            }
        }

        public void Save(string filePath)
        {
            var data = new MlpWeightsData
            {
                InputSize = _inputSize,
                HiddenSize = _hiddenSize,
                OutputSize = _outputSize,
                W1 = _w1,
                B1 = _b1,
                W2 = _w2,
                B2 = _b2
            };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        // Сувора валідація при Load згідно з вимогою №3
        public void Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Model weights file not found.", filePath);

            string json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<MlpWeightsData>(json);

            if (data?.W1 == null || data?.B1 == null || data?.W2 == null || data?.B2 == null ||
                data.W1.Length != data.InputSize || data.W2.Length != data.HiddenSize)
            {
                throw new InvalidDataException("Invalid or incompatible MLP weights structure in JSON file.");
            }

            _inputSize = data.InputSize;
            _hiddenSize = data.HiddenSize;
            _outputSize = data.OutputSize;
            _w1 = data.W1;
            _b1 = data.B1;
            _w2 = data.W2;
            _b2 = data.B2;
        }
    }
}