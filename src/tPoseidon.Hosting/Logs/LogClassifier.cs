using System.Text.RegularExpressions;

namespace tPoseidon.Hosting.Logs;

public enum LogSeverity
{
    Info,
    Warning,
    Error,
}

public static partial class LogClassifier
{
    public static LogSeverity Classify(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var withoutCounters = ZeroCounterRegex().Replace(text, string.Empty);
        if (ErrorRegex().IsMatch(withoutCounters))
        {
            return LogSeverity.Error;
        }

        return WarningRegex().IsMatch(withoutCounters) ? LogSeverity.Warning : LogSeverity.Info;
    }

    [GeneratedRegex(
        @"(?<![A-Za-z])(?:exception|unhandled|fatal|fail(?:ed|ure|ures|s)?|error|errors|crash(?:ed)?|panic)(?![A-Za-z])",
        RegexOptions.IgnoreCase)]
    private static partial Regex ErrorRegex();

    [GeneratedRegex(
        @"(?<![A-Za-z])(?:warn|warning|warnings|falling back|deprecated)(?![A-Za-z])",
        RegexOptions.IgnoreCase)]
    private static partial Regex WarningRegex();

    [GeneratedRegex(
        @"\b0\s+(?:failed|failures?|errors?|exceptions?|warnings?|unhandled)\b|\b(?:failed|failures?|errors?|exceptions?|warnings?|missing|unhandled)\s*[=:]\s*0\b|\bno\s+(?:errors?|failures?|exceptions?|warnings?)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ZeroCounterRegex();
}
