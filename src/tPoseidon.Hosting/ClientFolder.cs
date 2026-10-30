using System.Collections.Concurrent;
using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting;

public sealed record ClientCheck(bool Ok, string Executable, IReadOnlyList<string> Problems);

public sealed record ClientFileCopy(string Source, string Target);

public sealed record ClientLaunchPlan(string Service, string LibConfig, IReadOnlyList<ClientFileCopy> Copies, string Explanation);

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

    private static readonly (string Target, string Source)[] KoreanModeFiles =
    {
        ("libconfig.dat", "libconfig_eng.dat"),
        ("libconfig.idx", "libconfig_eng.idx"),
    };

    public static ClientLaunchPlan Plan(string clientFolder, string configuredService)
    {
        var data = DataFolder(clientFolder);
        var english = ClientFiles.Locate(Path.Combine(data, "libconfig_eng.dat"));
        var korean = ClientFiles.Locate(Path.Combine(data, "libconfig.dat"));
        var automatic = string.IsNullOrWhiteSpace(configuredService) || string.Equals(configuredService, AutomaticService, StringComparison.OrdinalIgnoreCase);
        var service = automatic ? DetectService(english, korean) : configuredService;
        var englishMode = string.Equals(service, EnglishService, StringComparison.OrdinalIgnoreCase);
        var files = englishMode ? EnglishModeFiles : KoreanModeFiles;
        var copies = MissingFiles(data, files);
        var libconfigTarget = englishMode ? "libconfig_eng.dat" : "libconfig.dat";
        var libconfig = (englishMode ? english : korean)
            ?? copies.FirstOrDefault(copy => string.Equals(Path.GetFileName(copy.Target), libconfigTarget, StringComparison.OrdinalIgnoreCase))?.Source
            ?? Path.Combine(data, libconfigTarget);
        return new ClientLaunchPlan(service, libconfig, copies, Explain(automatic, englishMode, english, korean, libconfig));
    }

    public static ClientLaunchPlan Plan(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Plan(settings.ClientFolder, settings.Launch.ServiceName);
    }

    public static ClientLaunchPlan Prepare(HostSettings settings)
    {
        var plan = Plan(settings);
        foreach (var copy in plan.Copies)
        {
            CopyFile(copy);
        }

        return plan;
    }

    public static void CopyFile(ClientFileCopy copy)
    {
        ArgumentNullException.ThrowIfNull(copy);
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

    private static List<ClientFileCopy> MissingFiles(string data, (string Target, string Source)[] files)
    {
        var copies = new List<ClientFileCopy>();
        foreach (var (target, source) in files)
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

    private static string DetectService(string? english, string? korean)
    {
        var sample = english ?? korean;
        if (sample is null)
        {
            return KoreanService;
        }

        var file = new FileInfo(sample);
        var key = $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
        return DetectedServices.GetOrAdd(key, _ => IsKoreanText(LibConfigDatDecryptor.DecryptFile(file.FullName)) ? KoreanService : EnglishService);
    }

    private static string Explain(bool automatic, bool englishMode, string? english, string? korean, string libconfig)
    {
        var mode = englishMode ? "English" : "Korean";
        var file = Path.GetFileName(libconfig);
        var chosen = automatic ? "chosen from the game data" : "set in the panel";
        var origin = englishMode ? english : korean;
        var copied = origin is null ? $", made as a copy of {file}" : "";
        return $"Client mode: {mode} ({chosen}); the client and the servers read {(englishMode ? "libconfig_eng.dat" : "libconfig.dat")}{copied}.";
    }

    public static string DataFolder(string clientFolder) =>
        LocateDirectory(clientFolder, DataFolderName) ?? Path.Combine(clientFolder, DataFolderName);

    public static string MapFolder(string clientFolder)
    {
        var data = DataFolder(clientFolder);
        return LocateDirectory(data, "map") ?? Path.Combine(data, "map");
    }

    public static string LibConfigFor(string clientFolder, string serviceName) =>
        Plan(clientFolder, serviceName).LibConfig;

    public static string LibConfigFor(HostSettings settings) => Plan(settings).LibConfig;

    public static string Service(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ServiceFor(settings.ClientFolder, settings.Launch.ServiceName);
    }

    public static string ServiceFor(string clientFolder, string configuredService) =>
        Plan(clientFolder, configuredService).Service;

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
