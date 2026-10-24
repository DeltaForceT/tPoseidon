using System.Net;
using tPoseidon.Core.Common.Security;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Tests.Security;

public sealed class LoginThrottleTests
{
    private static readonly IPAddress Attacker = IPAddress.Parse("198.51.100.66");

    private static (LoginThrottle Throttle, ManualTime Time) NewThrottle(Action<GuardSettings>? change = null)
    {
        var settings = new GuardSettings();
        change?.Invoke(settings);
        var time = new ManualTime();
        return (new LoginThrottle(settings, time), time);
    }

    private static IPAddress Address(int i) => new([203, 0, 113, (byte)i]);

    [Fact]
    public void Delay_StartsAfterTheFreeFailuresAndDoublesUpToTheCap()
    {
        var (throttle, _) = NewThrottle(s => s.AccountLockoutFailures = 100);

        var delays = Enumerable.Range(0, 9).Select(_ => throttle.Failed(Attacker, "victim").Delay.TotalSeconds).ToArray();

        Assert.Equal(new double[] { 0, 0, 0, 1, 2, 4, 8, 16, 16 }, delays);
    }

    [Fact]
    public void Account_IsLockedAfterTooManyFailuresFromAnyAddress()
    {
        var (throttle, time) = NewThrottle();
        for (var i = 0; i < 9; i++)
        {
            Assert.False(throttle.Failed(Address(i), "Victim").LockedNow);
        }

        var last = throttle.Failed(Address(9), "victim");
        Assert.True(last.LockedNow);
        Assert.Equal(TimeSpan.FromSeconds(16), last.Delay);

        Assert.True(throttle.Check(Address(42), "VICTIM").LockedOut);
        Assert.False(throttle.Check(Address(42), "someone-else").LockedOut);

        time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        Assert.False(throttle.Check(Address(42), "victim").LockedOut);
    }

    [Fact]
    public void Address_IsLockedWhenItTriesManyAccounts()
    {
        var (throttle, _) = NewThrottle();
        for (var i = 0; i < 29; i++)
        {
            Assert.False(throttle.Failed(Attacker, "account" + i).LockedNow);
        }

        Assert.True(throttle.Failed(Attacker, "account29").LockedNow);
        Assert.True(throttle.Check(Attacker, "brand-new").LockedOut);
        Assert.True(throttle.Check(Attacker, null).LockedOut);
        Assert.False(throttle.Check(Address(1), "brand-new").LockedOut);
    }

    [Fact]
    public void Success_ClearsTheAccountButNotTheAddress()
    {
        var (throttle, _) = NewThrottle(s => s.AddressLockoutFailures = 100);
        for (var i = 0; i < 5; i++)
        {
            throttle.Failed(Attacker, "player");
        }

        throttle.Succeeded("player");

        Assert.Equal(6, throttle.Failed(Address(7), "player").Failures + 5);
        Assert.Equal(6, throttle.Failed(Attacker, "other").Failures);
    }

    [Fact]
    public void FailuresOutsideTheWindow_StartOver()
    {
        var (throttle, time) = NewThrottle();
        for (var i = 0; i < 9; i++)
        {
            throttle.Failed(Attacker, "player");
        }

        time.Advance(TimeSpan.FromMinutes(16));

        var next = throttle.Failed(Attacker, "player");
        Assert.False(next.LockedNow);
        Assert.Equal(1, next.Failures);
        Assert.Equal(TimeSpan.Zero, next.Delay);
    }

    [Fact]
    public void Pardon_ForgetsTheAddress()
    {
        var (throttle, _) = NewThrottle(s => s.AddressLockoutFailures = 2);
        throttle.Failed(Attacker, "a");
        throttle.Failed(Attacker, "b");
        Assert.True(throttle.Check(Attacker, null).LockedOut);

        Assert.True(AddressRange.TryParse("198.51.100.0/24", out var range));
        Assert.Equal(1, throttle.Pardon(range));

        Assert.False(throttle.Check(Attacker, null).LockedOut);
    }

    [Fact]
    public void Prune_DropsQuietRecordsButKeepsLockouts()
    {
        var (throttle, time) = NewThrottle(s => s.AccountLockoutFailures = 1);
        throttle.Failed(Attacker, "locked");
        throttle.Failed(Address(2), null);

        time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        throttle.Prune();
        Assert.Equal(0, throttle.TrackedAddresses);
        Assert.Equal(0, throttle.TrackedAccounts);
    }

    [Fact]
    public void TrustedSessions_AreNeverThrottled()
    {
        var guard = ConnectionGuardAdmissionTests.NewGuard(new GuardSettings { AccountLockoutFailures = 1 }, new ManualTime());
        var local = guard.Admit(IPAddress.Loopback).Session!;

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(TimeSpan.Zero, local.LoginFailed("admin"));
        }

        Assert.False(local.CheckLogin("admin").LockedOut);
        Assert.Equal(0, guard.Counters[GuardCounter.LoginFailures]);
    }

    [Fact]
    public void SessionGuard_CountsFailuresAndLockouts()
    {
        var guard = ConnectionGuardAdmissionTests.NewGuard(new GuardSettings { AccountLockoutFailures = 2 }, new ManualTime());
        var session = guard.Admit(Attacker).Session!;

        session.LoginFailed("victim");
        session.LoginFailed("victim");
        var gate = session.CheckLogin("victim");

        Assert.True(gate.LockedOut);
        Assert.Equal(2, guard.Counters[GuardCounter.LoginFailures]);
        Assert.Equal(1, guard.Counters[GuardCounter.Lockouts]);
        Assert.Equal(1, guard.Counters[GuardCounter.LockedOutAttempts]);
    }
}
