using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Tests;

public sealed class GuildLadderRulesTests
{
    private static GuildLadderStanding Standing(int ladder, int decided) => new(1, ladder, decided, decided, 0, 0, 0);

    [Fact]
    public void NewGuildsSplitTheExpectationWithFactorFiftyAndHalfTheUsers()
    {
        var settled = GuildLadderRules.Settle(GuildLadderRules.ResultDecided, 0, false, false,
            new GuildLadderEntrant(null, 3), new GuildLadderEntrant(Standing(0, 0), 3));
        Assert.Equal(new GuildLadderSideChange(28, 1028, GuildMatchOutcome.Win), settled.Make);
        Assert.Equal(new GuildLadderSideChange(-25, 975, GuildMatchOutcome.Lose), settled.Challenge);
    }

    [Fact]
    public void EachSideUsesItsOwnFactorAndTheRatingGap()
    {
        var settled = GuildLadderRules.Settle(GuildLadderRules.ResultDecided, 1, false, false,
            new GuildLadderEntrant(Standing(0, 0), 4), new GuildLadderEntrant(Standing(1200, 40), 5));
        Assert.Equal(new GuildLadderSideChange(11, 1211, GuildMatchOutcome.Win), settled.Challenge);
        Assert.Equal(new GuildLadderSideChange(-12, 988, GuildMatchOutcome.Lose), settled.Make);
        Assert.Equal(GuildLadderRules.MasterFactor, GuildLadderRules.Factor(Standing(2400, 0)));
        Assert.Equal(GuildLadderRules.NewGuildFactor, GuildLadderRules.Factor(Standing(1500, 29)));
        Assert.Equal(GuildLadderRules.SettledFactor, GuildLadderRules.Factor(Standing(1500, 30)));
    }

    [Fact]
    public void DailyLimitKeepsTheRatingAndNoLadderRoomsMoveByOne()
    {
        var over = GuildLadderRules.Settle(GuildLadderRules.ResultDecided, 0, false, true,
            new GuildLadderEntrant(Standing(1100, 3), 3), new GuildLadderEntrant(null, 3));
        Assert.Equal(new GuildLadderSideChange(0, 1100, GuildMatchOutcome.Win), over.Make);
        Assert.Equal(new GuildLadderSideChange(0, 1000, GuildMatchOutcome.Lose), over.Challenge);
        var fixedStep = GuildLadderRules.Settle(GuildLadderRules.ResultDecided, 0, true, false,
            new GuildLadderEntrant(Standing(1100, 3), 3), new GuildLadderEntrant(null, 3));
        Assert.Equal(new GuildLadderSideChange(1, 1100, GuildMatchOutcome.Win), fixedStep.Make);
        Assert.Equal(new GuildLadderSideChange(-1, 1000, GuildMatchOutcome.Lose), fixedStep.Challenge);
    }

    [Fact]
    public void LoserNeverDropsBelowOne()
    {
        var settled = GuildLadderRules.Settle(GuildLadderRules.ResultDecided, 0, false, false,
            new GuildLadderEntrant(Standing(10, 1), 3), new GuildLadderEntrant(Standing(10, 1), 3));
        Assert.Equal(new GuildLadderSideChange(-25, GuildLadderRules.MinimumLadder, GuildMatchOutcome.Lose), settled.Challenge);
    }

    [Fact]
    public void DrawsAndForfeitsKeepTheRatingAndMapTheOutcomes()
    {
        var draw = GuildLadderRules.Settle(GuildLadderRules.ResultDraw, 0, false, false,
            new GuildLadderEntrant(Standing(1300, 3), 3), new GuildLadderEntrant(null, 3));
        Assert.Equal(new GuildLadderSideChange(0, 1300, GuildMatchOutcome.Draw), draw.Make);
        Assert.Equal(new GuildLadderSideChange(0, 1000, GuildMatchOutcome.Draw), draw.Challenge);
        Assert.Equal((GuildMatchOutcome.Invalid, GuildMatchOutcome.Disconnect), GuildLadderRules.Outcomes(GuildLadderRules.ResultForfeit, 0));
        Assert.Equal((GuildMatchOutcome.Disconnect, GuildMatchOutcome.Invalid), GuildLadderRules.Outcomes(GuildLadderRules.ResultForfeit, 1));
    }
}
