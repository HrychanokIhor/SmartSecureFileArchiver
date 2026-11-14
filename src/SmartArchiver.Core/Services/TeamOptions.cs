using System.Text.Json;

namespace SmartArchiver.Core.Services;

public sealed record TeamOptions(int BlockSize, int MaxVolumeBytes, IReadOnlyList<int> Priorities,
    double Alpha, double Beta, double ReferenceSeconds, int Seed, int Initializations, double MinimumF1)
{
    public static TeamOptions Load(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;
        if (root.GetProperty("версія_формату").GetInt32() != 1)
            throw new InvalidDataException("Unsupported team format version.");
        var p = root.GetProperty("пакування");
        var q = root.GetProperty("метрика_Q");
        var c = root.GetProperty("класифікатор");
        var priorities = root.GetProperty("пріоритети_типів");
        var classes = c.GetProperty("класи");
        var options = new TeamOptions(p.GetProperty("block_size").GetInt32(),
            p.GetProperty("max_volume_bytes").GetInt32(),
            Enumerable.Range(0, 7).Select(i => priorities.GetProperty(classes.GetProperty(i.ToString()).GetString()!).GetInt32()).ToArray(),
            q.GetProperty("alpha").GetDouble(), q.GetProperty("beta").GetDouble(),
            q.GetProperty("t_ref_секунд").GetDouble(), root.GetProperty("seed").GetInt32(),
            c.GetProperty("протокол_навчання").GetProperty("ініціалізацій").GetInt32(), c.GetProperty("F1_min").GetDouble());
        if (options.BlockSize <= 0 || options.MaxVolumeBytes <= 100 ||
            options.Priorities.Any(x => x <= 0) || options.Alpha < 0 || options.Beta < 0 ||
            Math.Abs(options.Alpha + options.Beta - 1) > 1e-9 || options.ReferenceSeconds <= 0 ||
            options.Initializations < 3 || options.MinimumF1 is < 0 or > 1)
            throw new InvalidDataException("Invalid team configuration.");
        return options;
    }
}
