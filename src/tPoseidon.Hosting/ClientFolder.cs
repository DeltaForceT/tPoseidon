using System.Collections.Concurrent;
using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting;

public sealed record ClientCheck(bool Ok, string Executable, IReadOnlyList<string> Problems);

public sealed record ClientFileCopy(string Source, string Target);

public static class ClientFolder
{
    public const string AutomaticService = "auto";
    public const string EnglishService = "gsp";
    public const string KoreanService = "korean";
    public const int KoreanTextCharacters = 1000;

    private const string DataFolderName = "data";
    private static readonly ConcurrentDictionary<string, string> DetectedServices = new(StringComparer.Ordinal);
    private static readonly string[] ExecutableCandidates = { "Trickster.bin", "Trickster.exe" };
    private static readonly (string Target, string Source)[] EnglishModeFiles =
    {
        ("libconfig_eng.dat", "libconfig.dat"),
        ("libconfig_eng.idx", "libconfig.idx"),
        ("tahoma12.fnt", "gulim12.fnt"),
    };

    public static IReadOnlyList<ClientFileCopy> MissingEnglishModeFiles(string clientFolder)
    {
        var data = DataFolder(clientFolder);
        var copies = new List<ClientFileCopy>();
        foreach (var (target, source) in EnglishModeFiles)
        {
            if (ClientFiles.Locate(Path.Combine(data, target)) is not null)
            {
                continue;
            }

            var found = ClientFiles.Locate(Path.Combine(data, source));
            if (found is not null)
            {
                copies.Add(new ClientFileCopy(found, Path.Combine(data, target)));
            }
        }

        return copies;
    }

    public static IReadOnlyList<ClientFileCopy> PrepareEnglishMode(string clientFolder)
    {
        var copies = MissingEnglishModeFiles(clientFolder);
        foreach (var copy in copies)
        {
            try
            {
                File.Copy(copy.Source, copy.Target, overwrite: false);
            }
            catch (IOException ex)
            {
                throw new HostingException($"Could not create {copy.Target} from {Path.GetFileName(copy.Source)}: {ex.Message}", "Close the game client and any program using the client folder, then try again.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new PlayFailure(PlayFailureReason.ClientNotWritable, $"Could not create {copy.Target}: {ex.Message}", "Move the client to a folder you can write to (not Program Files), or give your account write access to it.", ex);
            }
        }

        return copies;
    }

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
        if (string.Equals(serviceName, EnglishService, StringComparison.OrdinalIgnoreCase) && english is not null)
        {
            return english;
        }

        return korean ?? english ?? Path.Combine(data, "libconfig.dat");
    }

    public static string LibConfigFor(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return LibConfigFor(settings.ClientFolder, Service(settings));
    }

    public static string Service(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ServiceFor(settings.ClientFolder, settings.Launch.ServiceName);
    }

    public static string ServiceFor(string clientFolder, string configuredService)
    {
        if (!string.IsNullOrWhiteSpace(configuredService) && !string.Equals(configuredService, AutomaticService, StringComparison.OrdinalIgnoreCase))
        {
            return configuredService;
        }

        var data = DataFolder(clientFolder);
        var libconfig = ClientFiles.Locate(Path.Combine(data, "libconfig_eng.dat")) ?? ClientFiles.Locate(Path.Combine(data, "libconfig.dat"));
        if (libconfig is null)
        {
            return KoreanService;
        }

        var file = new FileInfo(libconfig);
        var key = $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
        return DetectedServices.GetOrAdd(key, _ => IsKoreanText(LibConfigDatDecryptor.DecryptFile(file.FullName)) ? KoreanService : EnglishService);
    }

    public static bool IsKoreanText(ReadOnlySpan<byte> text)
    {
        var found = 0;
        for (var i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] is >= 0xB0 and <= 0xC8 && text[i + 1] is >= 0xA1 and <= 0xFE)
            {
                if (++found >= KoreanTextCharacters)
                {
                    return true;
                }

                i++;
            }
        }

        return false;
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
            foreach (var (target, source) in EnglishModeFiles)
            {
                if (ClientFiles.Locate(Path.Combine(data, target)) is null && ClientFiles.Locate(Path.Combine(data, source)) is null)
                {
                    problems.Add($"no data/{target} or data/{source}");
                }
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
