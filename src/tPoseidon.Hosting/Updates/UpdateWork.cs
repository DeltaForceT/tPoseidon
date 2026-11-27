using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Hosting.Updates;

public sealed class UpdateWork
{
    public const string FolderName = ".update";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public UpdateWork(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Paths = paths;
    }

    public InstallPaths Paths { get; }

    public string Folder => Path.Combine(Paths.Root, FolderName);

    public string Download => Path.Combine(Folder, "download");

    public string Staging => Path.Combine(Folder, "staging");

    public string Previous => Path.Combine(Folder, "previous");

    public string PendingFile => Path.Combine(Folder, "pending.json");

    public string ResultFile => Path.Combine(Folder, "result.json");

    public string LogFile => Path.Combine(Folder, "update.log");

    public static T? Read<T>(string file)
        where T : class
    {
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(file), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Write<T>(string file, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
        File.Move(temporary, file, overwrite: true);
    }

    public void Log(string line)
    {
        Directory.CreateDirectory(Folder);
        File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {line}{Environment.NewLine}");
    }

    public static void DeleteFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(folder, recursive: true);
    }
}

public enum UpdatePhase
{
    Staged,
    Swapping,
    Finished,
}

public sealed record SwapEntry(string Name, bool HadPrevious, bool IsDirectory);

public sealed record PendingUpdate(
    string FromVersion,
    string ToVersion,
    UpdatePhase Phase,
    bool ServersWereRunning,
    string? BackupFile,
    string? Relaunch,
    IReadOnlyList<SwapEntry> Swapped);

public enum UpdateOutcome
{
    Updated,
    RolledBack,
    RollbackFailed,
    NotStarted,
}

public sealed record UpdateResult(UpdateOutcome Outcome, string FromVersion, string ToVersion, string Message, DateTimeOffset At, bool Acknowledged = false);
