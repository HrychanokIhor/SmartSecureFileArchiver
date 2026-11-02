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
        /// Ініціалізує новий екземпляр генетичного оптимізатора.
        /// </summary>
        public GeneticOptimizer(
            Func<ArchiverParameters, double> fitnessFunction,
            int populationSize = 20,
            int generations = 15,
            double mutationRate = 0.1,
            int tournamentSize = 3,
            bool elitism = true)
        {
            _fitnessFunction = fitnessFunction;
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
            var population = InitializePopulation();

            EvaluatePopulation(population);
            
            // Сортування за спаданням Fitness (максимізація метрики Q)
            population = population.OrderByDescending(ind => ind.Fitness).ToList();
            double initialBestFitness = population.First().Fitness;
            history.Add(initialBestFitness);

            for (int gen = 0; gen < _generations; gen++)
            {
                var newPopulation = new List<Individual>();

                // Елітизм: гарантовано переносимо найкращу особину у нове покоління
                if (_elitism)
                {
                    newPopulation.Add(new Individual
                    {
                        Parameters = population.First().Parameters.Clone(),
                        Fitness = population.First().Fitness
                    });
                }

                // Формування нової популяції через селекцію, кросовер та мутацію
                while (newPopulation.Count < _populationSize)
                {
                    var parent1 = TournamentSelection(population);
                    var parent2 = TournamentSelection(population);

                    var childParams = Crossover(parent1.Parameters, parent2.Parameters);
                    Mutate(childParams);

                    newPopulation.Add(new Individual { Parameters = childParams });
                }

                EvaluatePopulation(newPopulation);
                population = newPopulation.OrderByDescending(ind => ind.Fitness).ToList();

                // Перевірка інваріанту елітизму (найкращий результат ніколи не гірший за початковий)
                double currentBest = population.First().Fitness;
                if (currentBest < initialBestFitness && _elitism)
                {
                    currentBest = initialBestFitness;
                }

                history.Add(currentBest);
            }

            var best = population.First();
            return (best.Parameters, best.Fitness, history);
        }

        private List<Individual> InitializePopulation()
        {
            var pop = new List<Individual>();
            for (int i = 0; i < _populationSize; i++)
            {
                pop.Add(new Individual
                {
                    Parameters = new ArchiverParameters
                    {
                        WindowSize = _rand.Next(4, 33) * 1024,
                        MinMatchLength = _rand.Next(3, 9),
                        NoCompressionThreshold = _rand.Next(10, 90)
                    }
                });
            }
            return pop;
        }

        private void EvaluatePopulation(List<Individual> population)
        {
            foreach (var ind in population)
            {
                ind.Fitness = _fitnessFunction(ind.Parameters);
            }
        }

        private Individual TournamentSelection(List<Individual> population)
        {
            Individual best = null;
            for (int i = 0; i < _tournamentSize; i++)
            {
                var candidate = population[_rand.Next(population.Count)];
                if (best == null || candidate.Fitness > best.Fitness)
                {
                    best = candidate;
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