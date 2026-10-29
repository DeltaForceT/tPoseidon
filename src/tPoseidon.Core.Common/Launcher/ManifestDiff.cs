namespace tPoseidon.Core.Common.Launcher;

public enum FileState
{
    Added,
    Changed,
    Unchanged,
    Removed,
}

public sealed record FileChange(string Path, string? Group, long Size, FileState State);

public sealed record ManifestChanges(IReadOnlyList<FileChange> Files, bool BannerChanged, int NewsPosts)
{
    public int Added => Count(FileState.Added);

    public int Changed => Count(FileState.Changed);

    public int Removed => Count(FileState.Removed);

    public int Unchanged => Count(FileState.Unchanged);

    public static ManifestChanges Compare(LauncherManifest? previous, LauncherManifest current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var before = (previous?.Files ?? []).ToDictionary(Key, StringComparer.Ordinal);
        var files = new List<FileChange>();
        foreach (var file in current.Files)
        {
            var state = !before.Remove(Key(file), out var old)
                ? FileState.Added
                : string.Equals(old.Sha256, file.Sha256, StringComparison.Ordinal) ? FileState.Unchanged : FileState.Changed;
            files.Add(new FileChange(file.Path, file.Group, file.Size, state));
        }

        files.AddRange(before.Values.Select(file => new FileChange(file.Path, file.Group, file.Size, FileState.Removed)));
        var bannerChanged = !string.Equals(previous?.BannerSha256, current.BannerSha256, StringComparison.Ordinal);
        return new ManifestChanges(files, bannerChanged, current.News.Count);
    }

    public string Describe() =>
        string.Join(", ", NewsPosts == 1 ? "1 news post" : $"{NewsPosts} news posts", BannerChanged ? "banner changed" : "banner unchanged", (Added == 1 ? "1 file added" : $"{Added} files added"), $"{Changed} changed", $"{Removed} removed");

    private static string Key(ManifestFile file) => (file.Group ?? "") + "\n" + file.Path;

    private int Count(FileState state) => Files.Count(file => file.State == state);
}
