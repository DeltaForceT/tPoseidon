using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.CardBattle;

public sealed class CardGenerator
{
    public const int SlotCount = 1000;

    private readonly int[] _slots = new int[SlotCount];
    private readonly Dictionary<int, CardGenCardEntry> _cards;
    private readonly Func<int, int> _roll;
    private readonly Func<DateOnly> _today;
    private Dictionary<int, int> _daily = new();
    private DateOnly _countedOn;

    public CardGenerator(CardGenTables tables, Func<int, int> roll, Func<DateOnly> today)
    {
        ArgumentNullException.ThrowIfNull(tables);
        _roll = roll ?? throw new ArgumentNullException(nameof(roll));
        _today = today ?? throw new ArgumentNullException(nameof(today));
        _cards = tables.Cards.ToDictionary(card => card.ItemId);
        _countedOn = _today();
        FillSlots(tables);
    }

    public int FilledSlots => _slots.Count(slot => slot != 0);

    public int SlotsOf(int itemId) => _slots.Count(slot => slot == itemId);

    public CardGenCardEntry? Find(int itemId) => _cards.TryGetValue(itemId, out var card) ? card : null;

    public CardGenCardEntry? Generate()
    {
        var start = _roll(SlotCount);
        var downwards = _roll(2) == 0;
        var index = start;
        while (true)
        {
            index = Step(index, downwards);
            if (_slots[index] != 0 && _cards.TryGetValue(_slots[index], out var card))
            {
                ResetCountsOnNewDay();
                if (Available(card))
                {
                    Count(card.ItemId);
                    return card;
                }
            }
            if (index == start) return null;
        }
    }

    private static int Step(int index, bool downwards)
    {
        if (downwards) return index == 0 ? SlotCount - 1 : index - 1;
        return index == SlotCount - 1 ? 0 : index + 1;
    }

    private void ResetCountsOnNewDay()
    {
        var today = _today();
        if (_countedOn == today) return;
        _daily.Clear();
        _countedOn = today;
    }

    private int CountOf(int itemId) => _daily.TryGetValue(itemId, out var count) ? count : -1;

    private bool Available(CardGenCardEntry card)
    {
        if (card.DailyLimit == 0) return true;
        var count = CountOf(card.ItemId);
        return count < 0 || count < card.DailyLimit;
    }

    private void Count(int itemId)
    {
        _daily[itemId] = CountOf(itemId) < 0 ? 1 : _daily[itemId] + 1;
    }

    private void FillSlots(CardGenTables tables)
    {
        var grades = tables.Grades;
        if (grades.Count == 0) return;
        var total = 0f;
        foreach (var grade in grades) total += grade.Param;
        var gradeShare = grades.Select(grade => grade.Param / total).ToArray();
        var clamped = tables.Cards.Select(card => Math.Clamp(card.Grade, 1, grades.Count)).ToArray();
        var sums = new float[grades.Count];
        for (var i = 0; i < tables.Cards.Count; i++) sums[clamped[i] - 1] += tables.Cards[i].Param;
        for (var i = 0; i < sums.Length; i++)
        {
            if (sums[i] <= 0f) sums[i] = 1f;
        }
        for (var g = 0; g < grades.Count; g++)
        {
            var slotsOfGrade = (double)gradeShare[g] * SlotCount;
            for (var i = 0; i < tables.Cards.Count; i++)
            {
                if (clamped[i] != grades[g].Grade) continue;
                var itemShare = tables.Cards[i].Param / sums[clamped[i] - 1];
                Place(tables.Cards[i].ItemId, (int)(itemShare * slotsOfGrade));
            }
        }
    }

    private void Place(int itemId, int count)
    {
        for (var placed = 0; placed < count; placed++)
        {
            var start = _roll(SlotCount);
            var downwards = _roll(2) == 0;
            var index = start;
            do
            {
                index = Step(index, downwards);
                if (_slots[index] != 0) continue;
                _slots[index] = itemId;
                break;
            }
            while (index != start);
        }
    }
}
