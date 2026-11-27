using tPoseidon.Core.Game.Fortune;
using tPoseidon.Core.Game.Monsters;
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

    public static int TodayValue(FortuneTables tables, int characterType, DateTimeOffset createdAt, DateTimeOffset now) =>
        FortuneToday.CardId(tables, characterType, createdAt, now);

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

    public const int ScatterFirstRing = 4;
    public const int ScatterReach = 8;

    public static IReadOnlyList<(ushort X, ushort Y)> Scatter(ZoneGeometry? geometry, ushort originX, ushort originY, int count, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var takenCells = new HashSet<(int X, int Y)>();
        var positions = new List<(ushort X, ushort Y)>(count);
        for (var index = 0; index < count; index++)
            positions.Add(DropPlacement.Place(geometry, originX, originY, ScatterFirstRing, ScatterReach, takenCells, rng));
        return positions;
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
