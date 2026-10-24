using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Tests.Security;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class ClientHeartbeatTests
{
    private static readonly TimeSpan Answer = TimeSpan.FromMilliseconds(60000);

    [Fact]
    public void FirstPingComesFiveSecondsAfterTheStart()
    {
        var time = new ManualTime();
        var beat = new ClientHeartbeat(Answer, time);
        beat.Start();

        time.Advance(TimeSpan.FromMilliseconds(5000));
        Assert.Equal(HeartbeatStep.Wait, beat.Tick());

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(HeartbeatStep.Ping, beat.Tick());
    }

    [Fact]
    public void AnAnsweredPingIsFollowedByTheNextOneSixtySecondsLater()
    {
        var time = new ManualTime();
        var beat = new ClientHeartbeat(Answer, time);
        beat.Start();
        time.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(HeartbeatStep.Ping, beat.Tick());

        time.Advance(TimeSpan.FromSeconds(1));
        beat.Answered();

        time.Advance(TimeSpan.FromSeconds(58));
        Assert.Equal(HeartbeatStep.Wait, beat.Tick());
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(HeartbeatStep.Ping, beat.Tick());
    }

    [Fact]
    public void AnUnansweredPingExpiresAfterTheAnswerTimeout()
    {
        var time = new ManualTime();
        var beat = new ClientHeartbeat(Answer, time);
        beat.Start();
        time.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(HeartbeatStep.Ping, beat.Tick());

        time.Advance(Answer);
        Assert.Equal(HeartbeatStep.Wait, beat.Tick());
        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(HeartbeatStep.Expired, beat.Tick());
    }

    [Fact]
    public void APlayerWaitingAtCharacterSelectStaysBelowTheGuardIdleLimit()
    {
        var time = new ManualTime();
        var beat = new ClientHeartbeat(Answer, time);
        beat.Start();
        var lastFrame = time.GetUtcNow();
        var longestSilence = TimeSpan.Zero;
        for (var second = 0; second < 3600; second++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            if (beat.Tick() == HeartbeatStep.Ping)
            {
                beat.Answered();
                var silence = time.GetUtcNow() - lastFrame;
                if (silence > longestSilence) longestSilence = silence;
                lastFrame = time.GetUtcNow();
            }
        }

        Assert.True(longestSilence <= TimeSpan.FromSeconds(61), $"longest silence {longestSilence}");
        Assert.True(longestSilence < TimeSpan.FromSeconds(new tPoseidon.Core.Common.Security.GuardSettings().IdleTimeoutSeconds));
    }

    [Fact]
    public void NothingIsSentBeforeTheLogin()
    {
        var time = new ManualTime();
        var beat = new ClientHeartbeat(Answer, time);

        time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(HeartbeatStep.Wait, beat.Tick());

        beat.Start();
        time.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(HeartbeatStep.Ping, beat.Tick());
    }

    [Theory]
    [InlineData(60000, 60000)]
    [InlineData(-1, 33333)]
    [InlineData(0, 33333)]
    public void TheAnswerTimeoutComesFromTheIniHeartbeat(int iniMilliseconds, int expectedMilliseconds)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), ClientHeartbeat.AnswerTimeoutFromIni(iniMilliseconds));
    }
}
