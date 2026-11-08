using Xunit;
using SmartArchiver.Optimization;
using System;
using System.Linq;

namespace SmartArchiver.Tests
{
    public class OptimizationTest
    {
        [Fact]
        public void TestElitism_BestFitnessDoesNotDecrease()
        {
            // Arrange
            Func<ArchiverParameters, double> mockFitness = p => p.WindowSize + p.MinMatchLength;
            var ga = new GeneticOptimizer(mockFitness, populationSize: 10, generations: 5, elitism: true);

            // Act
            var (bestParams, bestFitness, history) = ga.Optimize();

            // Assert: Перевіряємо інваріант елітизму (фінальний результат >= початкового)
            Assert.True(history.Last() >= history.First(), "Елітизм порушено: фінальний результат гірший за початковий.");
            Assert.NotNull(bestParams);
        }

        [Fact]
        public void TestReviewRequirements_BestFitnessEqualsHistoryLast()
        {
            // Arrange: Перевірка виконання вимоги з рев'ю PR #13
            Func<ArchiverParameters, double> mockFitness = p => p.WindowSize * 0.5 + p.MinMatchLength * 2;
            var ga = new GeneticOptimizer(mockFitness, populationSize: 15, generations: 10, elitism: true);

            // Act
            var (bestParams, bestFitness, history) = ga.Optimize();

            // Assert: 1. History не порожня
            Assert.NotEmpty(history);
            
            // Assert: 2. BestFitness рівний останньому елементу history
            Assert.Equal(bestFitness, history.Last());
        }

        [Fact]
        public void TestBudgetEqualsRandomSearch()
        {
            // Arrange: Перевірка рівності бюджету викликів fitness з RandomSearch
            Func<ArchiverParameters, double> mockFitness = p => p.WindowSize + p.NoCompressionThreshold;
            
            int popSize = 10;
            int generations = 5;
            // Розрахунковий бюджет: 10 (ініціалізація) + 5 поколінь * 9 нових осіб (бо 1 еліта) = 55 оцінок
            
            var ga = new GeneticOptimizer(mockFitness, populationSize: popSize, generations: generations, elitism: true);
            var rs = new RandomSearchOptimizer(mockFitness, totalEvaluations: 55);

            // Act
            var (gaParams, gaFitness, _) = ga.Optimize();
            var (rsParams, rsFitness) = rs.Optimize();

            // Assert: Обидва алгоритми провели однакову кількість оцінок
            Assert.Equal(55, ga.TotalEvaluations);
            Assert.NotNull(gaParams);
            Assert.NotNull(rsParams);
        }

        [Fact]
        public void TestParametersCloning()
        {
            // Arrange
            var original = new ArchiverParameters 
            { 
                WindowSize = 8192, 
                MinMatchLength = 4, 
                NoCompressionThreshold = 50 
            };

            // Act
            var clone = original.Clone();
            clone.WindowSize = 16384;

            // Assert
            Assert.Equal(8192, original.WindowSize);
            Assert.Equal(16384, clone.WindowSize);
            Assert.Equal(original.MinMatchLength, clone.MinMatchLength);
        }

        [Fact]
        public void TestRandomSearch_ReturnsValidResult()
        {
            // Arrange
            Func<ArchiverParameters, double> mockFitness = p => p.WindowSize * 0.1;
            int budget = 30;
            var rs = new RandomSearchOptimizer(mockFitness, totalEvaluations: budget);

            // Act
            var (bestParams, bestFitness) = rs.Optimize();

            // Assert
            Assert.NotNull(bestParams);
            Assert.True(bestFitness >= 0);
            Assert.True(bestParams.WindowSize >= 4096 && bestParams.WindowSize <= 32768);
        }
    }
}