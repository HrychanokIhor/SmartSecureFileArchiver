using System.Text;
using SmartArchiver.Core.Exceptions;
using SmartArchiver.Core.Services;
using SmartArchiver.Core.Services.Crypto;

namespace SmartArchiver.CLI;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            switch (args)
            {
                case ["train", var team, var destination]:
                    ModelTraining.Train(team, destination);
                    return 0;
                case ["benchmark", var team, var model, var report, var password]:
                    CorpusBenchmark.Run(team, report, ModelTraining.Load(model), password);
                    return 0;
                case ["pack", var input, var output, var password]:
                    Pack([input], output, password, FindTeamConfig(), null);
                    return 0;
                case ["pack-set", var output, var password, var config, var model, .. var inputs] when inputs.Length > 0:
                    Pack(inputs, output, password, config, model == "-" ? null : model);
                    return 0;
                case ["unpack", var input, var output, var password]:
                    var files = Open([input], password);
                    if (files.Count != 1) throw new InvalidDataException("Use unpack-set for multiple files.");
                    using (var stream = new FileStream(output, FileMode.CreateNew))
                        stream.Write(files[0].Data);
                    return 0;
                case ["unpack-set", var directory, var password, .. var volumes] when volumes.Length > 0:
                    Extract(Open(volumes, password), directory);
                    return 0;
                default:
                    Console.Error.WriteLine("""
                        pack <file> <volume> <password>
                        unpack <volume> <output-file> <password>
                        pack-set <archive.arc> <password> <team_config.json> <model-dir|-> <file> [...]
                        unpack-set <new-directory> <password> <volume> [...]
                        train <team-directory> <model-directory>
                        benchmark <team-directory> <model-directory> <report.json> <password>
                        """);
                    return 1;
            }
        }
        catch (ArchiveException ex) { Console.Error.WriteLine($"{ex.Code}: {ex.Message}"); return 1; }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }

    private static string FindTeamConfig()
    {
        for (DirectoryInfo? directory = new(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "team", "team_config.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("team/team_config.json not found. Use pack-set with an explicit config path.");
    }

    private static void Pack(string[] paths, string output, string password, string config, string? model)
    {
        var controller = model is null ? new ArchiverController() : ModelTraining.Load(model);
        var archive = new SarcArchive(CryptoService.CreateDefault(), controller);
        var files = paths.Select(p => new SarcArchive.Input(Path.GetFileName(p), File.ReadAllBytes(p))).ToArray();
        var packed = archive.Pack(files, password, TeamOptions.Load(config));
        var destinations = Enumerable.Range(0, packed.Volumes.Count).Select(i => packed.Volumes.Count == 1
            ? output : Path.Combine(Path.GetDirectoryName(output) ?? "", $"{Path.GetFileNameWithoutExtension(output)}.part{i}{Path.GetExtension(output)}")).ToArray();
        if (destinations.Any(File.Exists)) throw new IOException("An output volume already exists.");
        for (int i = 0; i < destinations.Length; i++)
        {
            using var stream = new FileStream(destinations[i], FileMode.CreateNew);
            stream.Write(packed.Volumes[i]);
            Console.WriteLine(destinations[i]);
        }
    }

    private static IReadOnlyList<SarcArchive.Input> Open(string[] volumes, string password) =>
        new SarcArchive(CryptoService.CreateDefault(), new ArchiverController()).Unpack(
            volumes.Select(File.ReadAllBytes).ToArray(), password);

    private static void Extract(IReadOnlyList<SarcArchive.Input> files, string destination)
    {
        string full = Path.GetFullPath(destination);
        string parent = Path.GetDirectoryName(full.TrimEnd(Path.DirectorySeparatorChar)) ??
            throw new IOException("Output must be a new directory.");
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, $".sarc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var file in files)
            {
                using var stream = new FileStream(Path.Combine(staging, file.Name), FileMode.CreateNew);
                stream.Write(file.Data);
            }
            Directory.Move(staging, full);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }
}
