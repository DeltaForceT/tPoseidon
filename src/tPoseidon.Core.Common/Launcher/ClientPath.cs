namespace tPoseidon.Core.Common.Launcher;

public static class ClientPath
{
    public const int MaxLength = 260;
    public const string WorkFolder = ".tposeidon-update";
    public const string BackupFolder = ".tposeidon-backup";

    public static readonly IReadOnlyList<string> ProtectedNames =
    [
        "Trickster.bin",
        "Trickster.exe",
        "Trickster.bin.tposeidon-original",
    ];

    public static bool TryNormalize(string? relative, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrEmpty(relative) || relative.Length > MaxLength || relative.Any(char.IsControl))
        {
            return false;
        }

        if (relative.Contains('\\', StringComparison.Ordinal) || relative.Contains(':', StringComparison.Ordinal) || relative.StartsWith('/'))
        {
            return false;
        }

        var segments = relative.Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' '))
            {
                return false;
            }
        }

        if (string.Equals(segments[0], WorkFolder, StringComparison.OrdinalIgnoreCase) || string.Equals(segments[0], BackupFolder, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (segments.Length == 1 && ProtectedNames.Contains(segments[0], StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = relative;
        return true;
    }

    public static string Resolve(string root, string relative)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!TryNormalize(relative, out var normalized))
        {
            throw new InvalidDataException($"'{relative}' is not a path inside the client folder.");
        }

        var fullRoot = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(fullRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{relative}' is not a path inside the client folder.");
        }

        return full;
    }
}
