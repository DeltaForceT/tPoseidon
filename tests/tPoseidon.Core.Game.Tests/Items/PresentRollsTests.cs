using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public class PresentRollsTests
{
    private static PresentItemEntry Box(int type, int dropCount, params PresentDrop[] drops) =>
        new(9000, type, dropCount, drops.Concat(Enumerable.Repeat(new PresentDrop(0, 0, 0), PresentItemEntry.DropColumns - drops.Length)).ToArray());

    [Fact]
    public void WeightedBoxPicksByRate()
    {
        var box = Box(PresentRolls.WeightedType, 1, new PresentDrop(101, 10, 1), new PresentDrop(102, 30, 2));

        Assert.Equal([new PresentGrant(101, 1)], PresentRolls.Roll(box, _ => 9));
        Assert.Equal([new PresentGrant(102, 2)], PresentRolls.Roll(box, _ => 10));
        Assert.Equal([new PresentGrant(102, 2)], PresentRolls.Roll(box, _ => 39));
    }

    [Fact]
    public void WeightedBoxRollsOncePerDrop()
    {
        var box = Box(PresentRolls.WeightedType, 3, new PresentDrop(101, 1, 1), new PresentDrop(102, 1, 1));
        var rolls = new Queue<int>([0, 1, 0]);

        var grants = PresentRolls.Roll(box, _ => rolls.Dequeue());

        Assert.Equal([101, 102, 101], grants.Select(grant => grant.ItemId));
    }

    [Fact]
    public void InOrderBoxGivesTheFirstDropsWithoutRolling()
    {
        var box = Box(PresentRolls.InOrderType, 2, new PresentDrop(201, 0, 5), new PresentDrop(202, 0, 1), new PresentDrop(203, 0, 1));

        var grants = PresentRolls.Roll(box, _ => throw new InvalidOperationException());

        Assert.Equal([new PresentGrant(201, 5), new PresentGrant(202, 1)], grants);
    }

    [Fact]
    public void ZeroCountGivesOne()
    {
        var box = Box(PresentRolls.InOrderType, 1, new PresentDrop(301, 0, 0));

        Assert.Equal([new PresentGrant(301, PresentRolls.SmallestCount)], PresentRolls.Roll(box, _ => 0));
    }

    [Fact]
    public void EmptyBoxGivesNothing()
    {
        Assert.Empty(PresentRolls.Roll(Box(PresentRolls.WeightedType, 1), _ => 0));
        Assert.Empty(PresentRolls.Roll(Box(PresentRolls.WeightedType, 0, new PresentDrop(101, 1, 1)), _ => 0));
    }

    [Fact]
    public void MergeJoinsOnlyStackableItems()
    {
        var merged = PresentRolls.Merge(
            [new PresentGrant(1, 2), new PresentGrant(2, 1), new PresentGrant(1, 3), new PresentGrant(2, 1)],
            itemId => itemId == 1);

        Assert.Equal([new PresentGrant(1, 5), new PresentGrant(2, 1), new PresentGrant(2, 1)], merged);
    }
}
