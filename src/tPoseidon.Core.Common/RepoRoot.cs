namespace tPoseidon.Core.Common;

public static class RepoRoot
{
    private static readonly Lazy<string> _resolved = new(Resolve);
    private static readonly Lazy<string> _dataDir = new(ResolveDataDir);

        public static string Path => _resolved.Value;

        public static string DataDir => _dataDir.Value;

        public static string Data(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var parts = new string[segments.Length + 1];
        parts[0] = DataDir;
        segments.CopyTo(parts, 1);
        return System.IO.Path.Combine(parts);
    }

    private static string Resolve()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "tPoseidon.sln")))
            {
                return dir.FullName;
            }
        }
        return System.AppContext.BaseDirectory;
    }

    private static string ResolveDataDir()
    {
        var envOverride = System.Environment.GetEnvironmentVariable("MT_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return envOverride.Trim();
        }
        return System.IO.Path.Combine(Path, "data");
    }
}
