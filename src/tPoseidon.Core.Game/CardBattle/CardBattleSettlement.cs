namespace tPoseidon.Core.Game.CardBattle;

public readonly record struct CardBattlePayout(
    uint WonCardItemId,
    uint LostCardItemId,
    uint SecondWonCardItemId,
    uint SecondLostCardItemId,
    uint PresentItemId,
    ulong OpponentCharacterId);

public static class CardBattleSettlement
{
    public static (CardBattlePayout Winner, CardBattlePayout Loser) Settle(BattleSide winner, BattleSide loser, Func<int, int> roll, Func<uint> newCard)
    {
        ArgumentNullException.ThrowIfNull(winner);
        ArgumentNullException.ThrowIfNull(loser);
        ArgumentNullException.ThrowIfNull(roll);
        ArgumentNullException.ThrowIfNull(newCard);
        uint won = 0;
        uint lost = 0;
        if (loser.Set is { } loserSet)
        {
            won = SpecialCardSets.PickCard(loserSet, roll);
            lost = won;
        }
        else if (CardBattleRules.Steals(roll(100)))
        {
            won = winner.ExtraCards.Count > 0 ? winner.ExtraCards[roll(winner.ExtraCards.Count)].ItemId : newCard();
        }
        var second = loser.ChanceCardItemId;
        var present = winner.Set is { } winnerSet ? SpecialCardSets.PickPresent(winnerSet, roll) : 0;
        return (
            new CardBattlePayout(won, 0, second, 0, present, loser.CharacterId),
            new CardBattlePayout(0, lost, 0, second, 0, 0));
    }
}
