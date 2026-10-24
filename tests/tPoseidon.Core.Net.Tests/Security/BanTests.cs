using System.Net;
using tPoseidon.Core.Common.Security;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Tests.Security;

public sealed class BanTests : IDisposable
{
    private static readonly IPAddress Attacker = IPAddress.Parse("198.51.100.66");
    private static readonly IPAddress Player = IPAddress.Parse("198.51.100.7");
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tposeidon-guard-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private (ConnectionGuard Guard, ManualTime Time, List<string> Log) NewGuard(Action<GuardSettings>? change = null)
    {
        var settings = new GuardSettings { NewConnectionsPerAddressBurst = 1, NewConnectionsPerAddressPerSecond = 0.001 };
        change?.Invoke(settings);
        var time = new ManualTime();
        var log = new List<string>();
        return (new ConnectionGuard(settings, "test", log.Add, time, trustLoopback: true, _directory), time, log);
    }

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
    public void EnoughStrikesInTheWindow_BanTheAddress()
    {
        var (guard, _, log) = NewGuard();

        Flood(guard, Attacker, 11);

        Assert.True(guard.IsBanned(Attacker));
        Assert.Equal(GuardRefusal.Banned, guard.Admit(Attacker).Refusal);
        Assert.True(guard.Admit(Player).Admitted);
        Assert.Equal(1, guard.Counters[GuardCounter.Bans]);
        Assert.Contains(log, line => line.Contains("banned 198.51.100.66 for 5 min", StringComparison.Ordinal));
        var ban = Assert.Single(guard.ActiveBans());
        Assert.Equal("198.51.100.66", ban.Address);
        Assert.Equal(1, ban.Level);
    }

    [Fact]
    public void StrikesSpreadPastTheWindow_DoNotBan()
    {
        var (guard, time, _) = NewGuard();
        guard.Release(guard.Admit(Attacker).Session!);

        for (var i = 0; i < 30; i++)
        {
            Assert.False(guard.Admit(Attacker).Admitted);
            time.Advance(TimeSpan.FromSeconds(7));
        }

        Assert.False(guard.IsBanned(Attacker));
    }

    [Fact]
    public void RepeatedBans_DoubleUpToTheCapAndAreForgottenAfterAQuietDay()
    {
        var (guard, time, _) = NewGuard(s => s.BanMaxMinutes = 30);
        var lengths = new List<double>();
        for (var round = 0; round < 5; round++)
        {
            Flood(guard, Attacker, 11);
            var ban = Assert.Single(guard.ActiveBans());
            lengths.Add(Math.Round((ban.Until - time.GetUtcNow()).TotalMinutes));
            time.Advance(ban.Until - time.GetUtcNow() + TimeSpan.FromSeconds(1));
            Assert.False(guard.IsBanned(Attacker));
            time.Advance(TimeSpan.FromHours(1));
        }

        Assert.Equal(new double[] { 5, 10, 20, 30, 30 }, lengths);

        time.Advance(TimeSpan.FromHours(25));
        Flood(guard, Attacker, 11);
        Assert.Equal(5, Math.Round((Assert.Single(guard.ActiveBans()).Until - time.GetUtcNow()).TotalMinutes));
    }

    [Fact]
    public void AutoBan_ClosesSessionsStillLoggingInButNotPlayersAlreadyIn()
    {
        var (guard, _, _) = NewGuard(s => s.NewConnectionsPerAddressBurst = 4);
        var playing = guard.Admit(Attacker).Session!;
        var playingAborts = 0;
        playing.Bind(() => playingAborts++);
        playing.FrameAccepted();
        playing.Authenticated();
        var waiting = guard.Admit(Attacker).Session!;
        var waitingAborts = 0;
        waiting.Bind(() => waitingAborts++);

        guard.Admit(Attacker).Session!.Reject(GuardIncident.MalformedFrame, "test");
        Assert.False(guard.IsBanned(Attacker));
        guard.Admit(Attacker).Session!.Reject(GuardIncident.MalformedFrame, "test");

        Assert.True(guard.IsBanned(Attacker));
        Assert.Equal(1, waitingAborts);
        Assert.Equal(0, playingAborts);
        Assert.Equal(1, guard.Counters[GuardCounter.BanKicks]);
    }

    [Fact]
    public void TrustedAddresses_AreNeverBanned()
    {
        var (guard, _, _) = NewGuard();

        for (var i = 0; i < 100; i++)
        {
            guard.Admit(IPAddress.Loopback).Session!.Reject(GuardIncident.OversizedFrame, "test");
        }

        Assert.False(guard.IsBanned(IPAddress.Loopback));
        Assert.True(guard.Admit(IPAddress.Loopback).Admitted);
    }

    [Fact]
    public void ManualBan_RefusesTheRangeAndClosesEverySessionInIt()
    {
        var (guard, time, log) = NewGuard(s => s.NewConnectionsPerAddressBurst = 20);
        var inGame = guard.Admit(Player).Session!;
        var aborts = 0;
        inGame.Bind(() => aborts++);
        inGame.Authenticated();
        Assert.True(AddressRange.TryParse("198.51.100.0/24", out var range));

        SecurityLedger.Update(SecurityFiles.LedgerPath(_directory), ledger => ledger.Ban(range, time.GetUtcNow(), null, "test"));
        guard.ReloadLedger();

        Assert.Equal(1, aborts);
        Assert.Equal(GuardRefusal.Banned, guard.Admit(Player).Refusal);
        Assert.True(guard.Admit(IPAddress.Parse("203.0.113.4")).Admitted);
        Assert.Contains(log, line => line.Contains("manual ban on 198.51.100.0/24", StringComparison.Ordinal));

        guard.ReloadLedger();
        Assert.Equal(1, guard.Counters[GuardCounter.BanKicks]);
    }

    [Fact]
    public void ManualBan_EndsAtItsTime()
    {
        var (guard, time, _) = NewGuard(s => s.NewConnectionsPerAddressBurst = 20);
        Assert.True(AddressRange.TryParse("198.51.100.7", out var range));
        SecurityLedger.Update(SecurityFiles.LedgerPath(_directory), ledger => ledger.Ban(range, time.GetUtcNow(), time.GetUtcNow().AddMinutes(30), ""));
        guard.ReloadLedger();
        Assert.False(guard.Admit(Player).Admitted);

        time.Advance(TimeSpan.FromMinutes(31));

        Assert.True(guard.Admit(Player).Admitted);
    }

    [Fact]
    public void AllowList_TrustsTheAddressEvenWhenItWasBanned()
    {
        var (guard, time, _) = NewGuard();
        Flood(guard, Attacker, 11);
        Assert.True(AddressRange.TryParse("198.51.100.66", out var range));

        SecurityLedger.Update(SecurityFiles.LedgerPath(_directory), ledger => ledger.AddAllow(range, time.GetUtcNow(), "office"));
        guard.ReloadLedger();

        var admission = guard.Admit(Attacker);
        Assert.True(admission.Admitted);
        Assert.True(admission.Session!.IsTrusted);
    }

    [Fact]
    public void Unban_LiftsAnAutoBanAndForgetsTheHistory()
    {
        var (guard, time, _) = NewGuard(s => s.NewConnectionsPerAddressBurst = 1);
        Flood(guard, Attacker, 11);
        Assert.True(guard.IsBanned(Attacker));
        Assert.True(AddressRange.TryParse("198.51.100.66", out var range));
        time.Advance(TimeSpan.FromSeconds(1));

        SecurityLedger.Update(SecurityFiles.LedgerPath(_directory), ledger => ledger.Unban(range, time.GetUtcNow()));
        guard.ReloadLedger();

        Assert.False(guard.IsBanned(Attacker));
        time.Advance(TimeSpan.FromHours(1));
        Assert.True(guard.Admit(Attacker).Admitted);
        Flood(guard, Attacker, 11);
        Assert.Equal(1, Assert.Single(guard.ActiveBans()).Level);
    }

    [Fact]
    public void Unban_DoesNotLiftABanThatStartedAfterIt()
    {
        var (guard, time, _) = NewGuard();
        var pardonedAt = time.GetUtcNow();
        time.Advance(TimeSpan.FromSeconds(5));
        Flood(guard, Attacker, 11);
        Assert.True(AddressRange.TryParse("198.51.100.66", out var range));

        SecurityLedger.Update(SecurityFiles.LedgerPath(_directory), ledger => ledger.Unban(range, pardonedAt));
        guard.ReloadLedger();

        Assert.True(guard.IsBanned(Attacker));
    }

    [Fact]
    public void BrokenLedgerFile_KeepsThePreviousLists()
    {
        var (guard, time, log) = NewGuard(s => s.NewConnectionsPerAddressBurst = 20);
        Assert.True(AddressRange.TryParse("198.51.100.7", out var range));
        SecurityLedger.Update(SecurityFiles.LedgerPath(_directory), ledger => ledger.Ban(range, time.GetUtcNow(), null, ""));
        guard.ReloadLedger();

        File.WriteAllText(SecurityFiles.LedgerPath(_directory), "{ not json");
        guard.ReloadLedger();

        Assert.False(guard.Admit(Player).Admitted);
        Assert.Contains(log, line => line.Contains("could not read bans.json", StringComparison.Ordinal));
    }
}
