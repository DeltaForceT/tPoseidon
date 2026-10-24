using tPoseidon.Core.Game.CardBattle;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.CardBattle;

public sealed class CardBattleSettlementTests
{
    private static readonly CardSpecialSetEntry Bears = new(3, 10, [24007, 24110, 24123, 24222, 0], [(2004, 0.4f), (7854, 0.2f), (61043, 0.4f), (0, 0f)]);
    private static readonly CardSpecialSetEntry Dragons = new(4, 20, [24006, 24307, 24315, 0, 0], [(2004, 0.1f), (7854, 0.1f), (61046, 0.8f), (0, 0f)]);

    private static BattleCard Card(uint itemId, uint type = 1, byte steal = 0) => new(itemId, itemId, type, 1, 0, 0, steal);

    private static BattleSide Side(ulong id, params uint[] itemIds) =>
        new(id, (ushort)id, $"p{id}", 10) { Deck = itemIds.Select(itemId => Card(itemId)).ToArray() };

    [Fact]
    public void ADeckHoldsTheHighestGradeCompleteSet()
    {
        var sets = new SpecialCardSets([Bears, Dragons]);
        var deck = Side(1, 24007, 24110, 24123, 24222, 24006).Deck;
        Assert.Same(Bears, sets.Held(deck));
        Assert.Same(Dragons, sets.Held(Side(1, 24006, 24307, 24315, 1, 2).Deck));
        Assert.Null(sets.Held(Side(1, 24006, 24307, 1, 2, 3).Deck));
    }

    [Fact]
    public void AWinnerTakesACardOfTheLosersSetAndTheLoserLosesIt()
    {
        var winner = Side(1, 1, 2, 3, 4, 5);
        var loser = Side(2, 24007, 24110, 24123, 24222, 9);
        loser.Set = Bears;
        var (won, lost) = CardBattleSettlement.Settle(winner, loser, limit => limit - 1);
        Assert.Equal(24222u, won.WonCardItemId);
        Assert.Equal(24222u, lost.LostCardItemId);
        Assert.Equal(2UL, won.OpponentCharacterId);
        Assert.Equal(0UL, lost.OpponentCharacterId);
    }

    [Fact]
    public void WithoutASetOnlyTheTenPercentRollGivesACopyAndTheLoserLosesNothing()
    {
        var winner = Side(1, 1, 2, 3, 4, 5);
        var loser = Side(2, 11, 12, 13, 14, 15);
        var (hit, loserHit) = CardBattleSettlement.Settle(winner, loser, limit => limit == 100 ? 9 : 2);
        Assert.Equal(13u, hit.WonCardItemId);
        Assert.Equal(0u, loserHit.LostCardItemId);
        var (miss, _) = CardBattleSettlement.Settle(winner, loser, limit => limit == 100 ? 10 : 0);
        Assert.Equal(0u, miss.WonCardItemId);
    }

    [Fact]
    public void TheChanceCardTheLoserPlayedGoesToTheWinner()
    {
        var winner = Side(1, 1, 2, 3, 4, 5);
        var loser = Side(2, 11, 12, 13, 14, 15);
        loser.Deck[2] = Card(13, CardBattleRules.ChanceCardType, CardBattleRules.StealFlag);
        loser.ChanceUsed = true;
        var (won, lost) = CardBattleSettlement.Settle(winner, loser, _ => 99);
        Assert.Equal(13u, won.SecondWonCardItemId);
        Assert.Equal(13u, lost.SecondLostCardItemId);
    }

    [Fact]
    public void AWinnerWithASetGetsAPresentFromTheSetRates()
    {
        var winner = Side(1, 1, 2, 3, 4, 5);
        winner.Set = Dragons;
        var loser = Side(2, 11, 12, 13, 14, 15);
        Assert.Equal(61046u, CardBattleSettlement.Settle(winner, loser, _ => 50).Winner.PresentItemId);
        Assert.Equal(61046u, SpecialCardSets.PickPresent(Dragons, _ => 50));
        Assert.Equal(2004u, SpecialCardSets.PickPresent(Dragons, _ => 0));
        Assert.Equal(7854u, SpecialCardSets.PickPresent(Dragons, _ => 10));
    }

    [Fact]
    public void ASetWinnerSquaresTheStreakBonus()
    {
        var match = new CardBattleMatch(1, new BattleSide(1, 1, "a", 10)) { Opponent = new BattleSide(2, 2, "b", 10), Rule = 1, State = BattleState.Battle };
        BattleCard[] deck = [Card(1, 1), Card(2, 2), Card(3, 3), Card(4, 4), Card(5, 0)];
        match.Master.Deck = deck.Select((card, i) => card with { Number = (byte)(9 + i) }).ToArray();
        match.Opponent.Deck = deck.Select((card, i) => card with { ItemUid = card.ItemUid + 10, ItemId = card.ItemId + 10, Number = (byte)(1 + i) }).ToArray();
        match.Master.Set = Bears;
        for (var round = 0; round < 2; round++)
        {
            match.Master.Dropped = round;
            match.Opponent.Dropped = round;
            match.Resolve();
        }
        Assert.Equal(2, match.Master.Score.Streak);
        Assert.Equal(8 + 8 + 4, match.Master.Score.Score);
    }
}
