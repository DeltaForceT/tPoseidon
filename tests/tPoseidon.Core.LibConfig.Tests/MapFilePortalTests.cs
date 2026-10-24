using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class MapFilePortalTests
{
    private static Md3Map Map(Md3RangeObject[] ranges, Md3PointObject[] points) => new()
    {
        MapSizeX = 4096,
        MapSizeY = 4096,
        Ranges = ranges,
        Points = points,
    };

    private static JsonPortalLookup Build(params (ushort Id, Md3Map Map)[] maps)
    {
        var entrances = new Dictionary<ushort, IReadOnlyList<PortalEntrance>>();
        var arrivals = new Dictionary<(ushort, ushort), (ushort, ushort)>();
        foreach (var (id, map) in maps) JsonPortalLookup.AddMap(id, map, entrances, arrivals);
        return new JsonPortalLookup(entrances, arrivals);
    }

    [Fact]
    public void RangesWithADestinationBecomePortalsAndArrivalsAreKeyedByTheSourceMap()
    {
        var lookup = Build(
            (3, Map([new Md3RangeObject(0, 1, 9, 1965, 132, 2151, 251, 0), new Md3RangeObject(1, 1, 152, 143, 229, 311, 353, 0)],
                    [new Md3PointObject(0, 1, 9, 1921, 326, 0), new Md3PointObject(0, 2, 0, 1440, 259, 0)])),
            (9, Map([new Md3RangeObject(0, 1, 3, 3473, 3788, 3617, 3904, 0)],
                    [new Md3PointObject(0, 1, 3, 3562, 3700, 0)])));

        Assert.Equal(2, lookup.GetEntrances(3).Count);
        Assert.Equal(((ushort)3562, (ushort)3700), lookup.FindArrival(3, 9));
        Assert.Equal(((ushort)1921, (ushort)326), lookup.FindArrival(9, 3));
        var destination = lookup.FindDestination(3, 2000, 200);
        Assert.NotNull(destination);
        Assert.Equal((9, 3562, 3700), (destination!.DestMapId, destination.SpawnX, destination.SpawnY));
    }

    [Fact]
    public void TypeZeroArrivalsCountWhenNoTypeOneArrivalExists()
    {
        var lookup = Build((150, Map([], [new Md3PointObject(0, 0, 149, 500, 600, 0), new Md3PointObject(0, 0, 0, 1, 1, 0)])));

        Assert.Equal(((ushort)500, (ushort)600), lookup.FindArrival(149, 150));
        Assert.Null(lookup.FindArrival(0, 150));
    }

    [Fact]
    public void TypeOneArrivalWinsOverTypeZero()
    {
        var lookup = Build((285, Map([], [new Md3PointObject(0, 0, 39, 10, 10, 0), new Md3PointObject(0, 1, 39, 20, 20, 0)])));

        Assert.Equal(((ushort)20, (ushort)20), lookup.FindArrival(39, 285));
    }

    [Fact]
    public void RangesWithoutADestinationAreNotPortals()
    {
        var lookup = Build((1, Map([new Md3RangeObject(0, 1, 0, 0, 0, 10, 10, 0)], [])));

        Assert.Empty(lookup.GetEntrances(1));
        Assert.Equal(0, lookup.MapCount);
    }
}
