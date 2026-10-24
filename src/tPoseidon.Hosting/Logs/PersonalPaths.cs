using System.Text.RegularExpressions;

namespace tPoseidon.Hosting.Logs;

public sealed partial class PersonalPaths
{
    public const string Replacement = "~";
    private const int ShortestProfile = 4;

    private readonly string[] _profileForms;

    public PersonalPaths(string? userProfile = null)
    {
        _profileForms = Forms(userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    public string Hide(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = text;
        foreach (var form in _profileForms)
        {
            result = result.Replace(form, Replacement, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        return OtherUserRegex().Replace(result, Replacement);
    }

    private static string[] Forms(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<string>();
        }

        var trimmed = path.TrimEnd('\\', '/');
        if (trimmed.Length < ShortestProfile)
        {
            return Array.Empty<string>();
        }

        var backslash = trimmed.Replace('/', '\\');
        var slash = trimmed.Replace('\\', '/');
        return string.Equals(backslash, slash, StringComparison.Ordinal) ? new[] { backslash } : new[] { backslash, slash };
    }

    [GeneratedRegex(@"[A-Za-z]:[\\/]Users[\\/][^\\/\s""'<>|]+|/home/[^/\s""'<>|]+|/Users/[^/\s""'<>|]+", RegexOptions.IgnoreCase)]
    private static partial Regex OtherUserRegex();
}
