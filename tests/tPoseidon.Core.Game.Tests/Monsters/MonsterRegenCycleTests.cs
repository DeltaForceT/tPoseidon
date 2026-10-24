using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Monsters;

public sealed class MonsterRegenCycleTests
{
    private const ushort MapId = 153;
    private const int Walker = 2100;
    private const int Runner = 2101;
    private const int Boss = 2102;
    private const int OutOfRange = 5000;

    private static readonly InMemoryMonsterParamLookup Catalog = new(new[]
    {
        new MonsterParamEntry { Id = Walker, Name = "Walker", MaxHpLv = 10 },
        new MonsterParamEntry { Id = Runner, Name = "Runner", MaxHpLv = 10 },
        new MonsterParamEntry { Id = Boss, Name = "Boss", MaxHpLv = 10, Type = (int)MonsterAType.BossMonster },
        new MonsterParamEntry { Id = OutOfRange, Name = "Stray", MaxHpLv = 10 },
    });

    private static ZoneGeometry Field(params ZonePoint[] points) => new()
    {
        MinX = 0,
        MinY = 0,
        MaxX = 639,
        MaxY = 639,
        CellsX = 40,
        CellsY = 40,
        CellAttributes = new byte[40 * 40],
        Regions = Array.Empty<ZoneRegion>(),
        Points = points,
    };

    private static (MonsterRegenCycle Cycle, MonsterManager Monsters) Make(Random? rng = null)
    {
        var monsters = new MonsterManager(Catalog, new IdAllocator());
        return (new MonsterRegenCycle(monsters, Catalog, rng), monsters);
    }

    [Fact]
    public void EachPassAddsOneMonsterPerRowUntilTheAverageIsReached()
    {
        var (cycle, monsters) = Make();
        cycle.Configure(
            MapId, mapCap: 200,
            new[] { new MonsterRegenRow(Walker, 1, Average: 2, Max: 5, Ratio: 2), new MonsterRegenRow(Runner, 2, Average: 1, Max: 5, Ratio: 2) },
            geometry: null);

        Assert.Equal(new[] { Walker, Runner }, cycle.Pass(MapId).Select(m => m.MonsterParamId).ToArray());
        Assert.Equal(new[] { Walker }, cycle.Pass(MapId).Select(m => m.MonsterParamId).ToArray());
        Assert.Empty(cycle.Pass(MapId));
        Assert.Equal(3, monsters.GetByMap(MapId).Count);
    }

    [Fact]
    public void MaxLimitsARowBelowItsAverage()
    {
        var (cycle, _) = Make();
        cycle.Configure(MapId, 200, new[] { new MonsterRegenRow(Walker, 1, Average: 5, Max: 1, Ratio: 2) }, null);

        Assert.Single(cycle.Pass(MapId));
        Assert.Empty(cycle.Pass(MapId));
    }

    [Fact]
    public void NoPassRunsOnceTheMapHoldsItsCap()
    {
        var (cycle, _) = Make();
        cycle.Configure(MapId, mapCap: 1, new[] { new MonsterRegenRow(Walker, 1, Average: 5, Max: 5, Ratio: 2) }, null);

        Assert.Single(cycle.Pass(MapId));
        Assert.Empty(cycle.Pass(MapId));
    }

    [Fact]
    public void RowWithoutAnAverageGetsItsShareOfTheCapByRatio()
    {
        var (cycle, _) = Make();
        cycle.Configure(
            MapId, mapCap: 10,
            new[] { new MonsterRegenRow(Walker, 1, Average: 0, Max: 50, Ratio: 1), new MonsterRegenRow(Runner, 2, Average: 0, Max: 50, Ratio: 3) },
            null);

        var born = new List<MonsterEntity>();
        for (var pass = 0; pass < 20; pass++) born.AddRange(cycle.Pass(MapId));

        Assert.Equal(2, born.Count(m => m.MonsterParamId == Walker));
        Assert.Equal(7, born.Count(m => m.MonsterParamId == Runner));
    }

    [Fact]
    public void DeadMonsterFreesItsRowForAReplacement()
    {
        var (cycle, _) = Make();
        cycle.Configure(MapId, 200, new[] { new MonsterRegenRow(Walker, 1, Average: 1, Max: 1, Ratio: 2) }, null);
        var first = Assert.Single(cycle.Pass(MapId));
        Assert.Empty(cycle.Pass(MapId));

        first.ApplyDamage(first.MaxHp);
        cycle.Release(first);

        var replacement = Assert.Single(cycle.Pass(MapId));
        Assert.NotEqual(first.EntityId, replacement.EntityId);
        Assert.Equal(1, replacement.RegionId);
    }

    [Fact]
    public void MonsterAppearsOnAPointOfItsRegion()
    {
        var (cycle, _) = Make();
        var geometry = Field(
            new ZonePoint(2, ZoneGeometry.PointTypeRegionSpawn, 0, 500, 500),
            new ZonePoint(1, ZoneGeometry.PointTypeRegionSpawn, 0, 120, 140),
            new ZonePoint(0, ZoneGeometry.PointTypeSpawn, 0, 300, 300));
        cycle.Configure(MapId, 200, new[] { new MonsterRegenRow(Walker, 1, Average: 1, Max: 1, Ratio: 2) }, geometry);

        var monster = Assert.Single(cycle.Pass(MapId));

        Assert.Equal((120, 140), (monster.SpawnX, monster.SpawnY));
    }

    [Fact]
    public void WithoutRegionPointsItUsesAGeneralPointThenTheFirstEntryThenTheFallback()
    {
        var (general, _) = Make();
        general.Configure(
            MapId, 200, new[] { new MonsterRegenRow(Walker, 9, Average: 1, Max: 1, Ratio: 2) },
            Field(new ZonePoint(0, ZoneGeometry.PointTypeSpawn, 0, 300, 310), new ZonePoint(0, ZoneGeometry.PointTypeEntry, 33, 40, 50)));
        var (entry, _) = Make();
        entry.Configure(
            MapId, 200, new[] { new MonsterRegenRow(Walker, 0, Average: 1, Max: 1, Ratio: 2) },
            Field(new ZonePoint(0, ZoneGeometry.PointTypeEntry, 33, 40, 50), new ZonePoint(0, ZoneGeometry.PointTypeEntry, 34, 60, 70)));
        var (fallback, _) = Make();
        fallback.Configure(MapId, 200, new[] { new MonsterRegenRow(Walker, 0, Average: 1, Max: 1, Ratio: 2) }, Field());

        var onGeneral = Assert.Single(general.Pass(MapId));
        var onEntry = Assert.Single(entry.Pass(MapId));
        var onFallback = Assert.Single(fallback.Pass(MapId));

        Assert.Equal((300, 310), (onGeneral.SpawnX, onGeneral.SpawnY));
        Assert.Equal((40, 50), (onEntry.SpawnX, onEntry.SpawnY));
        Assert.Equal(
            (MonsterRegenCycle.FallbackX, MonsterRegenCycle.FallbackY),
            (onFallback.SpawnX, onFallback.SpawnY));
    }

    [Fact]
    public void BossTypesAndIdsOutsideTheRangesAreNotCreated()
    {
        var (cycle, _) = Make();
        cycle.Configure(
            MapId, 200,
            new[] { new MonsterRegenRow(Boss, 1, Average: 1, Max: 1, Ratio: 2), new MonsterRegenRow(OutOfRange, 1, Average: 1, Max: 1, Ratio: 2) },
            null);

        Assert.Empty(cycle.Pass(MapId));
        Assert.True(MonsterRegenCycle.IsSpawnableId(2000));
        Assert.True(MonsterRegenCycle.IsSpawnableId(3434));
        Assert.False(MonsterRegenCycle.IsSpawnableId(3435));
        Assert.True(MonsterRegenCycle.IsSpawnableId(16998));
        Assert.False(MonsterRegenCycle.IsSpawnableId(16999));
    }
}
