using tPoseidon.Core.Game.CardBattle;

namespace tPoseidon.Core.Game.Tests.CardBattle;

public sealed class CardBattleMatchTests
{
    private static BattleCard Card(ulong uid, byte number, uint type, uint tieBreak = 0, byte steal = 0) => new(uid, 100 + (uint)uid, type, number, 0, tieBreak, steal);

    private static CardBattleMatch Match(BattleCard[] master, BattleCard[] opponent, byte rule = 0)
    {
        var match = new CardBattleMatch(1, new BattleSide(1, 1, "a", 10)) { Opponent = new BattleSide(2, 2, "b", 10), Rule = rule, State = BattleState.Battle };
        match.Master.Deck = master;
        match.Opponent.Deck = opponent;
        return match;
    }

    [Fact]
    public void DeckRules()
    {
        BattleCard[] good = [Card(1, 1, 0), Card(2, 1, 1), Card(3, 5, 2), Card(4, 6, 2), Card(5, 15, 3)];
        Assert.Equal(CardBattleRules.CodeOk, CardBattleRules.CheckDeck(good, _ => true));
        Assert.Equal(CardBattleRules.CodeDuplicate, CardBattleRules.CheckDeck(good, card => card.ItemUid != 3));
        Assert.Equal(CardBattleRules.CodeBadNumber, CardBattleRules.CheckDeck([.. good[..4], Card(5, 16, 3)], _ => true));
        Assert.Equal(CardBattleRules.CodeDuplicate, CardBattleRules.CheckDeck([.. good[..4], Card(5, 1, 3)], _ => true));
        Assert.Equal(CardBattleRules.CodeDuplicate, CardBattleRules.CheckDeck([.. good[..4], Card(5, 9, 2)], _ => true));
        Assert.Equal(CardBattleRules.CodeUsedResult, CardBattleRules.CheckDeck([.. good[..4], Card(5, 9, 3) with { Result = 1 }], _ => true));
        Assert.Equal(CardBattleRules.CodeNoItem, CardBattleRules.CheckDeck([.. good[..4], Card(5, 9, 3) with { ItemId = 0 }], _ => true));
    }

    [Fact]
    public void ComparatorFollowsTheRule()
    {
        Assert.Equal(1, CardBattleRules.Compare(0, Card(1, 3, 1), Card(2, 9, 1)));
        Assert.Equal(-1, CardBattleRules.Compare(0, Card(1, 3, 1), Card(2, 9, 2)));
        Assert.Equal(-1, CardBattleRules.Compare(1, Card(1, 3, 1), Card(2, 9, 1)));
        Assert.Equal(1, CardBattleRules.Compare(1, Card(1, 3, 1), Card(2, 9, 2)));
        Assert.Equal(1, CardBattleRules.Compare(0, Card(1, 7, 1, 5), Card(2, 7, 2, 4)));
        Assert.Equal(0, CardBattleRules.Compare(0, Card(1, 7, 1, 5), Card(2, 7, 2, 5)));
    }

    [Fact]
    public void ScoreAndStreak()
    {
        var match = Match([Card(1, 3, 1), Card(2, 3, 2), Card(3, 2, 3), Card(4, 5, 4), Card(5, 6, 5)],
            [Card(11, 9, 1), Card(12, 9, 2), Card(13, 10, 3), Card(14, 1, 0), Card(15, 1, 1)]);
        match.Master.Dropped = 0;
        match.Opponent!.Dropped = 0;
        var (winner, outcome) = match.Resolve();
        Assert.Same(match.Master, winner);
        Assert.Equal(RoundOutcome.NextRound, outcome);
        Assert.Equal((byte)1, match.Master.Score.Wins);
        Assert.Equal((short)6, match.Master.Score.Score);
        Assert.Equal((short)-6, match.Opponent.Score.LastDelta);
        Assert.Equal(CardBattleRules.ResultWon, match.Master.Deck[0].Result);
        match.Master.Dropped = 1;
        match.Opponent.Dropped = 1;
        match.Resolve();
        Assert.Equal((byte)2, match.Master.Score.Streak);
        Assert.Equal((short)(6 + 6 + 2), match.Master.Score.Score);
        match.Master.Dropped = 3;
        match.Opponent.Dropped = 2;
        match.Resolve();
        Assert.Equal((byte)1, match.Opponent.Score.Wins);
        Assert.Equal((byte)0, match.Master.Score.Streak);
    }

    [Fact]
    public void StealFlagTakesAWinFromTheLoser()
    {
        var match = Match([Card(1, 3, 1, 0, 1), Card(2, 9, 1), Card(3, 1, 0), Card(4, 1, 1), Card(5, 1, 2)],
            [Card(11, 9, 1), Card(12, 3, 1), Card(13, 1, 3), Card(14, 1, 4), Card(15, 1, 5)]);
        match.Master.Dropped = 1;
        match.Opponent!.Dropped = 1;
        match.Resolve();
        Assert.Equal((byte)1, match.Opponent.Score.Wins);
        match.Master.Dropped = 0;
        match.Opponent.Dropped = 0;
        match.Resolve();
        Assert.Equal((byte)1, match.Master.Score.Wins);
        Assert.Equal((byte)0, match.Opponent.Score.Wins);
    }

    [Fact]
    public void FourRoundsThenExtraThenDraw()
    {
        var master = Enumerable.Range(1, 5).Select(i => Card((ulong)i, 7, (uint)(i % 3), 1)).ToArray();
        var opponent = Enumerable.Range(11, 5).Select(i => Card((ulong)i, 7, (uint)(i % 3), 1)).ToArray();
        var match = Match(master, opponent);
        var outcome = RoundOutcome.NextRound;
        for (var slot = 0; slot < 4; slot++)
        {
            match.Master.Dropped = slot;
            match.Opponent!.Dropped = slot;
            outcome = match.Resolve().Outcome;
        }
        Assert.Equal(RoundOutcome.ExtraRound, outcome);
        Assert.Equal(4, match.Round);
        match.Master.Dropped = 4;
        match.Opponent!.Dropped = 4;
        Assert.Equal(RoundOutcome.Draw, match.Resolve().Outcome);
        Assert.Equal(BattleState.Result, match.State);
        Assert.Null(match.Winner);
    }

    [Fact]
    public void ChanceNeedsAnUnusedTypeFiveCardWhenBehind()
    {
        BattleCard[] deck = [Card(1, 1, 5)];
        Assert.True(CardBattleRules.ChanceAvailable(false, deck, 0, 2));
        Assert.False(CardBattleRules.ChanceAvailable(true, deck, 0, 2));
        Assert.False(CardBattleRules.ChanceAvailable(false, deck, 1, 1));
        Assert.False(CardBattleRules.ChanceAvailable(false, [Card(1, 1, 4)], 0, 1));
        Assert.True(CardBattleRules.Steals(9));
        Assert.False(CardBattleRules.Steals(10));
    }
}
