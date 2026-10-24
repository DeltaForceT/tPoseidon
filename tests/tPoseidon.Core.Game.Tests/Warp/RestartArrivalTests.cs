using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Warp;

public sealed class RestartArrivalTests
{
    private static ZoneGeometry Town(params ZonePoint[] points) => new()
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

    [Fact]
    public void RestartPointsComeFirst()
    {
        var town = Town(
            new ZonePoint(0, ZoneGeometry.PointTypeEntry, 34, 40, 50),
            new ZonePoint(0, ZoneGeometry.PointTypeRestart, 0, 300, 310));

        Assert.Equal((300, 310), RestartArrival.Pick(town, new Random(1)));
    }

    [Fact]
    public void WithoutRestartPointsAnEntryPointIsUsedThenTheFallback()
    {
        var onlyEntry = Town(new ZonePoint(0, ZoneGeometry.PointTypeEntry, 34, 40, 50));

        Assert.Equal((40, 50), RestartArrival.Pick(onlyEntry, new Random(1)));
        Assert.Equal((RestartArrival.FallbackX, RestartArrival.FallbackY), RestartArrival.Pick(Town(), new Random(1)));
        Assert.Equal((RestartArrival.FallbackX, RestartArrival.FallbackY), RestartArrival.Pick(null, new Random(1)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(33, 33)]
    [InlineData(999, 999)]
    [InlineData(1000, 1)]
    public void StoredReturnMapOutsideTheRangeBecomesMapOne(int stored, int expected)
    {
        Assert.Equal((ushort)expected, RestartArrival.NormalizeReturnMap((ushort)stored));
    }
}
