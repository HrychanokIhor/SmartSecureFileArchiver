using System.Text.Json;
using SmartArchiver.Core.Algorithms;
using SmartArchiver.Core.Services;

namespace SmartArchiver.CLI;

internal static class ModelTraining
{
    internal sealed record Sample(double[] Features, int Class);

    internal static Sample[] ReadSamples(string folder)
    {
        var extractor = new FeatureExtractor();
        return File.ReadLines(Path.Combine(folder, "labels.csv")).Skip(1).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(line =>
            {
                // The supplied corpus has plain file names and numeric labels in its first two columns.
                string[] fields = line.Split(',', 3);
                if (fields.Length != 3 || !int.TryParse(fields[1], out int label) || label is < 0 or > 6 ||
                    Path.GetFileName(fields[0]) != fields[0] || fields[0] is "" or "." or "..")
                    throw new InvalidDataException("Invalid corpus labels.");
                return new Sample(extractor.Extract(File.ReadAllBytes(Path.Combine(folder, fields[0]))).ToVector(), label);
            }).ToArray();
    }

    public static void Train(string team, string directory)
    {
        var options = TeamOptions.Load(Path.Combine(team, "team_config.json"));
        var train = ReadSamples(Path.Combine(team, "train"));
        var valid = ReadSamples(Path.Combine(team, "valid"));
        if (train.Length == 0 || valid.Length == 0) throw new InvalidDataException("Empty training or validation split.");

        var normalizer = new DataNormalizer();
        normalizer.Fit(train.Select(x => x.Features).ToArray());
        var inputs = train.Select(x => normalizer.Transform(x.Features)).ToArray();
        var validation = valid.Select(x => normalizer.Transform(x.Features)).ToArray();
        var labels = train.Select(x => x.Class).ToArray();

        Directory.CreateDirectory(directory);
        double best = -1;
        int[,]? bestMatrix = null;
        var attempts = new List<object>();
        for (int run = 0; run < options.Initializations; run++)
        {
            int seed = options.Seed + run;
            var model = new MultilayerPerceptron(inputSize: inputs[0].Length, hiddenSize: 64, seed: seed);
            var random = new Random(seed);
            double runBest = -1;
            int bestEpoch = 0;
            for (int epoch = 1; epoch <= 120; epoch++)
            {
                int[] order = Enumerable.Range(0, train.Length).ToArray();
                random.Shuffle(order.AsSpan());
                model.TrainEpoch(order.Select(i => inputs[i]).ToArray(), order.Select(i => labels[i]).ToArray(),
                    0.01 / (1 + epoch / 60.0));

                var matrix = new int[7, 7];
                for (int i = 0; i < validation.Length; i++)
                    matrix[valid[i].Class, model.Predict(validation[i])]++;
                double score = MacroF1(matrix);
                if (score > runBest) { runBest = score; bestEpoch = epoch; }
                if (score <= best) continue;
                best = score;
                bestMatrix = matrix;
                model.Save(Path.Combine(directory, "weights.json"));
            }
            attempts.Add(new { seed, macroF1 = runBest, epoch = bestEpoch });
        }
        normalizer.Save(Path.Combine(directory, "normalization.json"));
        var report = new
        {
            validationSamples = valid.Length, macroF1 = best, requiredF1 = options.MinimumF1,
            meetsTarget = best >= options.MinimumF1, attempts,
            confusionMatrix = Enumerable.Range(0, 7)
                .Select(i => Enumerable.Range(0, 7).Select(j => bestMatrix![i, j]).ToArray()).ToArray()
        };
        File.WriteAllText(Path.Combine(directory, "validation.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Validation macro-F1: {best:F4} (target {options.MinimumF1:F4})");
    }

    public static ArchiverController Load(string directory)
    {
        var network = new MultilayerPerceptron();
        network.Load(Path.Combine(directory, "weights.json"));
        var normalizer = new DataNormalizer();
        normalizer.Load(Path.Combine(directory, "normalization.json"));
        // Validate the pair against the actual feature extractor before the CLI accepts it.
        _ = network.Predict(normalizer.Transform(new FeatureExtractor().Extract([42]).ToVector()));
        return new ArchiverController(network, normalizer);
    }

    private static double MacroF1(int[,] matrix)
    {
        double total = 0;
        for (int i = 0; i < 7; i++)
        {
            int actual = 0, predicted = 0;
            for (int j = 0; j < 7; j++)
            {
                actual += matrix[i, j];
                predicted += matrix[j, i];
            }
            if (actual + predicted != 0) total += 2.0 * matrix[i, i] / (actual + predicted);
        }
        return total / 7;
    }
}
