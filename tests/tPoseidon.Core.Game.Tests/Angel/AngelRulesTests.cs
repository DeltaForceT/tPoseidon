using tPoseidon.Core.Game.Angel;

namespace tPoseidon.Core.Game.Tests.Angel;

public sealed class AngelRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LevelBandsFollowTheSystemServer()
    {
        Assert.True(AngelRules.IsNewbie(30));
        Assert.False(AngelRules.IsNewbie(31));
        Assert.True(AngelRules.IsAngel(51));
        Assert.False(AngelRules.IsAngel(50));
    }

    [Fact]
    public void RequestStatesHoldTheAngel()
    {
        Assert.Equal(AngelRules.SameAngelTooSoon, AngelRules.RefusalFor(AngelRequestState.Requested, Now.AddSeconds(-299), Now));
        Assert.Null(AngelRules.RefusalFor(AngelRequestState.Requested, Now.AddSeconds(-300), Now));
        Assert.Equal(AngelRules.HelpingAnother, AngelRules.RefusalFor(AngelRequestState.Accepted, Now.AddDays(-1), Now));
        Assert.Equal(AngelRules.HelpingAnother, AngelRules.RefusalFor(AngelRequestState.Ended, Now.AddSeconds(-1799), Now));
        Assert.True(AngelRules.IsFree(AngelRequestState.Ended, Now.AddSeconds(-1800), Now));
        Assert.True(AngelRules.IsFree(AngelRequestState.None, Now, Now));
    }

    [Fact]
    public void RudeNewbieWaitGrowsAfterFourReports()
    {
        Assert.Equal(TimeSpan.FromSeconds(300), AngelRules.RudeWait(3));
        Assert.Equal(TimeSpan.FromSeconds(1800), AngelRules.RudeWait(4));
        Assert.Equal(TimeSpan.FromSeconds(200), AngelRules.RemainingRudeWait(1, Now.AddSeconds(-100), Now));
        Assert.Null(AngelRules.RemainingRudeWait(1, Now.AddSeconds(-300), Now));
        Assert.Null(AngelRules.RemainingRudeWait(0, Now, Now));
    }
}
