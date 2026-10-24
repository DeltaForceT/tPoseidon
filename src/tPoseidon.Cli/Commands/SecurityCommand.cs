using System.Globalization;
using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Security;

namespace tPoseidon.Cli.Commands;

internal static class SecurityCommand
{
    private static readonly (string Key, string Label)[] CounterLabels =
    [
        ("accepted", "accepted"),
        ("refusedBanned", "refused (banned)"),
        ("refusedAddressRate", "refused (connection rate)"),
        ("refusedAddressLimit", "refused (connections per address)"),
        ("refusedServerFull", "refused (server full)"),
        ("refusedTooManyPending", "refused (too many logging in)"),
        ("handshakeTimeouts", "closed (no valid frame)"),
        ("loginTimeouts", "closed (no login)"),
        ("idleTimeouts", "closed (idle)"),
        ("partialFrames", "closed (partial frame)"),
        ("oversizedFrames", "closed (oversized frame)"),
        ("malformedFrames", "closed (malformed frame)"),
        ("packetFloods", "closed (packet flood)"),
        ("loginFailures", "failed logins"),
        ("lockouts", "login lockouts"),
        ("lockedOutAttempts", "logins refused while locked out"),
        ("bans", "auto-bans"),
        ("banKicks", "sessions closed by bans"),
        ("requestsRefused", "web requests refused"),
    ];

    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        var action = commandLine.Positionals.Count > 0 ? commandLine.Positionals[0] : "status";
        var target = commandLine.Positionals.Count > 1 ? commandLine.Positionals[1] : null;
        if (commandLine.Positionals.Count > 2)
        {
            throw new CliException($"Unexpected argument '{commandLine.Positionals[2]}'.");
        }

        return action switch
        {
            "status" => Status(paths, commandLine, target),
            "ban" => Ban(paths, commandLine, Required(target, "ban")),
            "unban" => Unban(paths, commandLine, Required(target, "unban")),
            "allow" => Allow(paths, commandLine, Required(target, "allow")),
            "disallow" => Disallow(paths, commandLine, Required(target, "disallow")),
            _ => throw new CliException($"Unknown security command '{action}'. Run 'tposeidon security --help'."),
        };
    }

    public static int Help()
    {
        Console.WriteLine("tposeidon security [status|ban|unban|allow|disallow] [address]");
        Console.WriteLine();
        Console.WriteLine("The connection guard in every server limits connections, frames and logins per address and");
        Console.WriteLine("bans addresses that keep breaking the limits. Changes here reach running servers within seconds.");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  status                      what each server's guard sees, blocked addresses, recent incidents");
        Console.WriteLine("  ban <address|range>         block an address or range like 203.0.113.0/24 on every server");
        Console.WriteLine("      [--minutes <n>]         for n minutes (default: until you unban it)");
        Console.WriteLine("      [--reason <text>]       a note kept with the ban");
        Console.WriteLine("  unban <address|range>       lift a manual ban and any automatic ban on that address");
        Console.WriteLine("  allow <address|range>       always allow an address (never limited or banned)");
        Console.WriteLine("      [--reason <text>]");
        Console.WriteLine("  disallow <address|range>    take an address off the allow list");
        Console.WriteLine();
        Console.WriteLine("Loopback and the security.trustedAddresses in tposeidon.json are always allowed.");
        return 0;
    }

    private static string Required(string? target, string action) =>
        string.IsNullOrWhiteSpace(target)
            ? throw new CliException($"Say which address: tposeidon security {action} <address|range>")
            : target.Trim();

    private static int Status(InstallPaths paths, CommandLine commandLine, string? target)
    {
        commandLine.RejectUnknown();
        if (target is not null)
        {
            throw new CliException($"Unexpected argument '{target}'.");
        }

        var now = DateTimeOffset.UtcNow;
        var overview = SecurityState.Read(paths, now);
        Console.WriteLine($"Connection guard status ({overview.Folder})");
        Console.WriteLine();
        if (overview.Servers.Count == 0)
        {
            Console.WriteLine("  No server has reported yet. Start the servers with 'tposeidon start'.");
        }

        foreach (var server in overview.Servers)
        {
            var snapshot = server.Snapshot;
            var state = server.Running
                ? string.Create(CultureInfo.InvariantCulture, $"{snapshot.Connections} connections ({snapshot.Pending} logging in, {snapshot.TrustedConnections} trusted)")
                : $"not running (last report {Local(snapshot.UpdatedAt)})";
            Console.WriteLine($"  {snapshot.Server,-12} {state}");
            var counts = CounterLabels
                .Where(label => snapshot.Counters.TryGetValue(label.Key, out var value) && value > 0 && label.Key != "accepted")
                .Select(label => string.Create(CultureInfo.InvariantCulture, $"{label.Label} {snapshot.Counters[label.Key]}"))
                .ToList();
            var accepted = snapshot.Counters.GetValueOrDefault("accepted");
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {"",-12} accepted {accepted}{(counts.Count > 0 ? "; " + string.Join("; ", counts) : "; nothing refused or closed")}"));
        }

        Console.WriteLine();
        Console.WriteLine("Blocked addresses:");
        if (overview.Blocked.Count == 0)
        {
            Console.WriteLine("  none");
        }

        foreach (var blocked in overview.Blocked.OrderBy(b => b.Until ?? DateTimeOffset.MaxValue))
        {
            var until = blocked.Until is { } end ? $"until {Local(end)}" : "until unbanned";
            var source = blocked.Manual ? "manual" : "auto, " + string.Join(", ", blocked.Servers);
            var reason = string.IsNullOrWhiteSpace(blocked.Reason) ? "" : $" - {blocked.Reason}";
            Console.WriteLine($"  {blocked.Address,-20} {until,-27} {source}{reason}");
        }

        Console.WriteLine();
        Console.WriteLine("Always allowed: loopback"
            + string.Concat(overview.Trusted.Select(text => ", " + text + " (settings)"))
            + string.Concat(overview.Allowed.Select(entry => ", " + entry.Address)));

        Console.WriteLine();
        Console.WriteLine("Recent incidents:");
        if (overview.Incidents.Count == 0)
        {
            Console.WriteLine("  none");
        }

        foreach (var incident in overview.Incidents.Take(15))
        {
            Console.WriteLine($"  {Local(incident.At)}  {incident.Server,-10} {incident.Address,-16} {incident.Kind}: {incident.Detail}");
        }

        return 0;
    }

    private static int Ban(InstallPaths paths, CommandLine commandLine, string target)
    {
        commandLine.RejectUnknown("minutes", "reason");
        TimeSpan? length = null;
        if (commandLine.Option("minutes") is { } minutesText)
        {
            length = int.TryParse(minutesText, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
                ? TimeSpan.FromMinutes(minutes)
                : throw new CliException($"'{minutesText}' is not a number of minutes.");
        }

        var ban = SecurityState.Ban(paths, target, length, commandLine.Option("reason") ?? "", DateTimeOffset.UtcNow);
        Console.WriteLine($"Banned {ban.Address} {(ban.Until is { } until ? "until " + Local(until) : "until you unban it")}. Running servers close its sessions within a few seconds.");
        return 0;
    }

    private static int Unban(InstallPaths paths, CommandLine commandLine, string target)
    {
        commandLine.RejectUnknown();
        var result = SecurityState.Unban(paths, target, DateTimeOffset.UtcNow);
        var parts = new List<string>();
        if (result.ManualRemoved > 0)
        {
            parts.Add("removed the manual ban");
        }

        if (result.AutoLifted > 0)
        {
            parts.Add("lifted the automatic ban");
        }

        Console.WriteLine(parts.Count == 0
            ? $"{SecurityState.Parse(target)} was not banned; its strikes and login lockouts are cleared anyway."
            : $"{SecurityState.Parse(target)}: {string.Join(" and ", parts)}. Running servers pick this up within a few seconds.");
        return 0;
    }

    private static int Allow(InstallPaths paths, CommandLine commandLine, string target)
    {
        commandLine.RejectUnknown("reason");
        var added = SecurityState.Allow(paths, target, commandLine.Option("reason") ?? "", DateTimeOffset.UtcNow);
        Console.WriteLine(added
            ? $"{SecurityState.Parse(target)} is now always allowed."
            : $"{SecurityState.Parse(target)} was already on the allow list.");
        return 0;
    }

    private static int Disallow(InstallPaths paths, CommandLine commandLine, string target)
    {
        commandLine.RejectUnknown();
        var removed = SecurityState.Disallow(paths, target);
        Console.WriteLine(removed > 0
            ? $"{SecurityState.Parse(target)} is off the allow list."
            : $"{SecurityState.Parse(target)} was not on the allow list.");
        return 0;
    }

    private static string Local(DateTimeOffset time) =>
        time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
