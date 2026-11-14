using System;

namespace SmartArchiver.Optimization
{
    /// <summary>
    /// Реалізує випадковий пошук (Random Search) для порівняння результатів з генетичним алгоритмом.
    /// Працює за однакового загального бюджету обчислень (кількості викликів функції пристосованості).
    /// </summary>
    public class RandomSearchOptimizer
    {
        private readonly Random _rand;
        private readonly Func<ArchiverParameters, double> _fitnessFunction;
        private readonly int _totalEvaluations;

        public RandomSearchOptimizer(Func<ArchiverParameters, double> fitnessFunction, int totalEvaluations, int? seed = null)
        {
            _fitnessFunction = fitnessFunction;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalEvaluations);
            _totalEvaluations = totalEvaluations;
            _rand = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Запускає випадковий перебір параметрів у межах заданого бюджету.
        /// </summary>
        public (ArchiverParameters BestParams, double BestFitness) Optimize()
        {
            ArchiverParameters bestParams = null;
            double bestFitness = double.MinValue;

            for (int i = 0; i < _totalEvaluations; i++)
            {
                var candidate = new ArchiverParameters
                {
                    WindowSize = _rand.Next(4, 33) * 1024,
                    MinMatchLength = _rand.Next(3, 9),
                    NoCompressionThreshold = _rand.Next(10, 90)
                };

                double fitness = _fitnessFunction(candidate);
                if (fitness > bestFitness)
                {
                    bestFitness = fitness;
                    bestParams = candidate;
                }
            }

            return (bestParams, bestFitness);
        }
    }
}