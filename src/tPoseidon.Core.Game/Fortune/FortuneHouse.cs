using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Fortune;

public enum FortuneStep
{
    Closed = 0,
    Dealt = 1,
    Changed = 2,
    Rolled = 3,
}

public enum FortuneChangeRefusal
{
    WrongStep,
    CountMismatch,
    IndexOutOfRange,
    NotABasicCard,
    DuplicateIndex,
}

public readonly record struct FortuneJoker(ulong ItemUid, uint ItemId);

public readonly record struct FortuneJokerRange(int CardId, int RangeLo, int RangeHi);

public sealed class FortuneRound
{
    private readonly List<ulong> _jokerUids = new();

    public FortuneCard[] Cards { get; private set; } = Array.Empty<FortuneCard>();

    public FortuneStep Step { get; private set; }

    public FortuneAward? Pending { get; private set; }

    public IReadOnlyList<ulong> JokerUids => _jokerUids;

    public bool InHouse => Step != FortuneStep.Closed;

    public void Deal(FortuneCard[] cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        Cards = cards;
        Step = FortuneStep.Dealt;
        Pending = null;
        _jokerUids.Clear();
    }

    public FortuneChangeRefusal? Change(IReadOnlyList<FortuneJoker> jokers, IReadOnlyList<uint> indices)
    {
        ArgumentNullException.ThrowIfNull(jokers);
        ArgumentNullException.ThrowIfNull(indices);
        if (Step is not (FortuneStep.Dealt or FortuneStep.Changed)) return FortuneChangeRefusal.WrongStep;
        if (jokers.Count != indices.Count || jokers.Count > FortuneRules.JokerSlot) return FortuneChangeRefusal.CountMismatch;
        var seen = new HashSet<uint>();
        foreach (var index in indices)
        {
            if (index > FortuneRules.JokerSlot || index >= Cards.Length) return FortuneChangeRefusal.IndexOutOfRange;
            if (Cards[index].IsBasic == 0) return FortuneChangeRefusal.NotABasicCard;
            if (!seen.Add(index)) return FortuneChangeRefusal.DuplicateIndex;
        }
        for (var i = 0; i < indices.Count; i++)
        {
            var card = Cards[indices[i]];
            Cards[indices[i]] = card with { JokerSet = 1, CardId = jokers[i].ItemId, ItemUid = jokers[i].ItemUid };
            _jokerUids.Add(jokers[i].ItemUid);
        }
        Step = FortuneStep.Changed;
        return null;
    }

    public void Rolled(FortuneAward award)
    {
        Pending = award ?? throw new ArgumentNullException(nameof(award));
        Step = FortuneStep.Rolled;
    }

    public void Close()
    {
        Step = FortuneStep.Closed;
        Pending = null;
        Cards = Array.Empty<FortuneCard>();
        _jokerUids.Clear();
    }
}

public static class FortuneHouse
{
    public const int BasicCardType = 35;
    public const int BasicCardSubType = 70;
    public const int MaxBasicCardNum = 3;
    public const int JokerItemType = 35;
    public const uint JokerCardId = 21109;
    public const float BasicCardWeight = 0.099f;
    public const float JokerSlotWeight = 0.01f;
    public const int RollEvent = 0x29;
    public const int ThreeOfAKindEvent = 0x28;
    public static readonly DateTimeOffset NoRecordDate = new(2005, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly (int FirstSlot, int SecondSlot, int Event)[] StatGroupEvents =
    [
        (0, 2, 0x24),
        (4, 5, 0x25),
        (7, 8, 0x26),
        (10, 11, 0x27),
    ];

    public static bool IsBasicCard(ItemParamEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Type == BasicCardType && item.SubType == BasicCardSubType && item.CardNum is >= 0 and <= MaxBasicCardNum;
    }

    public static IReadOnlyList<int> BasicCards(IEnumerable<ItemParamEntry> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Where(IsBasicCard).Select(item => item.Id).Distinct().ToArray();
    }

    public static FortuneCard[] Deal(IReadOnlyList<int> basicCards, Func<int, int> rand)
    {
        ArgumentNullException.ThrowIfNull(basicCards);
        ArgumentNullException.ThrowIfNull(rand);
        var cards = new FortuneCard[FortuneRules.CardCount];
        for (var slot = 0; slot < FortuneRules.JokerSlot; slot++)
        {
            var id = basicCards.Count == 0 ? 0u : (uint)basicCards[rand(basicCards.Count)];
            cards[slot] = new FortuneCard(0, 1, id, 0, BasicCardWeight);
        }
        cards[FortuneRules.JokerSlot] = new FortuneCard(0, 0, JokerCardId, 0, JokerSlotWeight);
        return cards;
    }

    public static IReadOnlyList<FortuneJokerRange> JokerRanges(IEnumerable<FortuneCardEntry> rowsInOrder)
    {
        ArgumentNullException.ThrowIfNull(rowsInOrder);
        var rows = rowsInOrder.Where(row => row.JokerProb > 0f).ToArray();
        var sum = rows.Sum(row => (double)row.JokerProb);
        var ranges = new List<FortuneJokerRange>(rows.Length);
        var lo = 0;
        foreach (var row in rows)
        {
            var weight = sum > 1 ? row.JokerProb / sum : row.JokerProb;
            var hi = lo + (int)Math.Round(weight * FortuneRules.RangeScale, MidpointRounding.AwayFromZero);
            ranges.Add(new FortuneJokerRange(row.Id, lo, hi));
            lo = hi;
        }
        return ranges;
    }

    public static uint? JokerCard(IReadOnlyList<FortuneJokerRange> ranges, Func<int, int> rand)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(rand);
        if (ranges.Count == 0 || ranges[^1].RangeHi <= 0) return null;
        while (true)
        {
            var roll = rand(FortuneRules.RangeScale);
            foreach (var range in ranges)
            {
                if (roll < range.RangeHi) return (uint)range.CardId;
            }
        }
    }

    public static FortuneDraw ExchangeJokers(FortuneDraw draw, Func<uint, uint> jokerCard)
    {
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentNullException.ThrowIfNull(jokerCard);
        if (!draw.JokerHit) return draw;
        return draw with
        {
            Results = draw.Results.Select(result => result.Joker ? result with { CardId = jokerCard(result.CardId) } : result).ToArray(),
        };
    }

    public static int Fee(IReadOnlyDictionary<int, int> feeByUse, int use)
    {
        ArgumentNullException.ThrowIfNull(feeByUse);
        if (feeByUse.Count == 0) return 0;
        return feeByUse.TryGetValue(use, out var fee) ? fee : feeByUse[feeByUse.Keys.Max()];
    }

    public static bool IsAnotherDay(DateTimeOffset last, DateTimeOffset now) =>
        last.ToLocalTime().DayOfYear != now.ToLocalTime().DayOfYear;

    public static IReadOnlyList<int> StatEvents(FortuneBonus bonus)
    {
        ArgumentNullException.ThrowIfNull(bonus);
        var events = new List<int>(StatGroupEvents.Length);
        foreach (var (first, second, eventType) in StatGroupEvents)
        {
            var any = false;
            for (var slot = first; slot <= second && slot < bonus.Stats.Count; slot++) any |= bonus.Stats[slot] != 0;
            if (any) events.Add(eventType);
        }
        return events;
    }
}
