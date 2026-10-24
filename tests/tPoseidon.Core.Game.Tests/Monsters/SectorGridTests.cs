using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Monsters;

public sealed class SectorGridTests
{
    private static ZoneGeometry MapOf(int cellsX, int cellsY) => new()
    {
        MinX = 0,
        MinY = 0,
        MaxX = cellsX * ZoneGeometry.CellSize - 1,
        MaxY = cellsY * ZoneGeometry.CellSize - 1,
        CellsX = cellsX,
        CellsY = cellsY,
        CellAttributes = new byte[cellsX * cellsY],
        Regions = Array.Empty<ZoneRegion>(),
    };

    [Fact]
    public void SmallMapIsASingleSector()
    {
        var grid = new SectorGrid(MapOf(40, 40));

        Assert.Equal((1, 1), (grid.Columns, grid.Rows));
        Assert.Equal((0, 0), grid.SectorOf(600, 600));
    }

    [Fact]
    public void LargeMapIsSplitIntoSixHundredByFiveHundredSectors()
    {
        var grid = new SectorGrid(MapOf(200, 200));

        Assert.Equal((5, 6), (grid.Columns, grid.Rows));
        Assert.Equal((2, 2), grid.SectorOf(1300, 1100));
        Assert.Equal((4, 5), grid.SectorOf(3199, 3199));
    }

    [Fact]
    public void APlayerWatchesTheirSectorAndTheOnesAroundIt()
    {
        var grid = new SectorGrid(MapOf(200, 200));

        var watched = grid.SectorsWatchedFrom(new[] { (700, 600) });

        Assert.Equal(9, watched.Count);
        Assert.Contains((0, 0), watched);
        Assert.Contains((2, 2), watched);
        Assert.DoesNotContain((3, 1), watched);
    }

    [Fact]
    public void APlayerInACornerWatchesOnlySectorsInsideTheMap()
    {
        var grid = new SectorGrid(MapOf(200, 200));

        var watched = grid.SectorsWatchedFrom(new[] { (10, 10) });

        Assert.Equal(4, watched.Count);
    }
    [Fact]
    public void NeighbourhoodFollowsTheOffsetOrderAndIsClippedAtTheEdges()
    {
        var grid = new SectorGrid(MapOf(200, 200));

        Assert.Equal(
            new[] { (2, 2), (1, 2), (1, 1), (2, 1), (3, 1), (3, 2), (3, 3), (2, 3), (1, 3) },
            grid.Neighbourhood((2, 2)));
        Assert.Equal(new[] { (0, 0), (1, 0), (1, 1), (0, 1) }, grid.Neighbourhood((0, 0)));
        Assert.Equal(6, grid.Neighbourhood((0, 2)).Count);
    }

    [Fact]
    public void MovingOneSectorRightLeavesTheLeftColumnAndEntersTheRightOne()
    {
        var grid = new SectorGrid(MapOf(200, 200));

        var (leaving, entering) = grid.Change((1, 2), (2, 2));

        Assert.Equal(new[] { (0, 2), (0, 1), (0, 3) }, leaving);
        Assert.Equal(new[] { (3, 1), (3, 2), (3, 3) }, entering);
    }

    [Fact]
    public void StayingInTheSameSectorChangesNothing()
    {
        var grid = new SectorGrid(MapOf(200, 200));

        var (leaving, entering) = grid.Change((2, 2), (2, 2));

        Assert.Empty(leaving);
        Assert.Empty(entering);
    }

    [Fact]
    public void ASectorSeesOnlyItsThreeByThree()
    {
        var grid = new SectorGrid(MapOf(200, 200));

        Assert.True(grid.Sees(1300, 1100, 1900, 1600));
        Assert.False(grid.Sees(1300, 1100, 2500, 1100));
        Assert.True(new SectorGrid(MapOf(40, 40)).Sees(0, 0, 600, 600));
    }
}
