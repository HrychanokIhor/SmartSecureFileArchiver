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
            // Arrange: проста тестова функція пристосованості
            Func<ArchiverParameters, double> mockFitness = p => p.WindowSize + p.MinMatchLength;
            
            // Створюємо ГА з увімкненим елітизмом
            var ga = new GeneticOptimizer(mockFitness, populationSize: 10, generations: 5, elitism: true);

            // Act
            var (bestParams, bestFitness, history) = ga.Optimize();

            // Assert: перевіряємо інваріант елітизму (кейс №17 з методички)
            // Найкращий результат фіналу має бути >= за результат нульового покоління[cite: 2]
            Assert.True(history.Last() >= history.First(), "Елітизм порушено: фінальний результат гірший за початковий.");
            Assert.NotNull(bestParams);
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
            clone.WindowSize = 16384; // Змінюємо клонований об'єкт

            // Assert: оригінал не повинен змінитися
            Assert.Equal(8192, original.WindowSize);
            Assert.Equal(16384, clone.WindowSize);
            Assert.Equal(original.MinMatchLength, clone.MinMatchLength);
        }

        [Fact]
        public void TestRandomSearch_ReturnsValidResult()
        {
            // Arrange
            Func<ArchiverParameters, double> mockFitness = p => p.WindowSize * 0.1;
            int budget = 30; // Бюджет оцінок
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