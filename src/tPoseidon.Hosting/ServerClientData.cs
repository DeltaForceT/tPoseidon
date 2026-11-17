using System.Globalization;

namespace tPoseidon.Hosting;

public static class ServerClientData
{
    public const string FolderName = "client-data";
    public const string StampFile = "source.txt";

    private static readonly string[] LibConfigNames = ["libconfig.dat", "libconfig_eng.dat"];

    public static string Folder(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.ServerData, FolderName);
    }

    public static string LibConfig(InstallPaths paths, HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Path.Combine(Folder(paths), Path.GetFileName(ClientFolder.LibConfigFor(settings)));
    }

    public static string LibConfigOrClient(HostSettings settings) => LibConfigOrClient(InstallPaths.Resolve(null), settings);

    public static string LibConfigOrClient(InstallPaths paths, HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var copy = LibConfig(paths, settings);
        return File.Exists(copy) ? copy : ClientFolder.LibConfigFor(settings);
    }

    public static string Ensure(InstallPaths paths, HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var source = ClientFolder.LibConfigFor(settings);
        var target = LibConfig(paths, settings);
        if (!File.Exists(source))
        {
            return target;
        }

        var folder = Folder(paths);
        Directory.CreateDirectory(folder);
        var stampPath = Path.Combine(folder, StampFile);
        if (File.Exists(target) && File.Exists(stampPath) && string.Equals(File.ReadAllText(stampPath), Stamp(source), StringComparison.Ordinal))
        {
            return target;
        }

        foreach (var stale in Directory.EnumerateFiles(folder).Where(file => !string.Equals(Path.GetFileName(file), StampFile, StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(stale);
        }

        foreach (var file in Companions(source))
        {
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)), overwrite: true);
        }

        File.WriteAllText(stampPath, Stamp(source));
        return target;
    }

    public static bool ClientChanged(HostSettings settings) => ChangedLibConfigs(settings).Any();

    public static ClientDataOutcome RestoreClient(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var executable = Path.Combine(settings.ClientFolder, settings.ClientExecutable);
        var outcomes = ChangedLibConfigs(settings).Select(libconfig => ShadowSanctuaryClientData.Restore(libconfig, executable)).ToList();
        return outcomes.Count == 0
            ? new ClientDataOutcome(false, "The client's libconfig files are the originals.")
            : new ClientDataOutcome(outcomes.Any(outcome => outcome.Changed), string.Join(" ", outcomes.Select(outcome => outcome.Message)));
    }

    private static IEnumerable<string> ChangedLibConfigs(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var data = ClientFolder.DataFolder(settings.ClientFolder);
        return LibConfigNames
            .Select(name => Path.Combine(data, name))
            .Where(ShadowSanctuaryClientData.CanRestore);
    }

    private static IEnumerable<string> Companions(string libconfig)
    {
        var index = ShadowSanctuaryClientData.IndexPath(libconfig);
        return new[]
        {
            libconfig,
            index,
            libconfig + ClientAddressPatch.BackupSuffix,
            index + ClientAddressPatch.BackupSuffix,
        }.Where(File.Exists);
    }

    private static string Stamp(string libconfig)
    {
        var backup = libconfig + ClientAddressPatch.BackupSuffix;
        var origin = new FileInfo(File.Exists(backup) ? backup : libconfig);
        return string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(libconfig)}|{origin.Length}|{origin.LastWriteTimeUtc.Ticks}");
    }
}
