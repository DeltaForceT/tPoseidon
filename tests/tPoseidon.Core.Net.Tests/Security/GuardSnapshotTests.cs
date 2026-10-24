using System.Net;
using tPoseidon.Core.Common.Security;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Tests.Security;

public sealed class GuardSnapshotTests : IDisposable
{
    private static readonly IPAddress Attacker = IPAddress.Parse("198.51.100.66");
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tposeidon-guard-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ConnectionGuard NewGuard(string name, ManualTime time) =>
        new(new GuardSettings { NewConnectionsPerAddressBurst = 1, NewConnectionsPerAddressPerSecond = 0.001 }, name, _ => { }, time, trustLoopback: true, _directory);

    private static void Flood(ConnectionGuard guard, IPAddress address, int attempts)
    {
        for (var i = 0; i < attempts; i++)
        {
            if (guard.Admit(address).Session is { } session)
            {
                guard.Release(session);
            }
        }
    }

    [Fact]
    public void Snapshot_CarriesCountersBansAndRecentIncidents()
    {
        var time = new ManualTime();
        var guard = NewGuard("Kr-Lobby", time);
        var open = guard.Admit(IPAddress.Parse("203.0.113.5")).Session!;
        Flood(guard, Attacker, 70);

        var snapshot = guard.Snapshot();

        Assert.Equal("Kr-Lobby", snapshot.Server);
        Assert.Equal(1, snapshot.Connections);
        Assert.Equal(1, snapshot.Pending);
        Assert.Equal(1, snapshot.Counters["bans"]);
        Assert.Equal(10, snapshot.Counters["refusedAddressRate"]);
        Assert.Equal(59, snapshot.Counters["refusedBanned"]);
        Assert.Equal("198.51.100.66", Assert.Single(snapshot.Bans).Address);
        Assert.Equal(11, snapshot.Incidents.Count);
        Assert.Equal("ban", snapshot.Incidents[^1].Kind);
        guard.Release(open);
    }

    [Fact]
    public void RecentIncidents_KeepOnlyTheNewest()
    {
        var guard = new ConnectionGuard(new GuardSettings { StrikesBeforeBan = 100000, NewConnectionsPerAddressBurst = 1 }, "x", _ => { }, new ManualTime(), trustLoopback: true);
        Flood(guard, Attacker, 200);

        Assert.Equal(ConnectionGuard.RecentIncidentCount, guard.Snapshot().Incidents.Count);
    }

    [Fact]
    public void Publish_WritesTheSnapshotFile()
    {
        var time = new ManualTime();
        var guard = NewGuard("Kr-Game1", time);

        guard.Publish();

        var written = GuardSnapshot.TryRead(SecurityFiles.SnapshotPath(_directory, "Kr-Game1"));
        Assert.NotNull(written);
        Assert.Equal("Kr-Game1", written.Server);
        Assert.Equal(Environment.ProcessId, written.ProcessId);
    }

    [Fact]
    public void BanOnOneServer_IsHonouredByTheOthers()
    {
        var time = new ManualTime();
        var lobby = NewGuard("Kr-Lobby", time);
        var channel = NewGuard("Kr-Channel", time);
        Flood(lobby, Attacker, 11);
        lobby.Publish();

        channel.Publish();

        Assert.Equal(GuardRefusal.Banned, channel.Admit(Attacker).Refusal);
        Assert.True(channel.IsBanned(Attacker));
        Assert.Empty(channel.Snapshot().Bans);
    }

    [Fact]
    public void SharedBan_EndsWithTheOriginalBanAndHonoursUnban()
    {
        var time = new ManualTime();
        var lobby = NewGuard("Kr-Lobby", time);
        var channel = NewGuard("Kr-Channel", time);
        Flood(lobby, Attacker, 11);
        lobby.Publish();
        channel.Publish();
        Assert.True(channel.IsBanned(Attacker));

        time.Advance(TimeSpan.FromMinutes(6));
        Assert.False(channel.IsBanned(Attacker));

        time.Advance(TimeSpan.FromHours(2));
        Flood(lobby, Attacker, 11);
        lobby.Publish();
        channel.Publish();
        Assert.True(channel.IsBanned(Attacker));
        Assert.True(AddressRange.TryParse("198.51.100.66", out var range));
        time.Advance(TimeSpan.FromSeconds(1));
        SecurityLedger.Update(SecurityFiles.LedgerPath(_directory), ledger => ledger.Unban(range, time.GetUtcNow()));
        channel.ReloadLedger();
        channel.Publish();

        Assert.False(channel.IsBanned(Attacker));
    }

    [Fact]
    public void RestartedServer_KeepsTheBansItHadBefore()
    {
        var time = new ManualTime();
        var before = NewGuard("Kr-Lobby", time);
        Flood(before, Attacker, 11);
        before.Publish();

        var after = NewGuard("Kr-Lobby", time);
        after.Publish();

        Assert.True(after.IsBanned(Attacker));
    }
}
