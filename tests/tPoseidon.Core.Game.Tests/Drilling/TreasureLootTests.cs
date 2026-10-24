using tPoseidon.Core.Game.Drilling;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Drilling;

public sealed class TreasureLootTests
{
    private static TreasureItemEntry Row(int option, int count, int sideItem = 0, int sideMin = 0, int sideMax = 0)
    {
        var slots = Enumerable.Range(0, TreasureMapTables.LootSlots)
            .Select(slot => slot < 3 ? new TreasureLootSlot(100 + slot, 10f, 5) : new TreasureLootSlot(0, 0f, 0))
            .ToArray();
        return new TreasureItemEntry(63935, 15, [16], option, count, slots, sideItem, sideMin, sideMax);
    }

    [Fact]
    public void FixedCountWhenOptionBitTwoIsSet()
    {
        Assert.Equal(1, TreasureLoot.Count(Row(2, 1), -6));
        Assert.Equal(1, TreasureLoot.Count(Row(3, 1), 6));
    }

    [Fact]
    public void FortuneCountRunsFromOneToFive()
    {
        Assert.Equal(5, TreasureLoot.Count(Row(0, 0), -6));
        Assert.Equal(1, TreasureLoot.Count(Row(0, 0), 0));
        Assert.Equal(2, TreasureLoot.Count(Row(0, 0), 6));
        Assert.All(Enumerable.Range(-6, 13), value => Assert.InRange(TreasureLoot.Count(Row(0, 0), value), 1, 5));
    }

    [Fact]
    public void TodayValueFollowsTheCharacterCycle()
    {
        Assert.Equal(0, TreasureLoot.TodayValue(0, 1000, 1.0));
        Assert.Equal(0, TreasureLoot.TodayValue(9, 1000, 1.0));
        var seconds = 23 * Math.PI / 2;
        Assert.Equal(-6, TreasureLoot.TodayValue(1, seconds, 1.0));
        Assert.Equal(6, TreasureLoot.TodayValue(2, seconds, 1.0));
    }

    [Fact]
    public void WithoutDuplicatesEachSlotOnceAndAttemptsStop()
    {
        var rolls = new Queue<double>([0.0, 0.0, 0.5, 0.0, 0.9]);
        var picked = TreasureLoot.Pick(Row(0, 0), 5, () => rolls.Count > 0 ? rolls.Dequeue() : 0.0, () => 0);
        Assert.Equal([(100, 5), (101, 5), (102, 5)], picked);
        var duplicates = TreasureLoot.Pick(Row(1, 0), 3, () => 0.0, () => 0);
        Assert.Equal(3, duplicates.Count);
        Assert.All(duplicates, item => Assert.Equal(100, item.ItemId));
    }

    [Fact]
    public void SideItemsNeverReachTheMaximum()
    {
        Assert.Equal(2, TreasureLoot.SideCount(2, 5, 0));
        Assert.Equal(4, TreasureLoot.SideCount(2, 5, TreasureLoot.RandRange - 1));
        var picked = TreasureLoot.Pick(Row(0, 0, 777, 3, 4), 0, () => 0.0, () => 0);
        Assert.Equal(3, picked.Count(item => item.ItemId == 777));
    }
}
