using tPoseidon.Core.Common.Security;
using tPoseidon.Hosting.Security;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Hosting.Tests;

public sealed class SecurityStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 10, 0, 0, TimeSpan.Zero);

    private static void WriteSnapshot(InstallPaths paths, string server, DateTimeOffset updated, params SnapshotBan[] bans)
    {
        Directory.CreateDirectory(paths.Security);
        new GuardSnapshot
        {
            Server = server,
            UpdatedAt = updated,
            Connections = 3,
            Counters = { ["accepted"] = 9, ["bans"] = bans.Length },
            Bans = [.. bans],
            Incidents = [new SnapshotIncident { At = updated, Address = "198.51.100.66", Kind = "connectionRate", Detail = server }],
        }.Write(SecurityFiles.SnapshotPath(paths.Security, server));
    }

    private static SnapshotBan AutoBan(string address, int minutes) =>
        new() { Address = address, Since = Now.AddMinutes(-1), Until = Now.AddMinutes(minutes), Level = 1, Reason = "connection rate" };

    [Fact]
    public void Read_MergesServersBansAndIncidents()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        WriteSnapshot(paths, "Kr-Lobby", Now, AutoBan("198.51.100.66", 5));
        WriteSnapshot(paths, "Kr-Channel", Now.AddMinutes(-10), AutoBan("198.51.100.66", 9), AutoBan("203.0.113.1", -1));
        SecurityState.Ban(paths, "192.0.2.0/24", null, "spam", Now);

        var overview = SecurityState.Read(paths, Now);

        Assert.Equal(2, overview.Servers.Count);
        Assert.False(overview.Servers.Single(s => s.Snapshot.Server == "Kr-Channel").Running);
        Assert.True(overview.Servers.Single(s => s.Snapshot.Server == "Kr-Lobby").Running);
        var auto = Assert.Single(overview.Blocked, b => !b.Manual);
        Assert.Equal("198.51.100.66", auto.Address);
        Assert.Equal(Now.AddMinutes(9), auto.Until);
        Assert.Equal(["Kr-Channel", "Kr-Lobby"], auto.Servers.Order(StringComparer.Ordinal));
        var manual = Assert.Single(overview.Blocked, b => b.Manual);
        Assert.Equal("192.0.2.0/24", manual.Address);
        Assert.Null(manual.Until);
        Assert.Equal(2, overview.Incidents.Count);
        Assert.Equal("Kr-Lobby", overview.Incidents[0].Server);
    }

    [Fact]
    public void BanUnbanAllowAndDisallow_EditTheLedger()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);

        var ban = SecurityState.Ban(paths, "203.0.113.9", TimeSpan.FromMinutes(30), "", Now);
        Assert.Equal(Now.AddMinutes(30), ban.Until);
        Assert.True(SecurityState.Allow(paths, "10.0.0.0/8", "office", Now));
        Assert.False(SecurityState.Allow(paths, "10.0.0.0/8", "office", Now));

        var unban = SecurityState.Unban(paths, "203.0.113.9", Now);
        Assert.Equal(1, unban.ManualRemoved);
        Assert.Equal(1, SecurityState.Disallow(paths, "10.0.0.0/8"));

        var ledger = SecurityLedger.Load(SecurityFiles.LedgerPath(paths.Security));
        Assert.Empty(ledger.Bans);
        Assert.Empty(ledger.Allow);
        Assert.Equal("203.0.113.9", Assert.Single(ledger.Pardons).Address);
    }

    [Fact]
    public void Unban_CountsAutomaticBansItLifts()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        WriteSnapshot(paths, "Kr-Lobby", Now, AutoBan("198.51.100.66", 5));

        var result = SecurityState.Unban(paths, "198.51.100.0/24", Now);

        Assert.Equal(0, result.ManualRemoved);
        Assert.Equal(1, result.AutoLifted);
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("10.0.0.0/40")]
    public void BadAddresses_AreRefusedWithAClearMessage(string address)
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);

        var error = Assert.Throws<HostingException>(() => SecurityState.Ban(paths, address, null, "", Now));

        Assert.Contains(address, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ServersGetTheSecurityFolderAndTheirName()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        var lobby = ServerCatalog.All.Single(server => server.Name == "Kr-Lobby");

        var environment = ServerEnvironment.For(lobby, new HostSettings(), paths);

        Assert.Equal(paths.Security, environment[GuardSettings.StateDirectoryVariable]);
        Assert.Equal("Kr-Lobby", environment[GuardSettings.ServerNameVariable]);
    }
}
