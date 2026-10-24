using tPoseidon.Core.Game.Quests;

namespace tPoseidon.Core.Game.Tests.Quests;

public sealed class DailyCycleTests
{
    private static readonly DailyCycle KrDay = new([38, 6, 10, 6]);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(37, 0)]
    [InlineData(38, 1)]
    [InlineData(43, 1)]
    [InlineData(44, 2)]
    [InlineData(53, 2)]
    [InlineData(54, 3)]
    [InlineData(59, 3)]
    [InlineData(60, 0)]
    [InlineData(98, 1)]
    public void ThePhaseFollowsTheRunningSumOfTheDailyTimeMinutes(int minute, int phase)
    {
        Assert.Equal(phase, KrDay.PhaseAt(DateTimeOffset.UnixEpoch.AddMinutes(minute).AddSeconds(30)));
    }

    [Fact]
    public void AnEmptyTableHasNoPhase()
    {
        Assert.Null(DailyCycle.Empty.PhaseAt(DateTimeOffset.UnixEpoch));
        Assert.Equal(4, KrDay.Phases);
    }

    [Fact]
    public void CheckDailyValueIsAScriptFunction()
    {
        Assert.Equal(NpcScriptOperation.CheckDailyValue, NpcScript.Classify(87).Operation);
    }
}
