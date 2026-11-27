namespace tPoseidon.Hosting.Updates;

public static class PackageSwap
{
    public const int MoveAttempts = 5;

    public static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(400);

    private static readonly string[] KeepList =
    [
        "tposeidon.json",
        "tposeidon.json.tmp",
        "server-data",
        "log",
        "backups",
        "updates",
        "db",
        "pgsql",
        "run",
        "launcher-signing-key.json",
        "launcher-history.json",
        UpdateWork.FolderName,
    ];

    public static bool IsKept(string name) => KeepList.Contains(name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<SwapEntry> Plan(string root, string staging)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(staging);
        var entries = new List<SwapEntry>();
        foreach (var path in Directory.EnumerateFileSystemEntries(staging).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);
            if (IsKept(name))
            {
                continue;
            }

            var target = Path.Combine(root, name);
            entries.Add(new SwapEntry(name, File.Exists(target) || Directory.Exists(target), Directory.Exists(path)));
        }

        return entries;
    }

    public static void Apply(string root, string staging, string previous, IReadOnlyList<SwapEntry> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        UpdateWork.DeleteFolder(previous);
        Directory.CreateDirectory(previous);
        foreach (var entry in plan)
        {
            var current = Path.Combine(root, entry.Name);
            var kept = Path.Combine(previous, entry.Name);
            var fresh = Path.Combine(staging, entry.Name);
            if (entry.HadPrevious)
            {
                Move(current, kept, entry.IsDirectory);
            }

            Move(fresh, current, entry.IsDirectory);
        }
    }

    public static void Undo(string root, string staging, string previous, IReadOnlyList<SwapEntry> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var entry in plan.Reverse())
        {
            var current = Path.Combine(root, entry.Name);
            var kept = Path.Combine(previous, entry.Name);
            var fresh = Path.Combine(staging, entry.Name);
            var oldIsMovedOut = entry.HadPrevious && Exists(kept);
            var newIsInPlace = !Exists(fresh) && (Exists(current) && (oldIsMovedOut || !entry.HadPrevious));
            if (newIsInPlace)
            {
                Delete(current);
            }

            if (oldIsMovedOut)
            {
                Move(kept, current, entry.IsDirectory);
            }
        }
    }

    private static bool IsRetryable(Exception ex) =>
        ex is UnauthorizedAccessException || (ex is IOException && ex is not (FileNotFoundException or DirectoryNotFoundException));

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            UpdateWork.DeleteFolder(path);
        }
        else if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
    }

    private static void Move(string from, string to, bool directory)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (directory)
                {
                    Directory.Move(from, to);
                }
                else
                {
                    File.Move(from, to, overwrite: true);
                }

                return;
            }
            catch (Exception ex) when (attempt < MoveAttempts && IsRetryable(ex))
            {
                Thread.Sleep(MoveRetryDelay);
            }
        }
    }
}
