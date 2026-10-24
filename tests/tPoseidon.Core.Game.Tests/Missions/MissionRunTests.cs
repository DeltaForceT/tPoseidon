using tPoseidon.Core.Game.Missions;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Missions;

public sealed class MissionRunTests
{
    private static readonly MissionInfoEntry Coral = new(1, "Coral", 180, 400, 5, 758, 2, 1, 1, "MissionMonster_Coral");
    private static readonly MissionWave[] Waves = [new(1, [(3245, 1)]), new(2, [(3246, 2)])];
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WavesRunInOrderAndEndWithSuccess()
    {
        var run = new MissionRun(Coral, Waves, 3, 100, 200);
        Assert.Equal(1, run.Start(Now)!.Wave);
        Assert.Null(run.Start(Now));
        Assert.Equal(Now.AddMinutes(5), run.EndsAt);
        Assert.Equal(2, run.WaveCleared(Now.AddSeconds(30))!.Wave);
        Assert.Null(run.WaveCleared(Now.AddSeconds(60)));
        Assert.Equal(MissionRules.Succeeded, run.Result);
        Assert.Equal(MissionPhase.Exiting, run.Phase);
        Assert.False(run.ReadyToLeave(Now.AddSeconds(79)));
        Assert.True(run.ReadyToLeave(Now.AddSeconds(80)));
    }

    [Fact]
    public void TimeOutFailsTheMission()
    {
        var run = new MissionRun(Coral, Waves, 3, 100, 200);
        run.Start(Now);
        Assert.False(run.TimedOut(Now.AddMinutes(4)));
        Assert.True(run.TimedOut(Now.AddMinutes(5)));
        Assert.Equal(MissionRules.Failed, run.Result);
        Assert.False(MissionRules.InBand(Coral, 179));
        Assert.True(MissionRules.InBand(Coral, 180));
    }
}
