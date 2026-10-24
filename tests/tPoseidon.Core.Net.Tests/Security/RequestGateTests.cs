using System.Net;
using tPoseidon.Core.Common.Security;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Tests.Security;

public sealed class RequestGateTests
{
    private static readonly IPAddress Visitor = IPAddress.Parse("198.51.100.20");

    private static (ConnectionGuard Guard, ManualTime Time) NewGuard(Action<GuardSettings>? change = null)
    {
        var settings = new GuardSettings { RequestBurst = 5, RequestsPerSecond = 2 };
        change?.Invoke(settings);
        var time = new ManualTime();
        return (ConnectionGuardAdmissionTests.NewGuard(settings, time), time);
    }

    [Fact]
    public void PageLoads_AreAllowedUpToTheBurstThenRefusedUntilTheBucketRefills()
    {
        var (guard, time) = NewGuard();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(GuardRefusal.None, guard.AdmitRequest(Visitor));
        }

        Assert.Equal(GuardRefusal.AddressRate, guard.AdmitRequest(Visitor));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(GuardRefusal.None, guard.AdmitRequest(Visitor));
        Assert.Equal(1, guard.Counters[GuardCounter.RequestsRefused]);
        Assert.Equal(6, guard.Counters[GuardCounter.RequestsAllowed]);
    }

    [Fact]
    public void RequestFlood_EndsInABan()
    {
        var (guard, _) = NewGuard();

        for (var i = 0; i < 15; i++)
        {
            guard.AdmitRequest(Visitor);
        }

        Assert.Equal(GuardRefusal.Banned, guard.AdmitRequest(Visitor));
        Assert.True(guard.IsBanned(Visitor));
        Assert.Equal(GuardRefusal.Banned, guard.Admit(Visitor).Refusal);
    }

    [Fact]
    public void TrustedAddresses_AreNeverLimited()
    {
        var (guard, _) = NewGuard();

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(GuardRefusal.None, guard.AdmitRequest(IPAddress.Loopback));
        }
    }

    [Fact]
    public void DisabledGuard_AllowsEveryRequest()
    {
        var (guard, _) = NewGuard(s => s.Enabled = false);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(GuardRefusal.None, guard.AdmitRequest(Visitor));
        }
    }
}
