namespace tPoseidon.Core.Common;

public static class LogPaths
{
    public static string Scrub(string? path)
    {
        if (string.IsNullOrEmpty(path)) return path ?? "";

        var normalized = path.Replace('\\', '/').TrimEnd('/');

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 2) return path;
        return segments[^2] + "/" + segments[^1];
    }
}
