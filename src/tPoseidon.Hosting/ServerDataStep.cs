using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.LibConfig.ServerData;
using tPoseidon.Hosting.Processes;

namespace tPoseidon.Hosting;

public sealed record StepProgress(int Step, int Steps, string Message);

public static class ServerDataStep
{
    public const string SnapshotBuilderAssembly = "tPoseidon.Tools.BuildWorldSnapshot";
    public const int Steps = 3;
    public const int DataVersion = 2;
    public const string DataVersionFile = "data-version.txt";
    private const string SnapshotVariant = "kr2014";
    private const string ZoneRoutingFile = "master-zone-routing.json";
    private const string ZoneRouting = "{\n  \"default\": 2,\n  \"zones\": {\n    \"148\": 2,\n    \"153\": 3\n  }\n}\n";

    public static void Run(InstallPaths paths, HostSettings settings, IProgress<StepProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        var libconfig = ClientFolder.LibConfigFor(settings);
        var mapFolder = ClientFolder.MapFolder(settings.ClientFolder);
        var clientExecutable = Path.Combine(settings.ClientFolder, settings.ClientExecutable);
        if (!RunningClient.IsRunning(clientExecutable))
        {
            ShadowSanctuaryClientData.Apply(libconfig, clientExecutable);
        }

        var builder = paths.AppExecutable(SnapshotBuilderAssembly);
        if (!File.Exists(builder))
        {
            throw new HostingException($"{builder} is missing. Unpack the full release package again.");
        }

        progress?.Report(new StepProgress(1, Steps, $"Reading the packet key table from {settings.ClientExecutable}..."));
        var extraction = KeyTableExtractor.Extract(File.ReadAllBytes(Path.Combine(settings.ClientFolder, settings.ClientExecutable)));
        Directory.CreateDirectory(paths.ServerData);
        var keyTablePath = paths.KeyTable;
        if (extraction.Table is not null)
        {
            File.WriteAllBytes(keyTablePath, extraction.Table);
        }
        else
        {
            throw new HostingException($"Could not read the packet key table from {settings.ClientExecutable}: {extraction.Outcome} Make sure the folder holds an unmodified KR 2014 client.");
        }

        progress?.Report(new StepProgress(2, Steps, "Reading maps, NPC points, portals and monster tables from the client..."));
        var summary = ServerDataGenerator.Generate(libconfig, mapFolder, paths.ServerData);
        progress?.Report(new StepProgress(2, Steps, $"{summary.Maps} maps, {summary.Portals} portals on {summary.PortalMaps} maps, {summary.PoolRows} monster rows on {summary.PoolMaps} maps."));

        var routing = Path.Combine(paths.ServerData, ZoneRoutingFile);
        if (!File.Exists(routing))
        {
            File.WriteAllText(routing, ZoneRouting);
        }

        progress?.Report(new StepProgress(3, Steps, "Building the world snapshot (map geometry and attack timings)..."));
        var result = ToolRunner.Capture(builder, new[]
        {
            "--variant", SnapshotVariant,
            "--data-dir", paths.ServerData,
            "--libconfig", libconfig,
            "--md3-dir", mapFolder,
            "--output", Path.Combine(paths.ServerData, "cache", SnapshotVariant + "-world.snapshot"),
        });
        if (result.ExitCode != 0)
        {
            throw new HostingException($"Building the world snapshot failed (exit {result.ExitCode}):{Environment.NewLine}{result.Output}");
        }

        File.WriteAllText(Path.Combine(paths.ServerData, DataVersionFile), Stamp(libconfig));
    }

    public static bool IsCurrent(InstallPaths paths, HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        var stamp = Path.Combine(paths.ServerData, DataVersionFile);
        if (!IsCurrent(paths))
        {
            return false;
        }

        var lines = File.ReadAllLines(stamp);
        var libconfig = ClientFolder.LibConfigFor(settings);
        return lines.Length > 1 && File.Exists(libconfig) && string.Equals(lines[1].Trim(), LibConfigHash(libconfig), StringComparison.Ordinal);
    }

    public static bool IsCurrent(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var stamp = Path.Combine(paths.ServerData, DataVersionFile);
        return File.Exists(stamp)
            && File.ReadLines(stamp).FirstOrDefault() is { } first
            && int.TryParse(first.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var version)
            && version == DataVersion;
    }

    private static string Stamp(string libconfig) =>
        DataVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + LibConfigHash(libconfig) + "\n";

    private static string LibConfigHash(string libconfig)
    {
        using var stream = File.OpenRead(libconfig);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
    }
}
