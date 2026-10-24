using System.Net;
using tPoseidon.Core.Common.Security;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Tests.Security;

public sealed class ConnectionGuardTimeoutTests
{
    private static readonly IPAddress Player = IPAddress.Parse("198.51.100.7");

    private sealed class Watched
    {
        public int Aborts { get; set; }
    }

    private static (ConnectionGuard Guard, ManualTime Time) NewGuard(bool expectsLogin = false, bool keepsAlive = false, Action<GuardSettings>? change = null)
    {
        var settings = new GuardSettings();
        change?.Invoke(settings);
        var time = new ManualTime();
        var guard = ConnectionGuardAdmissionTests.NewGuard(settings, time);
        guard.UseListener(expectsLogin, keepsAlive);
        return (guard, time);
    }

    private static (SessionGuard Session, Watched Watch) Admit(ConnectionGuard guard, IPAddress? address = null)
    {
        var session = guard.Admit(address ?? Player).Session!;
        var watch = new Watched();
        session.Bind(() => watch.Aborts++);
        return (session, watch);
    }

    [Fact]
    public void SilentConnection_IsClosedAfterTheHandshakeTimeout()
    {
        var (guard, time) = NewGuard();
        var (_, watch) = Admit(guard);

        time.Advance(TimeSpan.FromSeconds(30));
        guard.Sweep();
        Assert.Equal(0, watch.Aborts);

        time.Advance(TimeSpan.FromSeconds(1));
        guard.Sweep();
        guard.Sweep();
        Assert.Equal(1, watch.Aborts);
        Assert.Equal(1, guard.Counters[GuardCounter.HandshakeTimeouts]);
    }

    [Fact]
    public void FirstValidFrame_StopsTheHandshakeTimerAndEndsPendingWithoutLogins()
    {
        var (guard, time) = NewGuard();
        var (session, watch) = Admit(guard);

        session.FrameAccepted();
        time.Advance(TimeSpan.FromHours(2));
        guard.Sweep();

        Assert.Equal(0, watch.Aborts);
        Assert.Equal(0, guard.Pending);
    }

    [Fact]
    public void SessionThatNeverLogsIn_IsClosedAfterTheLoginTimeout()
    {
        var (guard, time) = NewGuard(expectsLogin: true);
        var (session, watch) = Admit(guard);
        session.FrameAccepted();
        Assert.Equal(1, guard.Pending);

        time.Advance(TimeSpan.FromSeconds(120));
        guard.Sweep();
        Assert.Equal(0, watch.Aborts);

        time.Advance(TimeSpan.FromSeconds(1));
        guard.Sweep();
        Assert.Equal(1, watch.Aborts);
        Assert.Equal(1, guard.Counters[GuardCounter.LoginTimeouts]);
    }

    [Fact]
    public void LoggedInSession_IsNotClosedByTheLoginTimeout()
    {
        var (guard, time) = NewGuard(expectsLogin: true);
        var (session, watch) = Admit(guard);
        session.FrameAccepted();
        session.Authenticated();

        time.Advance(TimeSpan.FromHours(3));
        guard.Sweep();

        Assert.Equal(0, watch.Aborts);
        Assert.Equal(0, guard.Pending);
    }

    [Fact]
    public void IdleLimit_AppliesOnlyWhereClientsKeepAlive()
    {
        var (keepAliveGuard, keepAliveTime) = NewGuard(expectsLogin: true, keepsAlive: true);
        var (idle, idleWatch) = Admit(keepAliveGuard);
        idle.FrameAccepted();
        idle.Authenticated();
        var (beating, beatingWatch) = Admit(keepAliveGuard, IPAddress.Parse("198.51.100.9"));
        beating.FrameAccepted();
        beating.Authenticated();

        for (var i = 0; i < 100; i++)
        {
            keepAliveTime.Advance(TimeSpan.FromSeconds(7));
            beating.FrameAccepted();
            keepAliveGuard.Sweep();
        }

        Assert.Equal(1, idleWatch.Aborts);
        Assert.Equal(0, beatingWatch.Aborts);
        Assert.Equal(1, keepAliveGuard.Counters[GuardCounter.IdleTimeouts]);

        var (quietGuard, quietTime) = NewGuard(expectsLogin: true, keepsAlive: false);
        var (quiet, quietWatch) = Admit(quietGuard);
        quiet.FrameAccepted();
        quiet.Authenticated();
        quietTime.Advance(TimeSpan.FromHours(5));
        quietGuard.Sweep();
        Assert.Equal(0, quietWatch.Aborts);
    }

    [Fact]
    public void IdleLimitOfZero_IsOff()
    {
        var (guard, time) = NewGuard(expectsLogin: true, keepsAlive: true, settings => settings.IdleTimeoutSeconds = 0);
        var (session, watch) = Admit(guard);
        session.FrameAccepted();
        session.Authenticated();

        time.Advance(TimeSpan.FromDays(1));
        guard.Sweep();

        Assert.Equal(0, watch.Aborts);
    }

    [Fact]
    public void PartialFrameHeldOpen_IsClosed()
    {
        var (guard, time) = NewGuard();
        var (session, watch) = Admit(guard);
        session.FrameAccepted();

        session.Buffered(5, madeProgress: false);
        for (var i = 0; i < 5; i++)
        {
            time.Advance(TimeSpan.FromSeconds(3));
            session.Buffered(5 + i, madeProgress: false);
            guard.Sweep();
        }

        Assert.Equal(0, watch.Aborts);
        time.Advance(TimeSpan.FromSeconds(1));
        guard.Sweep();
        Assert.Equal(1, watch.Aborts);
        Assert.Equal(1, guard.Counters[GuardCounter.PartialFrames]);
    }

    [Fact]
    public void SteadyStreamOfFrames_NeverLooksLikeAPartialFrame()
    {
        var (guard, time) = NewGuard();
        var (session, watch) = Admit(guard);
        session.FrameAccepted();

        for (var i = 0; i < 200; i++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            session.Buffered(3, madeProgress: true);
            guard.Sweep();
        }

        session.Buffered(0, madeProgress: true);
        time.Advance(TimeSpan.FromMinutes(10));
        guard.Sweep();
        Assert.Equal(0, watch.Aborts);
    }

    [Fact]
    public void TrustedSessions_HaveNoDeadlines()
    {
        var (guard, time) = NewGuard(expectsLogin: true, keepsAlive: true);
        var (session, watch) = Admit(guard, IPAddress.Loopback);
        session.Buffered(9, madeProgress: false);

        time.Advance(TimeSpan.FromDays(1));
        guard.Sweep();

        Assert.Equal(0, watch.Aborts);
    }
}
