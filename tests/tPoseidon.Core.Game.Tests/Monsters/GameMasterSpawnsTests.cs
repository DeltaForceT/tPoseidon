using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.GameTests.Monsters;

public class GameMasterSpawnsTests
{
    private static ZoneGeometry Field(params (int X, int Y)[] blocked)
    {
        var cells = new byte[40 * 40];
        foreach (var (x, y) in blocked) cells[y * 40 + x] = ZoneGeometry.CellBlocked;
        return new ZoneGeometry
        {
            MinX = 0,
            MinY = 0,
            MaxX = 639,
            MaxY = 639,
            CellsX = 40,
            CellsY = 40,
            CellAttributes = cells,
            Regions = Array.Empty<ZoneRegion>(),
        };
    }

    private static MonsterEntity Monster(ushort id) => new()
    {
        EntityId = id,
        MapId = 1,
        MonsterParamId = 2001,
        Name = "Wolf",
        SpawnX = 0,
        SpawnY = 0,
        MaxHp = 10,
        Level = 1,
    };

    [Fact]
    public void ARadiusOfZeroPutsEveryMonsterOnTheCentre()
    {
        var spots = GameMasterSpawnArea.Scatter(300, 200, 0, 5, null, new Random(1));

        Assert.Equal(5, spots.Count);
        Assert.All(spots, spot => Assert.Equal(((ushort)300, (ushort)200), spot));
    }

    [Fact]
    public void MonstersLandInsideTheCircleAndSpreadOut()
    {
        var spots = GameMasterSpawnArea.Scatter(300, 300, 100, 200, null, new Random(7));

        Assert.Equal(200, spots.Count);
        Assert.All(spots, spot => Assert.True(Math.Sqrt(Math.Pow(spot.X - 300, 2) + Math.Pow(spot.Y - 300, 2)) <= 100.5));
        Assert.True(spots.Distinct().Count() > 100);
    }

    [Fact]
    public void ASpotNearTheEdgeOfTheWorldNeverWrapsAround()
    {
        var spots = GameMasterSpawnArea.Scatter(10, 10, 500, 100, null, new Random(3));

        Assert.All(spots, spot => Assert.True(spot.X <= 510 && spot.Y <= 510));
    }

    [Fact]
    public void BlockedAndOffMapCellsAreAvoidedWhenTheMapHasGeometry()
    {
        var blocked = Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 20).Select(y => (x, y))).ToArray();
        var geometry = Field(blocked);

        var spots = GameMasterSpawnArea.Scatter(320, 320, 300, 200, geometry, new Random(11));

        Assert.All(spots, spot =>
        {
            var cellX = ZoneGeometry.CellOf(spot.X);
            var cellY = ZoneGeometry.CellOf(spot.Y);
            var onCentre = spot == ((ushort)320, (ushort)320);
            Assert.True(onCentre || (geometry.ContainsCell(cellX, cellY) && geometry.IsFreeDestination(cellX, cellY)));
        });
    }

    [Fact]
    public void WhenNoFreeSpotIsFoundTheMonsterStaysOnTheCentre()
    {
        var everything = Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 40).Select(y => (x, y))).ToArray();

        var spots = GameMasterSpawnArea.Scatter(320, 320, 200, 4, Field(everything), new Random(5));

        Assert.All(spots, spot => Assert.Equal(((ushort)320, (ushort)320), spot));
    }

    [Fact]
    public void TheLedgerGivesBackOnlyTheMonstersOfThatGameMasterOnceAndForgetsThem()
    {
        var ledger = new GameMasterSpawnLedger();
        var first = Monster(1);
        var second = Monster(2);
        var other = Monster(3);
        ledger.Record(0x4001, first);
        ledger.Record(0x4001, second);
        ledger.Record(0x4002, other);

        var mine = ledger.Take(0x4001);

        Assert.Equal([first, second], mine);
        Assert.Empty(ledger.Take(0x4001));
        Assert.Equal([other], ledger.Take(0x4002));
        Assert.Empty(ledger.Take(0x4003));
    }
}
