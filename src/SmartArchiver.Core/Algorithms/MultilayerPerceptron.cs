using System;

namespace SmartSecureFileArchiver.Core.Algorithms
{
    public class MultilayerPerceptron
    {
        private int _inputSize;
        private int _hiddenSize;
        private int _outputSize;

        private double[][] _w1; // [inputSize][hiddenSize]
        private double[] _b1;   // [hiddenSize]
        private double[][] _w2; // [hiddenSize][outputSize]
        private double[] _b2;   // [outputSize]

        private Random _rnd = new Random(42);

        public MultilayerPerceptron(int inputSize = 52, int hiddenSize = 32, int outputSize = 7)
        {
            _inputSize = inputSize;
            _hiddenSize = hiddenSize;
            _outputSize = outputSize;

            InitWeights();
        }

        private void InitWeights()
        {
            _w1 = new double[_inputSize][];
            for (int i = 0; i < _inputSize; i++)
            {
                _w1[i] = new double[_hiddenSize];
                for (int j = 0; j < _hiddenSize; j++)
                    _w1[i][j] = (_rnd.NextDouble() - 0.5) * Math.Sqrt(2.0 / _inputSize); // Xavier / He init
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

            // Softmax
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

                // Backpropagation (Cross-Entropy Loss + Softmax)
                double[] dO = new double[_outputSize];
                for (int k = 0; k < _outputSize; k++)
                    dO[k] = probs[k] - (k == target ? 1.0 : 0.0);

                double[] dH = new double[_hiddenSize];
                for (int j = 0; j < _hiddenSize; j++)
                {
                    double err = 0;
                    for (int k = 0; k < _outputSize; k++)
                        err += dO[k] * _w2[j][k];
                    dH[j] = hidden[j] > 0 ? err : 0; // Derivative of ReLU
                }

                // Update Weights Layer 2
                for (int j = 0; j < _hiddenSize; j++)
                    for (int k = 0; k < _outputSize; k++)
                        _w2[j][k] -= learningRate * dO[k] * hidden[j];

                for (int k = 0; k < _outputSize; k++)
                    _b2[k] -= learningRate * dO[k];

                // Update Weights Layer 1
                for (int i = 0; i < _inputSize; i++)
                    for (int j = 0; j < _hiddenSize; j++)
                        _w1[i][j] -= learningRate * dH[j] * x[i];

                for (int j = 0; j < _hiddenSize; j++)
                    _b1[j] -= learningRate * dH[j];
            }
        }
    }
}