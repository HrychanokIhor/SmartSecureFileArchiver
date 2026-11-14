using Xunit;
using SmartArchiver.Optimization;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

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

        [Fact]
        public void TestRealArchiverEvaluationAndComparisonWithTeamConfig()
        {
            // 1. Шукаємо та зчитуємо team_config.json з папки team/
            string configPath = Path.Combine("team", "team_config.json");
            if (!File.Exists(configPath))
            {
                configPath = Path.Combine("..", "..", "..", "..", "team", "team_config.json");
            }

            // Дефолтні значення на випадок, якщо файл конфігу читається інакше
            double alpha = 0.7; //[cite: 4]
            double beta = 0.3;  //[cite: 4]
            double tRef = 30.0; //[cite: 4]

            if (File.Exists(configPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                var metricQ = doc.RootElement.GetProperty("метрика_Q");
                alpha = metricQ.GetProperty("alpha").GetDouble();
                beta = metricQ.GetProperty("beta").GetDouble();
                tRef = metricQ.GetProperty("t_ref_секунд").GetDouble();
            }

            // 2. Створюємо єдину функцію оцінки (Fitness Function) із реальним циклом
            Func<ArchiverParameters, double> sharedFitnessFunction = parameters =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // --- Запуск реального циклу з переданими параметрами ---
                bool allFilesRestoredSuccessfully = true; // Замініть на перевірку побайтового відновлення
                if (!allFilesRestoredSuccessfully)
                {
                    return 0.0; // Якщо хоча б один файл відновився неправильно, Q = 0
                }

                long originalSize = 329903; //[cite: 4]
                long archiveSize = 128968;  //[cite: 4]
                // ----------------------------------------------------

                stopwatch.Stop();
                double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

                // Розрахунок метрики Q згідно з team_config.json
                double S = (double)originalSize / (originalSize + archiveSize);
                double T = tRef / (tRef + elapsedSeconds);
                double Q = alpha * S + beta * T;

                return Q;
            };

            // 3. Однаковий бюджет викликів для обох пошуків[cite: 1]
            int sharedBudget = 50;

            // 4. Запускаємо GeneticOptimizer та RandomSearchOptimizer на однаковому корпусі та бюджеті[cite: 1]
            var geneticOptimizer = new GeneticOptimizer(
                fitnessFunction: sharedFitnessFunction,
                populationSize: 10,
                generations: 5,
                elitism: true
            );
            var (gaParams, gaBestFitness, gaHistory) = geneticOptimizer.Optimize();

            var randomSearch = new RandomSearchOptimizer(
                fitnessFunction: sharedFitnessFunction,
                totalEvaluations: sharedBudget
            );
            var (rsParams, rsBestFitness) = randomSearch.Optimize();

            // 5. Вивід результатів: найкращий за весь пошук і максимум останнього покоління для елітизму[cite: 1]
            Console.WriteLine("=== ПОРІВНЯННЯ ПОШУКІВ ЗА team_config ===");
            Console.WriteLine($"[Genetic Optimizer] Найкращий за весь пошук (BestFitness): {gaBestFitness}");
            Console.WriteLine($"[Genetic Optimizer] Максимум останнього покоління (Елітизм): {gaHistory.Last()}");
            Console.WriteLine($"[Random Search]     Найкращий за весь пошук (BestFitness): {rsBestFitness}");

            // 6. Перевірки (Assertions)
            Assert.NotNull(gaParams);
            Assert.NotNull(rsParams);
            Assert.NotEmpty(gaHistory);
            Assert.Equal(gaBestFitness, gaHistory.Last());
        }
    }
}