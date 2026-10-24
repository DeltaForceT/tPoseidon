using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.CardBattle;

public sealed class SpecialCardSets
{
    public const int PresentSlots = 100;

    private readonly IReadOnlyList<CardSpecialSetEntry> _sets;

    public SpecialCardSets(IReadOnlyList<CardSpecialSetEntry> sets)
    {
        _sets = sets ?? throw new ArgumentNullException(nameof(sets));
    }

    public static SpecialCardSets None { get; } = new(Array.Empty<CardSpecialSetEntry>());

    public CardSpecialSetEntry? Held(IReadOnlyList<BattleCard> deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        CardSpecialSetEntry? best = null;
        foreach (var set in _sets)
        {
            if (set.Grade <= (best?.Grade ?? 0) || !Complete(set, deck)) continue;
            best = set;
        }
        return best;
    }

    public static uint PickCard(CardSpecialSetEntry set, Func<int, int> roll)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(roll);
        var cards = set.CardIds.Where(card => card != 0).ToArray();
        return cards.Length == 0 ? 0 : (uint)cards[roll(cards.Length)];
    }

    public static uint PickPresent(CardSpecialSetEntry set, Func<int, int> roll)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(roll);
        var slots = new List<int>(PresentSlots);
        foreach (var (itemId, rate) in set.Presents)
        {
            if (itemId == 0) continue;
            var count = (int)Math.Round(rate * PresentSlots, MidpointRounding.AwayFromZero);
            for (var i = 0; i < count && slots.Count < PresentSlots; i++) slots.Add(itemId);
        }
        var pick = roll(PresentSlots);
        return pick < slots.Count ? (uint)slots[pick] : 0;
    }

    private static bool Complete(CardSpecialSetEntry set, IReadOnlyList<BattleCard> deck)
    {
        var required = set.CardIds.Count(card => card != 0);
        var matches = set.CardIds.Where(card => card != 0).Sum(card => deck.Count(held => held.ItemId == card));
        return matches == required;
    }
}
