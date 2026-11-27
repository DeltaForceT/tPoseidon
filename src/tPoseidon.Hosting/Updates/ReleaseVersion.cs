using System.Globalization;

namespace tPoseidon.Hosting.Updates;

public enum ReleaseChannel
{
    Stable,
    Beta,
}

public sealed record ReleaseVersion(int Major, int Minor, int Patch, string Label) : IComparable<ReleaseVersion>
{
    public bool IsPrerelease => Label.Length > 0;

    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = new ReleaseVersion(0, 0, 0, "");
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.Length > 1 && (value[0] is 'v' or 'V') && char.IsDigit(value[1]))
        {
            value = value[1..];
        }

        var build = value.IndexOf('+', StringComparison.Ordinal);
        if (build >= 0)
        {
            value = value[..build];
        }

        var dash = value.IndexOf('-', StringComparison.Ordinal);
        var label = dash >= 0 ? value[(dash + 1)..] : "";
        var numbers = (dash >= 0 ? value[..dash] : value).Split('.');
        if (numbers.Length != 3 || (dash >= 0 && (label.Length == 0 || label.Split('.').Any(part => part.Length == 0))))
        {
            return false;
        }

        if (!int.TryParse(numbers[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(numbers[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(numbers[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch, label);
        return true;
    }

    public static ReleaseVersion Parse(string text) =>
        TryParse(text, out var version) ? version : throw new FormatException($"'{text}' is not a version like 0.90.0-beta.");

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byNumber = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (byNumber != 0)
        {
            return byNumber;
        }

        if (Label.Length == 0 || other.Label.Length == 0)
        {
            return Label.Length == other.Label.Length ? 0 : Label.Length == 0 ? 1 : -1;
        }

        return CompareLabels(Label.Split('.'), other.Label.Split('.'));
    }

    public override string ToString() => Label.Length == 0
        ? $"{Major}.{Minor}.{Patch}"
        : $"{Major}.{Minor}.{Patch}-{Label}";

    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) >= 0;

    private static int CompareLabels(string[] left, string[] right)
    {
        for (var index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            var leftNumeric = long.TryParse(left[index], NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightNumeric = long.TryParse(right[index], NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
            var result = (leftNumeric, rightNumeric) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(left[index], right[index]),
            };
            if (result != 0)
            {
                return Math.Sign(result);
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}
