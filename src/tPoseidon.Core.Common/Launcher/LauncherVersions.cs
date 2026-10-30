using System.Globalization;

namespace tPoseidon.Core.Common.Launcher;

public static class LauncherVersions
{
    public const char BuildSeparator = '+';
    public const char LabelSeparator = '-';
    public const string BuildStampFormat = "yyyyMMddHHmmss";

    public static string WithBuild(string version, DateTimeOffset builtAt) =>
        version + BuildSeparator + builtAt.UtcDateTime.ToString(BuildStampFormat, CultureInfo.InvariantCulture);

    public static bool IsNewer(string candidate, string current) => Compare(candidate, current) > 0;

    public static int Compare(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var (leftNumbers, leftBuild) = Split(left);
        var (rightNumbers, rightBuild) = Split(right);
        var length = Math.Max(leftNumbers.Count, rightNumbers.Count);
        for (var index = 0; index < length; index++)
        {
            var byNumber = At(leftNumbers, index).CompareTo(At(rightNumbers, index));
            if (byNumber != 0)
            {
                return byNumber;
            }
        }

        return CompareBuilds(leftBuild, rightBuild);
    }

    private static int CompareBuilds(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return left is null ? (right is null ? 0 : -1) : 1;
        }

        return long.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftStamp) && long.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightStamp)
            ? leftStamp.CompareTo(rightStamp)
            : string.CompareOrdinal(left, right);
    }

    private static long At(List<long> numbers, int index) => index < numbers.Count ? numbers[index] : 0;

    private static (List<long> Numbers, string? Build) Split(string version)
    {
        var text = version.Trim();
        string? build = null;
        var plus = text.IndexOf(BuildSeparator, StringComparison.Ordinal);
        if (plus >= 0)
        {
            build = text[(plus + 1)..];
            text = text[..plus];
        }

        var dash = text.IndexOf(LabelSeparator, StringComparison.Ordinal);
        if (dash >= 0)
        {
            text = text[..dash];
        }

        var numbers = text.Split('.').Select(part => long.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0).ToList();
        return (numbers, build);
    }
}
