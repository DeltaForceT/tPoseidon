using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Monsters;

public sealed class WanderRouteTests
{
    private const int Cells = 20;

    private static ZoneGeometry Field(params (int X, int Y, byte Attribute)[] marked)
    {
        var attributes = new byte[Cells * Cells];
        foreach (var (x, y, attribute) in marked)
        {
            attributes[y * Cells + x] = attribute;
        }
        return new ZoneGeometry
        {
            MinX = 0,
            MinY = 0,
            MaxX = Cells * ZoneGeometry.CellSize - 1,
            MaxY = Cells * ZoneGeometry.CellSize - 1,
            CellsX = Cells,
            CellsY = Cells,
            CellAttributes = attributes,
            Regions = Array.Empty<ZoneRegion>(),
        };
    }

    private static int Centre(int cell) => ZoneGeometry.CentreOf(cell);

    [Fact]
    public void DestinationInTheSameCellIsRefused()
    {
        Assert.Null(WanderRoute.Find(Field(), Centre(5), Centre(5), Centre(5) + 3, Centre(5) - 4));
    }

    [Fact]
    public void AnExactPointKeepsItsCoordinates()
    {
        var route = WanderRoute.FindToPoint(Field(), Centre(2), Centre(2), Centre(9) + 5, Centre(4) - 3);

        Assert.NotNull(route);
        Assert.Equal((Centre(9) + 5, Centre(4) - 3), (route.DestinationX, route.DestinationY));
    }

    [Fact]
    public void AnExactPointInTheSameCellIsAStraightStep()
    {
        var route = WanderRoute.FindToPoint(Field(), Centre(5), Centre(5), Centre(5) + 3, Centre(5) - 4);

        Assert.NotNull(route);
        Assert.Equal((Centre(5) + 3, Centre(5) - 4, 5.0), (route.DestinationX, route.DestinationY, route.Length));
    }

    [Fact]
    public void AnExactPointOnABlockedCellFallsBackToTheReplacementCentre()
    {
        var blocked = Field((9, 4, 1));
        var exact = WanderRoute.FindToPoint(blocked, Centre(2), Centre(2), Centre(9) + 5, Centre(4) - 3);
        var snapped = WanderRoute.Find(blocked, Centre(2), Centre(2), Centre(9) + 5, Centre(4) - 3);

        Assert.Equal((snapped?.DestinationX, snapped?.DestinationY), (exact?.DestinationX, exact?.DestinationY));
    }

    [Fact]
    public void DestinationOutsideTheMapIsRefused()
    {
        Assert.Null(WanderRoute.Find(Field(), Centre(5), Centre(5), Cells * ZoneGeometry.CellSize + 4, Centre(5)));
    }

    [Fact]
    public void DestinationIsMovedToTheCellCentre()
    {
        var route = WanderRoute.Find(Field(), Centre(5), Centre(5), 8 * ZoneGeometry.CellSize + 1, 5 * ZoneGeometry.CellSize + 15);

        Assert.NotNull(route);
        Assert.Equal((Centre(8), Centre(5)), (route.DestinationX, route.DestinationY));
        Assert.Equal(3 * ZoneGeometry.CellSize, route.Length, precision: 6);
    }

    [Fact]
    public void BlockedDestinationIsReplacedByTheCellToItsRight()
    {
        var route = WanderRoute.Find(
            Field((8, 5, ZoneGeometry.CellBlocked)), Centre(5), Centre(5), Centre(8), Centre(5));

        Assert.NotNull(route);
        Assert.Equal((Centre(9), Centre(5)), (route.DestinationX, route.DestinationY));
    }

    [Fact]
    public void ReplacementTriesTheRowBelowBeforeTheRowAbove()
    {
        var route = WanderRoute.Find(
            Field((8, 5, ZoneGeometry.CellBlocked), (9, 5, ZoneGeometry.CellBlocked)),
            Centre(5), Centre(5), Centre(8), Centre(5));

        Assert.NotNull(route);
        Assert.Equal((Centre(9), Centre(6)), (route.DestinationX, route.DestinationY));
    }

    [Fact]
    public void DestinationWithNoFreeReplacementIsRefused()
    {
        var blocked = new List<(int X, int Y, byte Attribute)> { (8, 5, ZoneGeometry.CellNoDestination) };
        for (var row = 3; row <= 8; row++)
        {
            blocked.Add((9, row, ZoneGeometry.CellBlocked));
        }

        Assert.Null(WanderRoute.Find(Field(blocked.ToArray()), Centre(5), Centre(5), Centre(8), Centre(5)));
    }

    [Fact]
    public void WallAcrossTheMapMakesTheDestinationUnreachable()
    {
        var wall = Enumerable.Range(0, Cells)
            .Select(row => (7, row, ZoneGeometry.CellBlocked))
            .ToArray();

        Assert.Null(WanderRoute.Find(Field(wall), Centre(5), Centre(5), Centre(10), Centre(5)));
    }

    [Fact]
    public void APathEndsAtTheExactPointAndStepsThroughFreeCells()
    {
        var obstacle = new[]
        {
            (7, 4, ZoneGeometry.CellBlocked),
            (7, 5, ZoneGeometry.CellBlocked),
            (7, 6, ZoneGeometry.CellBlocked),
        };
        var field = Field(obstacle);

        var path = WanderRoute.FindPathToPoint(field, Centre(5), Centre(5), Centre(9) + 3, Centre(5) - 2);

        Assert.NotNull(path);
        Assert.Equal((Centre(9) + 3, Centre(5) - 2), path[^1]);
        Assert.All(path, point => Assert.False(field.IsBlocked(ZoneGeometry.CellOf(point.X), ZoneGeometry.CellOf(point.Y))));
        Assert.True(path.Count > 4);
    }

    [Fact]
    public void APathInTheSameCellIsTheExactPoint()
    {
        Assert.Equal([(Centre(5) + 3, Centre(5) - 4)], WanderRoute.FindPathToPoint(Field(), Centre(5), Centre(5), Centre(5) + 3, Centre(5) - 4));
    }

    [Fact]
    public void APathToAWalledOffPointIsRefused()
    {
        var wall = Enumerable.Range(0, Cells).Select(row => (7, row, ZoneGeometry.CellBlocked)).ToArray();

        Assert.Null(WanderRoute.FindPathToPoint(Field(wall), Centre(5), Centre(5), Centre(10), Centre(5)));
    }

    [Fact]
    public void RouteGoesAroundAnObstacle()
    {
        var obstacle = new[]
        {
            (7, 4, ZoneGeometry.CellBlocked),
            (7, 5, ZoneGeometry.CellBlocked),
            (7, 6, ZoneGeometry.CellBlocked),
        };

        var route = WanderRoute.Find(Field(obstacle), Centre(5), Centre(5), Centre(9), Centre(5));

        Assert.NotNull(route);
        Assert.True(route.Length > 4 * ZoneGeometry.CellSize);
    }
    [Fact]
    public void WaypointsAreCellCentresEndingAtTheGoalCellCentre()
    {
        var waypoints = WanderRoute.FindWaypoints(Field(), Centre(1), Centre(1), Centre(4) + 5, Centre(1) - 3);

        Assert.NotNull(waypoints);
        Assert.Equal(new[] { (Centre(2), Centre(1)), (Centre(3), Centre(1)), (Centre(4), Centre(1)) }, waypoints);
    }

    [Fact]
    public void APathLongerThanTheWaypointLimitIsRefused()
    {
        var wide = new ZoneGeometry
        {
            MinX = 0,
            MinY = 0,
            MaxX = 200 * ZoneGeometry.CellSize - 1,
            MaxY = ZoneGeometry.CellSize - 1,
            CellsX = 200,
            CellsY = 1,
            CellAttributes = new byte[200],
            Regions = Array.Empty<ZoneRegion>(),
        };

        Assert.NotNull(WanderRoute.Find(wide, Centre(0), Centre(0), Centre(WanderRoute.MaxWaypoints), Centre(0)));
        Assert.Null(WanderRoute.Find(wide, Centre(0), Centre(0), Centre(WanderRoute.MaxWaypoints + 1), Centre(0)));
    }
}
