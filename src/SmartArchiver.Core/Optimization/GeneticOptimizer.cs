using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartArchiver.Optimization
{
    /// <summary>
    /// Реалізує класичний генетичний алгоритм (ГА) для пошуку оптимальних параметрів архіватора.
    /// </summary>
    public class GeneticOptimizer
    {
        private readonly Random _rand = new Random();
        private readonly Func<ArchiverParameters, double> _fitnessFunction;

        private readonly int _populationSize;
        private readonly int _generations;
        private readonly double _mutationRate;
        private readonly int _tournamentSize;
        private readonly bool _elitism;

        /// <summary>
        /// Загальна кількість викликаних оцінок fitness для звірки бюджету з RandomSearch.
        /// </summary>
        public int TotalEvaluations { get; private set; }

        public GeneticOptimizer(
            Func<ArchiverParameters, double> fitnessFunction,
            int populationSize = 20,
            int generations = 15,
            double mutationRate = 0.1,
            int tournamentSize = 3,
            bool elitism = true)
        {
            _fitnessFunction = fitnessFunction ?? throw new ArgumentNullException(nameof(fitnessFunction));
            _populationSize = populationSize;
            _generations = generations;
            _mutationRate = mutationRate;
            _tournamentSize = tournamentSize;
            _elitism = elitism;
        }

        /// <summary>
        /// Запускає цикл еволюції для пошуку найкращих параметрів архіватора.
        /// </summary>
        public (ArchiverParameters BestParams, double BestFitness, List<double> history) Optimize()
        {
            var history = new List<double>();
            TotalEvaluations = 0;

            // 1. Ініціалізація та первинна оцінка популяції (використовуємо кортежі замість класу Individual)
            var population = InitializePopulation();
            EvaluatePopulation(population);

            // Сортуємо популяцію за спаданням Fitness
            population = population.OrderByDescending(ind => ind.Fitness).ToList();
            
            double bestSoFar = population.First().Fitness;
            history.Add(bestSoFar);

            // 2. Основний цикл еволюції
            for (int gen = 0; gen < _generations; gen++)
            {
                var newPopulation = new List<(ArchiverParameters Parameters, double Fitness)>();

                // Елітизм: зберігаємо найкращу особину попереднього покоління
                if (_elitism)
                {
                    var elite = population.First();
                    newPopulation.Add((elite.Parameters.Clone(), elite.Fitness));
                }

                // Створення нових особин
                while (newPopulation.Count < _populationSize)
                {
                    var parent1 = TournamentSelection(population);
                    var parent2 = TournamentSelection(population);

                    var childParams = Crossover(parent1.Parameters, parent2.Parameters);
                    Mutate(childParams);

                    newPopulation.Add((childParams, 0.0));
                }

                // Оцінюємо нових особин
                int startIndex = _elitism ? 1 : 0;
                for (int i = startIndex; i < newPopulation.Count; i++)
                {
                    double fit = _fitnessFunction(newPopulation[i].Parameters);
                    newPopulation[i] = (newPopulation[i].Parameters, fit);
                    TotalEvaluations++;
                }

                population = newPopulation.OrderByDescending(ind => ind.Fitness).ToList();

                double currentGenBest = population.First().Fitness;
                if (currentGenBest > bestSoFar)
                {
                    bestSoFar = currentGenBest;
                }

                history.Add(bestSoFar);
            }

            var bestIndividual = population.First();
            return (bestIndividual.Parameters, bestSoFar, history);
        }

        private List<(ArchiverParameters Parameters, double Fitness)> InitializePopulation()
        {
            var pop = new List<(ArchiverParameters, double)>();
            for (int i = 0; i < _populationSize; i++)
            {
                var pars = new ArchiverParameters
                {
                    WindowSize = _rand.Next(4, 33) * 1024,
                    MinMatchLength = _rand.Next(3, 9),
                    NoCompressionThreshold = _rand.Next(10, 90)
                };
                pop.Add((pars, 0.0));
            }
            return pop;
        }

        private void EvaluatePopulation(List<(ArchiverParameters Parameters, double Fitness)> population)
        {
            for (int i = 0; i < population.Count; i++)
            {
                double fit = _fitnessFunction(population[i].Parameters);
                population[i] = (population[i].Parameters, fit);
                TotalEvaluations++;
            }
        }

        private (ArchiverParameters Parameters, double Fitness) TournamentSelection(List<(ArchiverParameters Parameters, double Fitness)> population)
        {
            (ArchiverParameters Parameters, double Fitness) best = default;
            bool initialized = false;

            for (int i = 0; i < _tournamentSize; i++)
            {
                var candidate = population[_rand.Next(population.Count)];
                if (!initialized || candidate.Fitness > best.Fitness)
                {
                    best = candidate;
                    initialized = true;
                }
            }
            return best;
        }

        private ArchiverParameters Crossover(ArchiverParameters p1, ArchiverParameters p2)
        {
            int point = _rand.Next(1, 3);
            var child = p1.Clone();

            if (point == 1)
            {
                child.MinMatchLength = p2.MinMatchLength;
                child.NoCompressionThreshold = p2.NoCompressionThreshold;
            }
            else if (point == 2)
            {
                child.NoCompressionThreshold = p2.NoCompressionThreshold;
            }

            return child;
        }

        private void Mutate(ArchiverParameters p)
        {
            if (_rand.NextDouble() < _mutationRate)
            {
                int geneIndex = _rand.Next(0, 3);
                switch (geneIndex)
                {
                    case 0:
                        p.WindowSize = _rand.Next(4, 33) * 1024;
                        break;
                    case 1:
                        p.MinMatchLength = _rand.Next(3, 9);
                        break;
                    case 2:
                        p.NoCompressionThreshold = _rand.Next(10, 90);
                        break;
                }
            }
        }
    }
}