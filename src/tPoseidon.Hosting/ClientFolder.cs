using tPoseidon.Core.Common;

namespace tPoseidon.Hosting;

public sealed record ClientCheck(bool Ok, string Executable, IReadOnlyList<string> Problems);

public static class ClientFolder
{
    private const string DataFolderName = "data";
    private const string EnglishServiceName = "gsp";
    private static readonly string[] ExecutableCandidates = { "Trickster.bin", "Trickster.exe" };

    public static string DataFolder(string clientFolder) =>
        LocateDirectory(clientFolder, DataFolderName) ?? Path.Combine(clientFolder, DataFolderName);

    public static string MapFolder(string clientFolder)
    {
        var data = DataFolder(clientFolder);
        return LocateDirectory(data, "map") ?? Path.Combine(data, "map");
    }

    public static string LibConfigFor(string clientFolder, string serviceName)
    {
        var data = DataFolder(clientFolder);
        var english = ClientFiles.Locate(Path.Combine(data, "libconfig_eng.dat"));
        var korean = ClientFiles.Locate(Path.Combine(data, "libconfig.dat"));
        if (string.Equals(serviceName, EnglishServiceName, StringComparison.OrdinalIgnoreCase) && english is not null)
        {
            return english;
        }

        return korean ?? english ?? Path.Combine(data, "libconfig.dat");
    }

    public static ClientCheck Check(string clientFolder)
    {
        var problems = new List<string>();
        if (!Directory.Exists(clientFolder))
        {
            return new ClientCheck(false, "", new[] { $"{clientFolder} does not exist." });
        }

        var executable = ExecutableCandidates
            .Select(name => ClientFiles.Locate(Path.Combine(clientFolder, name)))
            .FirstOrDefault(found => found is not null);
        if (executable is null)
        {
            problems.Add("no Trickster.bin or Trickster.exe in the folder");
        }

        var data = LocateDirectory(clientFolder, DataFolderName);
        if (data is null)
        {
            problems.Add("no data folder");
        }
        else
        {
            if (ClientFiles.Locate(Path.Combine(data, "libconfig.dat")) is null && ClientFiles.Locate(Path.Combine(data, "libconfig_eng.dat")) is null)
            {
                problems.Add("no data/libconfig.dat or data/libconfig_eng.dat");
            }

            if (LocateDirectory(data, "map") is null)
            {
                problems.Add("no data/map folder");
            }
        }

        return new ClientCheck(problems.Count == 0, executable is null ? "" : Path.GetFileName(executable), problems);
    }

    public static void EnsureUsable(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var check = Check(settings.ClientFolder);
        if (!check.Ok)
        {
            throw new HostingException($"The client folder {settings.ClientFolder} is not usable ({string.Join("; ", check.Problems)}). Run tposeidon setup again.");
        }
    }

    private static string? LocateDirectory(string parent, string name)
    {
        if (!Directory.Exists(parent))
        {
            return null;
        }

        var exact = Path.Combine(parent, name);
        if (Directory.Exists(exact))
        {
            return exact;
        }

        return Directory.EnumerateDirectories(parent)
            .FirstOrDefault(directory => string.Equals(Path.GetFileName(directory), name, StringComparison.OrdinalIgnoreCase));
    }
}
