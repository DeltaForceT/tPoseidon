using System.Text.Json;
using tPoseidon.Core.Common.Launcher;

namespace tPoseidon.Hosting;

public sealed record PublishRecord(
    DateTimeOffset Time,
    string? Note,
    int NewsPosts,
    bool BannerChanged,
    int Added,
    int Changed,
    int Removed,
    int TotalFiles,
    long TotalBytes)
{
    public static PublishRecord From(DateTimeOffset time, string? note, LauncherManifest manifest, ManifestChanges changes) =>
        new(time, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), changes.NewsPosts, changes.BannerChanged, changes.Added, changes.Changed, changes.Removed, manifest.Files.Count, manifest.TotalBytes);
}

public static class PublishHistory
{
    public const int MaxEntries = 200;
    public const int MaxNoteLength = 200;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IReadOnlyList<PublishRecord> Load(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Read(paths).OrderByDescending(record => record.Time).ToList();
    }

    public static void Append(InstallPaths paths, PublishRecord record)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(record);
        var records = Read(paths).Append(record).OrderBy(entry => entry.Time).TakeLast(MaxEntries).ToList();
        try
        {
            Directory.CreateDirectory(paths.Root);
            var temporary = paths.LauncherHistory + ".tmp";
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(records, Options));
            File.Move(temporary, paths.LauncherHistory, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"Could not save the publish history to {paths.LauncherHistory}: {ex.Message}", ex);
        }
    }

    private static List<PublishRecord> Read(InstallPaths paths)
    {
        if (!File.Exists(paths.LauncherHistory))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PublishRecord>>(File.ReadAllText(paths.LauncherHistory), Options) ?? [];
        }
        catch (JsonException ex)
        {
            throw new HostingException($"The publish history {paths.LauncherHistory} is not valid: {ex.Message}", "Fix or delete that file.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"Could not read the publish history {paths.LauncherHistory}: {ex.Message}", ex);
        }
    }
}
