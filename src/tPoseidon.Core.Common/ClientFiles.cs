using System.Collections.Concurrent;

namespace tPoseidon.Core.Common;

public static class ClientFiles
{
    private static readonly char[] Separators = { '\\', '/' };
    private static readonly ConcurrentDictionary<string, string[]> Listings = new(StringComparer.Ordinal);

    public static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    public static string Combine(string root, string relative)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(relative);
        return Path.Combine(root, Normalize(relative).TrimStart(Path.DirectorySeparatorChar));
    }

    public static string? Locate(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (File.Exists(path)) return path;
        var normalized = Normalize(path);
        if (File.Exists(normalized)) return normalized;

        var root = Path.GetPathRoot(normalized);
        var current = string.IsNullOrEmpty(root) ? Directory.GetCurrentDirectory() : root;
        var rest = string.IsNullOrEmpty(root) ? normalized : normalized[root.Length..];
        var segments = rest.Split(Separators.Append(Path.DirectorySeparatorChar).ToArray(), StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var exact = Path.Combine(current, segment);
            var last = i == segments.Length - 1;
            if (last ? File.Exists(exact) : Directory.Exists(exact))
            {
                current = exact;
                continue;
            }
            var match = EntriesOf(current).FirstOrDefault(entry => string.Equals(Path.GetFileName(entry), segment, StringComparison.OrdinalIgnoreCase)
                && (last ? File.Exists(entry) : Directory.Exists(entry)));
            if (match is null) return null;
            current = match;
        }
        return current;
    }

    public static bool Exists(string path) => Locate(path) is not null;

    private static string[] EntriesOf(string directory) =>
        Listings.GetOrAdd(directory, dir => Directory.Exists(dir) ? Directory.GetFileSystemEntries(dir) : Array.Empty<string>());
}
