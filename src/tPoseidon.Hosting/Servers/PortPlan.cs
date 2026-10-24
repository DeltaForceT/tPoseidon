using System.Globalization;
using System.Net;

namespace tPoseidon.Hosting.Servers;

public sealed record PortIssue(IReadOnlyList<string> Servers, string Summary, string Hint);

public static class PortPlan
{
    public const int Lowest = 1024;

    public static PortIssue? Validate(IReadOnlyList<(string Server, string Text)> entries, Func<int, bool> inUse)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(inUse);
        var parsed = new List<(string Server, int Port)>();
        foreach (var (server, text) in entries)
        {
            var trimmed = text.Trim();
            if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < Lowest or > IPEndPoint.MaxPort)
            {
                return new PortIssue(new[] { server }, $"'{trimmed}' is not a usable port for {server}.", $"Use a number from {Lowest} to {IPEndPoint.MaxPort}.");
            }

            parsed.Add((server, port));
        }

        var duplicate = parsed.GroupBy(entry => entry.Port).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            var names = duplicate.Select(entry => entry.Server).ToList();
            return new PortIssue(names, $"{string.Join(" and ", names)} use the same port ({duplicate.Key}).", "Every server needs its own port.");
        }

        var busy = parsed.FirstOrDefault(entry => inUse(entry.Port));
        if (busy.Server is not null)
        {
            return new PortIssue(new[] { busy.Server }, $"Port {busy.Port} for {busy.Server} is already used by another program.", "Close that program, or choose another port.");
        }

        return null;
    }
}
