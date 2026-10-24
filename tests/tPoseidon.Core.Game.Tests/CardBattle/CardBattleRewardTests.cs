using tPoseidon.Core.Game.CardBattle;

namespace tPoseidon.Core.Game.Tests.CardBattle;

public sealed class CardBattleRewardTests
{
    private static CardBattleStatus Status(bool winner, byte wins = 3, short score = 12, bool special = false, short bonus = 0, bool perfect = false, bool overDistance = false, bool noExp = false) =>
        new(wins, score, overDistance, 0, winner, 0, special, perfect, bonus, noExp);

    [Fact]
    public void TheWinnerGetsWinsTimesScoreTimesLevelPlusAQuarterOfTheLevelSquared()
    {
        Assert.Equal(3 * 12 * 50 + 625, CardBattleReward.WinnerExp(50, 3, 12));
        Assert.Equal(3 * 1 * 50 + 625, CardBattleReward.WinnerExp(50, 3, -4));
        Assert.Equal(1, CardBattleReward.WinnerExp(1, 0, 0));
    }

    [Fact]
    public void TheLoserGetsAQuarterOfTheWinnerFormula()
    {
        Assert.Equal((3 * 12 * 50 + 625) / 4, CardBattleReward.LoserExp(50, 3, 12));
        Assert.Equal(1, CardBattleReward.LoserExp(1, 0, 0));
    }

    [Fact]
    public void ASpecialSetRaisesTheExpByTwoAndAHalfPercentPerPoint()
    {
        var result = CardBattleReward.Compute(50, Status(true, special: true, bonus: 20));
        Assert.Equal((int)Math.Round((3 * 12 * 50 + 625) * 1.5), result.Exp);
        Assert.Equal(1f, CardBattleReward.SpecialMultiplier(-40));
    }

    [Fact]
    public void APerfectWinPaysTwiceTheScoreInGelder()
    {
        Assert.Equal(24, CardBattleReward.Compute(50, Status(true, perfect: true)).Money);
        Assert.Equal(0, CardBattleReward.Compute(50, Status(false, perfect: true)).Money);
    }

    [Fact]
    public void OverDistanceAndTheNoExpFlagStopTheWinnerExp()
    {
        Assert.Equal(new CardBattleRewardResult(0, 0), CardBattleReward.Compute(50, Status(true, perfect: true, overDistance: true)));
        Assert.Equal(0, CardBattleReward.Compute(50, Status(false, noExp: true)).Exp);
        Assert.Equal((3 * 12 * 50 + 625) / 4, CardBattleReward.Compute(50, Status(false, overDistance: true)).Exp);
    }

    [Fact]
    public void ADeckNeedsFiveCardsOfThreeTypesAndThreeNumbers()
    {
        var bag = new[]
        {
            new HeldCard(1, 1, 1, 1),
            new HeldCard(2, 2, 2, 1),
            new HeldCard(3, 3, 3, 1),
            new HeldCard(4, 1, 4, 1),
            new HeldCard(5, 2, 5, 1),
        };
        Assert.True(CardBattleRules.CanStillBuildDeck(bag, 0, 0));
        Assert.False(CardBattleRules.CanStillBuildDeck(bag, 5, 0));
        Assert.True(CardBattleRules.CanStillBuildDeck(bag.Append(new HeldCard(9, CardBattleRules.ChanceCardType, 6, 1)), 5, 0));
        Assert.False(CardBattleRules.CanStillBuildDeck(bag.Select(card => card with { CardType = 1 }), 0, 0));
        Assert.True(CardBattleRules.CanStillBuildDeck(bag.Take(4).Append(new HeldCard(5, 2, 5, 2)), 5, 0));
    }
}
