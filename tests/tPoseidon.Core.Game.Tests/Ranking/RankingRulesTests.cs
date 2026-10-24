using tPoseidon.Core.Game.Ranking;

namespace tPoseidon.Core.Game.Tests.Ranking;

public sealed class RankingRulesTests
{
    [Fact]
    public void MondayRunsCoverThePreviousWeek()
    {
        Assert.Equal((new DateTime(2026, 10, 12), new DateTime(2026, 10, 18, 23, 59, 59)), RankingRules.Week(new DateTime(2026, 10, 19, 3, 0, 0)));
        Assert.Equal((new DateTime(2026, 10, 19), new DateTime(2026, 10, 25, 23, 59, 59)), RankingRules.Week(new DateTime(2026, 10, 21, 12, 0, 0)));
        Assert.Equal((new DateTime(2026, 10, 19), new DateTime(2026, 10, 25, 23, 59, 59)), RankingRules.Week(new DateTime(2026, 10, 25, 23, 0, 0)));
    }

    [Fact]
    public void KindsMapToRankingTypes()
    {
        Assert.Equal(6, RankingRules.MonsterRankingType(RankingRules.TimeKind));
        Assert.Equal(5, RankingRules.MonsterRankingType(RankingRules.KillKind));
        Assert.Null(RankingRules.MonsterRankingType(2));
        Assert.Equal(1, RankingRules.CardBattleRankingType(0));
        Assert.Equal(4, RankingRules.CardBattleRankingType(3));
        Assert.Null(RankingRules.CardBattleRankingType(4));
        Assert.Equal(7000, RankingRules.MonsterValue(6, 30, 7000));
        Assert.Equal(30, RankingRules.MonsterValue(5, 30, 7000));
    }
}
