using System.Diagnostics;
using System.Text.Json;
using SmartArchiver.Core.Services;
using SmartArchiver.Core.Services.Crypto;
using SmartArchiver.Optimization;

namespace SmartArchiver.CLI;

internal static class CorpusBenchmark
{
    private sealed record Measurement(long InputBytes, long ArchiveBytes, double Seconds, bool Exact,
        double Q, long FirstVolumeValue, long FirstGreedyValue);

    public static void Run(string team, string reportPath, ArchiverController controller, string password)
    {
        var options = TeamOptions.Load(Path.Combine(team, "team_config.json"));
        var files = Directory.EnumerateFiles(Path.Combine(team, "corpus"))
            .Where(f => Path.GetFileName(f) != "labels.csv" && !Path.GetFileName(f).StartsWith('.'))
            .Order(StringComparer.Ordinal).Select(f => new SarcArchive.Input(Path.GetFileName(f), File.ReadAllBytes(f)))
            .ToArray();
        if (files.Length == 0) throw new InvalidDataException("Benchmark corpus is empty.");
        var archive = new SarcArchive(CryptoService.CreateDefault(), controller);

        Measurement Measure(ArchiverParameters? parameters = null, CompressionMode? mode = null, bool greedyPacking = false)
        {
            long inputBytes = files.Sum(f => (long)f.Data.Length);
            long archiveBytes = 0, first = 0, firstGreedy = 0;
            bool exact;
            var clock = Stopwatch.StartNew();
            try
            {
                var packed = archive.Pack(files, password, options, parameters, mode, greedyPacking);
                archiveBytes = packed.Volumes.Sum(x => (long)x.Length);
                first = packed.FirstVolumeValue;
                firstGreedy = packed.FirstGreedyValue;
                var restored = archive.Unpack(packed.Volumes, password).ToDictionary(x => x.Name);
                exact = restored.Count == files.Length &&
                    files.All(x => restored.TryGetValue(x.Name, out var output) && output.Data.AsSpan().SequenceEqual(x.Data));
            }
            catch (SmartArchiver.Core.Exceptions.ArchiveException) { exact = false; }
            clock.Stop();
            double t = clock.Elapsed.TotalSeconds;
            double score = exact ? options.Alpha * inputBytes / (inputBytes + (double)archiveBytes) +
                                   options.Beta * options.ReferenceSeconds / (options.ReferenceSeconds + t) : 0;
            return new Measurement(inputBytes, archiveBytes, t, exact, score, first, firstGreedy);
        }

        _ = Measure(); // Warm up the same pipeline before recording comparisons.
        var greedy = Measure(mode: CompressionMode.Greedy);
        var lazy = Measure(mode: CompressionMode.Lazy);
        var greedyPacking = Measure(greedyPacking: true);
        if (!greedy.Exact || !lazy.Exact || !greedyPacking.Exact) throw new InvalidDataException("Corpus round trip failed.");

        var ga = new GeneticOptimizer(p => Measure(p).Q, populationSize: 5, generations: 3, seed: options.Seed);
        var (gaParams, gaBest, history) = ga.Optimize();
        int randomCalls = 0;
        var random = new RandomSearchOptimizer(p => { randomCalls++; return Measure(p).Q; }, ga.TotalEvaluations, options.Seed);
        var (randomParams, randomBest) = random.Optimize();
        if (randomCalls != ga.TotalEvaluations) throw new InvalidOperationException("Search budgets differ.");

        var data = new
        {
            files = files.Length, greedy, lazy, greedyPacking,
            genetic = new { evaluations = ga.TotalEvaluations, parameters = gaParams, bestQ = gaBest,
                bestSoFar = history, generationBest = ga.GenerationBest },
            random = new { evaluations = randomCalls, parameters = randomParams, bestQ = randomBest }
        };
        File.WriteAllText(reportPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Benchmark: {files.Length} files; {randomCalls} fitness calls per approach.");
    }
}
