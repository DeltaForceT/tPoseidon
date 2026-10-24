using System.Text.Json;
using tPoseidon.Core.Common.Security;

namespace tPoseidon.Hosting.Security;

public sealed record GuardServer(GuardSnapshot Snapshot, bool Running);

public sealed record BlockedAddress(string Address, bool Manual, DateTimeOffset Since, DateTimeOffset? Until, string Reason, IReadOnlyList<string> Servers);

public sealed record GuardIncidentLine(DateTimeOffset At, string Server, string Address, string Kind, string Detail);

public sealed record SecurityOverview(
    string Folder,
    IReadOnlyList<GuardServer> Servers,
    IReadOnlyList<BlockedAddress> Blocked,
    IReadOnlyList<string> Trusted,
    IReadOnlyList<LedgerAllow> Allowed,
    IReadOnlyList<GuardIncidentLine> Incidents);

public sealed record UnbanResult(int ManualRemoved, int AutoLifted);

public static class SecurityState
{
    public const int IncidentLines = 30;

    public static SecurityOverview Read(InstallPaths paths, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var snapshots = SecurityFiles.ReadSnapshots(paths.Security).Select(entry => entry.Snapshot).ToList();
        var ledger = LoadLedger(paths);
        ledger.Prune(now);
        var servers = snapshots.Select(snapshot => new GuardServer(snapshot, snapshot.IsFresh(now))).ToList();
        var blocked = new List<BlockedAddress>();
        foreach (var ban in ledger.Bans)
        {
            blocked.Add(new BlockedAddress(ban.Address, Manual: true, ban.Since, ban.Until, ban.Reason, []));
        }

        foreach (var group in snapshots
            .SelectMany(snapshot => snapshot.Bans.Where(ban => ban.Until > now).Select(ban => (snapshot.Server, Ban: ban)))
            .GroupBy(entry => entry.Ban.Address, StringComparer.OrdinalIgnoreCase))
        {
            blocked.Add(new BlockedAddress(
                group.Key,
                Manual: false,
                group.Min(entry => entry.Ban.Since),
                group.Max(entry => entry.Ban.Until),
                string.Join("; ", group.Select(entry => entry.Ban.Reason).Distinct(StringComparer.Ordinal)),
                group.Select(entry => entry.Server).Distinct(StringComparer.Ordinal).ToList()));
        }

        var incidents = snapshots
            .SelectMany(snapshot => snapshot.Incidents.Select(incident => new GuardIncidentLine(incident.At, snapshot.Server, incident.Address, incident.Kind, incident.Detail)))
            .OrderByDescending(line => line.At)
            .Take(IncidentLines)
            .ToList();
        var trusted = HostSettings.Load(paths)?.Security.TrustedAddresses ?? [];
        return new SecurityOverview(paths.Security, servers, blocked, trusted, ledger.Allow, incidents);
    }

    public static LedgerBan Ban(InstallPaths paths, string address, TimeSpan? length, string reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var range = Parse(address);
        if (length is { } span && span <= TimeSpan.Zero)
        {
            throw new HostingException("A ban needs a length above zero, or no length for a ban until you lift it.");
        }

        LedgerBan? entry = null;
        Update(paths, ledger => entry = ledger.Ban(range, now, length is { } value ? now + value : null, reason ?? ""));
        return entry!;
    }

    public static UnbanResult Unban(InstallPaths paths, string address, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var range = Parse(address);
        var auto = Read(paths, now).Blocked.Count(blocked => !blocked.Manual
            && System.Net.IPAddress.TryParse(blocked.Address, out var ip)
            && range.Contains(ip));
        var removed = 0;
        Update(paths, ledger => removed = ledger.Unban(range, now));
        return new UnbanResult(removed, auto);
    }

    public static bool Allow(InstallPaths paths, string address, string reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var range = Parse(address);
        var added = false;
        Update(paths, ledger => added = ledger.AddAllow(range, now, reason ?? ""));
        return added;
    }

    public static int Disallow(InstallPaths paths, string address)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var range = Parse(address);
        var removed = 0;
        Update(paths, ledger => removed = ledger.RemoveAllow(range));
        return removed;
    }

    public static AddressRange Parse(string address) =>
        AddressRange.TryParse(address, out var range)
            ? range
            : throw new HostingException($"'{address}' is not an address or a range like 203.0.113.0/24.");

    private static SecurityLedger LoadLedger(InstallPaths paths)
    {
        try
        {
            return SecurityLedger.Load(SecurityFiles.LedgerPath(paths.Security));
        }
        catch (JsonException ex)
        {
            throw new HostingException($"{SecurityFiles.LedgerPath(paths.Security)} is not valid: {ex.Message}", "Fix or delete the file; the servers keep the lists they last read.", ex);
        }
    }

    private static void Update(InstallPaths paths, Action<SecurityLedger> change)
    {
        try
        {
            SecurityLedger.Update(SecurityFiles.LedgerPath(paths.Security), change);
        }
        catch (JsonException ex)
        {
            throw new HostingException($"{SecurityFiles.LedgerPath(paths.Security)} is not valid: {ex.Message}", "Fix or delete the file, then try again.", ex);
        }
        catch (IOException ex)
        {
            throw new HostingException($"Could not update {SecurityFiles.LedgerPath(paths.Security)}: {ex.Message}", "Try again in a moment.", ex);
        }
    }
}
