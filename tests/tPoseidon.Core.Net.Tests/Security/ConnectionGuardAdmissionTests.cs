using System.Net;
using tPoseidon.Core.Common.Security;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Tests.Security;

public sealed class ConnectionGuardAdmissionTests
{
    private static readonly IPAddress Player = IPAddress.Parse("198.51.100.7");
    private static readonly IPAddress Other = IPAddress.Parse("198.51.100.8");

    internal static ConnectionGuard NewGuard(GuardSettings settings, ManualTime time, List<string>? log = null, bool trustLoopback = true) =>
        new(settings, "test", line => log?.Add(line), time, trustLoopback);

    [Fact]
    public void Loopback_IsAlwaysAdmittedAndNotCounted()
    {
        var guard = NewGuard(new GuardSettings { MaxConnectionsPerAddress = 1, MaxConnections = 1 }, new ManualTime());

        for (var i = 0; i < 50; i++)
        {
            var admission = guard.Admit(IPAddress.Loopback);
            Assert.True(admission.Admitted);
            Assert.True(admission.Session!.IsTrusted);
        }

        Assert.Equal(0, guard.Connections);
        Assert.Equal(50, guard.TrustedConnections);
    }

    [Fact]
    public void TrustedRange_IsAdmittedBeyondTheLimits()
    {
        var settings = new GuardSettings { MaxConnectionsPerAddress = 1, TrustedAddresses = ["198.51.100.0/24"] };
        var guard = NewGuard(settings, new ManualTime());

        Assert.True(guard.Admit(Player).Admitted);
        Assert.True(guard.Admit(Player).Admitted);
        Assert.True(guard.Admit(Player).Session!.IsTrusted);
        Assert.False(guard.IsTrusted(IPAddress.Parse("203.0.113.1")));
    }

    [Fact]
    public void PerAddressConnectionLimit_RefusesUntilOneCloses()
    {
        var guard = NewGuard(new GuardSettings { MaxConnectionsPerAddress = 2 }, new ManualTime());
        var first = guard.Admit(Player);
        Assert.True(guard.Admit(Player).Admitted);

        var refused = guard.Admit(Player);
        Assert.False(refused.Admitted);
        Assert.Equal(GuardRefusal.AddressLimit, refused.Refusal);
        Assert.True(guard.Admit(Other).Admitted);

        guard.Release(first.Session!);
        Assert.True(guard.Admit(Player).Admitted);
        Assert.Equal(1, guard.Counters[GuardCounter.RefusedAddressLimit]);
    }

    [Fact]
    public void NewConnectionRate_AllowsTheBurstThenRefillsOverTime()
    {
        var time = new ManualTime();
        var guard = NewGuard(new GuardSettings { NewConnectionsPerAddressBurst = 3, NewConnectionsPerAddressPerSecond = 1 }, time);

        for (var i = 0; i < 3; i++)
        {
            guard.Release(guard.Admit(Player).Session!);
        }

        var refused = guard.Admit(Player);
        Assert.Equal(GuardRefusal.AddressRate, refused.Refusal);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(guard.Admit(Player).Admitted);
        Assert.Equal(GuardRefusal.AddressRate, guard.Admit(Player).Refusal);
        Assert.Equal(2, guard.Counters[GuardCounter.RefusedAddressRate]);
    }

    [Fact]
    public void GlobalConnectionCap_RefusesEveryAddress()
    {
        var guard = NewGuard(new GuardSettings { MaxConnections = 2 }, new ManualTime());
        Assert.True(guard.Admit(Player).Admitted);
        Assert.True(guard.Admit(Other).Admitted);

        Assert.Equal(GuardRefusal.ServerFull, guard.Admit(IPAddress.Parse("203.0.113.9")).Refusal);
        Assert.True(guard.Admit(IPAddress.Loopback).Admitted);
    }

    [Fact]
    public void PendingCap_FreesUpWhenSessionsSendAValidFrame()
    {
        var guard = NewGuard(new GuardSettings { MaxPendingHandshakes = 2 }, new ManualTime());
        var first = guard.Admit(Player).Session!;
        Assert.True(guard.Admit(Other).Admitted);

        Assert.Equal(GuardRefusal.TooManyPending, guard.Admit(IPAddress.Parse("203.0.113.9")).Refusal);

        first.FrameAccepted();
        first.FrameAccepted();
        Assert.Equal(1, guard.Pending);
        Assert.True(guard.Admit(IPAddress.Parse("203.0.113.9")).Admitted);
    }

    [Fact]
    public void Release_IsCountedOnce()
    {
        var guard = NewGuard(new GuardSettings(), new ManualTime());
        var session = guard.Admit(Player).Session!;

        guard.Release(session);
        guard.Release(session);

        Assert.Equal(0, guard.Connections);
        Assert.Equal(0, guard.Pending);
    }

    [Fact]
    public void DisabledGuard_AdmitsEverything()
    {
        var guard = NewGuard(new GuardSettings { Enabled = false, MaxConnectionsPerAddress = 1 }, new ManualTime());

        for (var i = 0; i < 10; i++)
        {
            Assert.Same(SessionGuard.None, guard.Admit(Player).Session);
        }

        Assert.Equal(0, guard.Connections);
    }

    [Fact]
    public void MappedAddresses_CountAsTheSameAddress()
    {
        var guard = NewGuard(new GuardSettings { MaxConnectionsPerAddress = 1 }, new ManualTime());
        Assert.True(guard.Admit(Player).Admitted);

        Assert.Equal(GuardRefusal.AddressLimit, guard.Admit(Player.MapToIPv6()).Refusal);
    }

    [Fact]
    public void Sweep_ForgetsQuietAddresses()
    {
        var time = new ManualTime();
        var guard = NewGuard(new GuardSettings(), time);
        guard.Release(guard.Admit(Player).Session!);
        Assert.Equal(1, guard.TrackedAddresses);

        time.Advance(TimeSpan.FromMinutes(5));
        guard.Sweep();
        Assert.Equal(1, guard.TrackedAddresses);

        time.Advance(TimeSpan.FromMinutes(6));
        guard.Sweep();
        Assert.Equal(0, guard.TrackedAddresses);
    }

    [Fact]
    public void Incidents_AreLoggedOncePerAddressAndKindInAnInterval()
    {
        var log = new List<string>();
        var time = new ManualTime();
        var guard = NewGuard(new GuardSettings { NewConnectionsPerAddressBurst = 1, IncidentLogIntervalSeconds = 30, StrikesBeforeBan = 1000 }, time, log);
        guard.Release(guard.Admit(Player).Session!);

        for (var i = 0; i < 100; i++)
        {
            Assert.False(guard.Admit(Player).Admitted);
        }

        Assert.Single(log);
        Assert.Contains("198.51.100.7", log[0], StringComparison.Ordinal);

        time.Advance(TimeSpan.FromSeconds(31));
        guard.Sweep();
        Assert.Equal(2, log.Count);
        Assert.Contains("99 more incidents", log[1], StringComparison.Ordinal);
    }
}
