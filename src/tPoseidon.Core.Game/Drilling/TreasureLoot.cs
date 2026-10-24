using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Drilling;

public static class TreasureLoot
{
    public const int FixedCountOption = 2;
    public const int DuplicatesOption = 1;
    public const int CountOffset = 10;
    public const int CountSpread = 5;
    public const int MaxAttempts = 100;
    public const int RandRange = 32768;
    public const double DefaultMultiplier = 1.0;

    private static readonly (int Period, bool Positive)[] Cycles =
    [
        (23, false), (23, true), (33, false), (33, true), (38, false), (38, true), (28, false), (28, true),
    ];

    public static int TodayValue(int characterType, double secondsSinceBirth, double multiplier)
    {
        if (characterType < 1 || characterType > Cycles.Length) return 0;
        var (period, positive) = Cycles[characterType - 1];
        var value = (int)Math.Round(2 * Math.PI * Math.Sin(secondsSinceBirth / period) * multiplier, MidpointRounding.AwayFromZero);
        return positive ? value : -value;
    }

    public static int Count(TreasureItemEntry row, int todayValue)
    {
        ArgumentNullException.ThrowIfNull(row);
        if ((row.Option & FixedCountOption) != 0) return row.Count;
        return ((todayValue + CountOffset) % CountSpread + CountSpread) % CountSpread + 1;
    }

    public static IReadOnlyList<(int ItemId, int Count)> Pick(TreasureItemEntry row, int count, Func<double> roll, Func<int> rand15)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(roll);
        ArgumentNullException.ThrowIfNull(rand15);
        var picked = new List<(int ItemId, int Count)>();
        var chosen = new HashSet<int>();
        var duplicates = (row.Option & DuplicatesOption) != 0;
        var total = row.Slots.Sum(slot => (double)slot.Ratio);
        var failed = 0;
        while (picked.Count < count && failed < MaxAttempts && total > 0)
        {
            var slotIndex = WeightedSlot(row.Slots, roll() * total);
            var slot = row.Slots[slotIndex];
            if (slot.ItemId == 0 || slot.Count <= 0 || (!duplicates && !chosen.Add(slotIndex)))
            {
                failed++;
                continue;
            }
            picked.Add((slot.ItemId, slot.Count));
        }
        if (row.SideItem != 0)
        {
            var side = SideCount(row.SideItemMin, row.SideItemMax, rand15());
            for (var unit = 0; unit < side; unit++) picked.Add((row.SideItem, 1));
        }
        return picked;
    }

    public static int SideCount(int min, int max, int rand) => min + rand * (max - min) / RandRange;

    public const int RingInnerCells = 4;
    public const int RingOuterCells = 8;

    public static (ushort X, ushort Y) RingPosition(int centreX, int centreY, int index, int total)
    {
        var angle = 2 * Math.PI * index / Math.Max(1, total);
        var cells = RingInnerCells + index % (RingOuterCells - RingInnerCells + 1);
        var radius = cells * ZoneGeometry.CellSize;
        var x = centreX + (int)Math.Round(Math.Cos(angle) * radius);
        var y = centreY + (int)Math.Round(Math.Sin(angle) * radius);
        return ((ushort)Math.Clamp(x, 0, ushort.MaxValue), (ushort)Math.Clamp(y, 0, ushort.MaxValue));
    }

    private static int WeightedSlot(IReadOnlyList<TreasureLootSlot> slots, double pick)
    {
        var cumulative = 0.0;
        for (var index = 0; index < slots.Count; index++)
        {
            cumulative += slots[index].Ratio;
            if (pick < cumulative) return index;
        }
        return slots.Count - 1;
    }
}
