using tPoseidon.Core.Game.Drilling;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Drilling;

public sealed class RandomSpotTests
{
    private static RandomSpotEntry Row(float monsterA, float monsterB, float treasure) =>
        new(2, [(3001, monsterA), (3002, monsterB), (0, 0f), (0, 0f), (0, 0f)], 64247, treasure, 0);

    private static TreasureMapTables Tables(RandomSpotEntry row, float added = 0f) => TreasureMapTables.Empty with
    {
        RandomSpots = new Dictionary<int, RandomSpotEntry> { [row.RegionId] = row },
        ZoneRegions = new Dictionary<int, int> { [16] = 2, [17] = 9 },
        RandomSpotAddedRatio = added,
    };

    [Fact]
    public void RowComesFromTheMapsRegion()
    {
        var tables = Tables(Row(0, 0, 1));
        Assert.Equal(64247, RandomSpot.RowFor(tables, 16)!.TreasureId);
        Assert.Null(RandomSpot.RowFor(tables, 17));
        Assert.Null(RandomSpot.RowFor(tables, 18));
    }

    [Fact]
    public void ChanceIsHalfAPercentPlusDrillAndEvent()
    {
        Assert.Equal(0.005 + 0.01f + 0.002f, RandomSpot.Chance(0.01f, 0.002f), 6);
        Assert.True(RandomSpot.Triggers(0.005, 0));
        Assert.True(RandomSpot.Triggers(0.005, 163));
        Assert.False(RandomSpot.Triggers(0.005, 164));
        Assert.False(RandomSpot.Triggers(0.005, RandomSpot.RandMax));
    }

    [Fact]
    public void TreasureOnlyRowsGiveTheTreasureExceptAtRollZero()
    {
        var row = Row(0, 0, 1);
        Assert.Null(RandomSpot.Choose(row, 0));
        Assert.Equal(new RandomSpotRoll(RandomSpotKind.Treasure, 64247), RandomSpot.Choose(row, 1));
        Assert.Equal(new RandomSpotRoll(RandomSpotKind.Treasure, 64247), RandomSpot.Choose(row, RandomSpot.RandMax));
    }

    [Fact]
    public void MonsterShareSplitsByCumulativeRatio()
    {
        var row = Row(1, 1, 2);
        Assert.Equal(new RandomSpotRoll(RandomSpotKind.Monster, 3001), RandomSpot.Choose(row, 0));
        Assert.Equal(new RandomSpotRoll(RandomSpotKind.Monster, 3002), RandomSpot.Choose(row, RandomSpot.RandMax / 4 + 1));
        Assert.Equal(new RandomSpotRoll(RandomSpotKind.Monster, 3002), RandomSpot.Choose(row, RandomSpot.RandMax / 2));
        Assert.Equal(new RandomSpotRoll(RandomSpotKind.Treasure, 64247), RandomSpot.Choose(row, RandomSpot.RandMax / 2 + 1));
        Assert.Null(RandomSpot.Choose(Row(0, 0, 0), 0));
    }

    [Fact]
    public void TreasureCountMustBeOneToSixtyFour()
    {
        Assert.False(RandomSpot.CountAllowed(0));
        Assert.True(RandomSpot.CountAllowed(1));
        Assert.True(RandomSpot.CountAllowed(64));
        Assert.False(RandomSpot.CountAllowed(65));
    }
}
