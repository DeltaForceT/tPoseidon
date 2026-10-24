using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Warp;

public sealed class TeleportArrivalTests
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
    public void TeleportArrivalPointsComeFirst()
    {
        var town = Town(
            new ZonePoint(0, ZoneGeometry.PointTypeRestart, 0, 300, 310),
            new ZonePoint(0, ZoneGeometry.PointTypeTeleportArrival, 0, 120, 130));

        Assert.Equal((120, 130), TeleportArrival.Pick(town, new Random(1)));
    }

    [Fact]
    public void WithoutThemTheRestartRuleApplies()
    {
        var town = Town(new ZonePoint(0, ZoneGeometry.PointTypeRestart, 0, 300, 310));

        Assert.Equal((300, 310), TeleportArrival.Pick(town, new Random(1)));
        Assert.Equal((RestartArrival.FallbackX, RestartArrival.FallbackY), TeleportArrival.Pick(null, new Random(1)));
    }
}
