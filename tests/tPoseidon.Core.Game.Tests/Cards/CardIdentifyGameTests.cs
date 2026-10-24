using tPoseidon.Core.Game.Cards;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Cards;

public sealed class CardIdentifyGameTests
{
    private static readonly int[] Combos = [1, 2, 4, 7, 12];

    private static CardIdentifyTables Tables() => new(
        new Dictionary<int, CardIdentifyGradeEntry> { [1] = new(1, 1, 8f, 0, 0f, 1, 2f), [2] = new(2, 1, 7.8f, 0, 0f, 2, 2.2f) },
        new Dictionary<int, IReadOnlyList<CardIdentifyResultEntry>>
        {
            [1] = [new(2003, 30), new(2203, 30), new(8300, 5), new(8301, 5), new(8302, 5)],
        },
        Combos,
        0f,
        1f);

    private static CardIdentifyGame Dealt(int cardValue = 15)
    {
        var game = new CardIdentifyGame();
        var random = new Random(7);
        Assert.True(game.Deal(cardValue, Tables(), random.Next));
        return game;
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(7, 18)]
    [InlineData(8, 16)]
    [InlineData(9, 18)]
    [InlineData(15, 30)]
    public void StartLifeMatchesTheGuideTable(int card, short life)
    {
        Assert.Equal(life, CardIdentifyGame.StartLife(card));
    }

    [Fact]
    public void ADealPlacesThreePairsAndOneSingle()
    {
        var game = Dealt();
        Assert.Equal(1, game.Stage);
        Assert.Equal(30, game.Life);
        var counts = Enumerable.Range(0, CardIdentifyGame.Positions).GroupBy(position => game.Card(position).ItemId).Select(group => group.Count()).Order().ToArray();
        Assert.Equal([1, 2, 2, 2], counts);
        Assert.All(Enumerable.Range(0, 7), position => Assert.InRange(game.Card(position).Group, 1, 4));
        Assert.Equal(-1, game.Combo);
    }

    [Fact]
    public void AMatchingPairScoresByStageAndCombo()
    {
        var game = Dealt();
        var pair = Enumerable.Range(0, 7).GroupBy(position => game.Card(position).ItemId).First(group => group.Count() == 2).ToArray();
        Assert.NotNull(game.Flip(pair[0]));
        Assert.Null(game.Flip(pair[0]));
        Assert.NotNull(game.Flip(pair[1]));
        var outcome = game.Pair(pair[0], pair[1], Combos);
        Assert.True(outcome!.Value.Matched);
        Assert.Equal(0, game.Combo);
        Assert.Equal(10, game.Score);
        Assert.Equal(game.SpecialPosition == pair[0] || game.SpecialPosition == pair[1] ? 35 : 32, game.Life);
        Assert.Null(game.Pair(pair[0], pair[1], Combos));
    }

    [Fact]
    public void AMissTurnsTheCardsBackCostsLifeAndResetsTheCombo()
    {
        var game = Dealt(7);
        var first = 0;
        var second = Enumerable.Range(1, 6).First(position => game.Card(position).ItemId != game.Card(first).ItemId);
        game.Flip(first);
        game.Flip(second);
        var outcome = game.Pair(first, second, Combos)!.Value;
        Assert.False(outcome.Matched);
        Assert.Equal(13, game.Life);
        Assert.Equal(CardIdentifyGame.FaceDown, game.State(first));
        Assert.False(outcome.Ends);
        game.Flip(first);
        game.Flip(second);
        game.Pair(first, second, Combos);
        game.Flip(first);
        game.Flip(second);
        Assert.True(game.Pair(first, second, Combos)!.Value.Ends);
    }

    [Fact]
    public void TheSingleCardIsTakenOnlyWhenFaceUp()
    {
        var game = Dealt();
        var single = Enumerable.Range(0, 7).GroupBy(position => game.Card(position).ItemId).First(group => group.Count() == 1).Single();
        Assert.Null(game.Take(single));
        game.Flip(single);
        var taken = game.Take(single)!.Value;
        Assert.Equal(game.Card(single).ItemId, taken.ItemId);
        Assert.Equal(CardIdentifyGame.Taken, game.State(single));
    }

    [Fact]
    public void SpecialChanceFollowsLifeSteps()
    {
        Assert.Equal(0, CardIdentifyGame.SpecialChance(4));
        Assert.Equal(30, CardIdentifyGame.SpecialChance(5));
        Assert.Equal(25, CardIdentifyGame.SpecialChance(12));
        Assert.Equal(20, CardIdentifyGame.SpecialChance(19));
        Assert.Equal(0, CardIdentifyGame.SpecialChance(20));
    }

    [Fact]
    public void EndRewardUsesTheLevelFormulas()
    {
        Assert.Equal(new CardIdentifyReward(0, 9166), CardIdentifyGame.Reward(100, 100, 0f, 1f));
        Assert.Equal(new CardIdentifyReward(9333, 9166), CardIdentifyGame.Reward(100, 100, 1f, 1f));
        Assert.Equal(default, CardIdentifyGame.Reward(0, 100, 1f, 1f));
        Assert.Equal(0u, CardIdentifyGame.Reward(100, 1300, 0f, 1f).TmExp);
    }
}
