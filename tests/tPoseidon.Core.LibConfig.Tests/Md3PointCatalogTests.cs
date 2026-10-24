namespace tPoseidon.Core.LibConfig.Tests;

public class Md3PointCatalogTests
{
    private const ushort CoralTownMapId = 148;
    private const ushort CoralShopMapId = 157;

    private static readonly string KrMapDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "variants", "Kr2014", "map"));

    private static bool KrMapsAvailable =>
        File.Exists(Path.Combine(KrMapDirectory, "map_sq11.md3")) && File.Exists(Path.Combine(KrMapDirectory, "map_sq11_01.md3"));

    private static MapInfoEntry Map(ushort id, string md3FileName) => new() { Id = id, Md3FileName = md3FileName };

    [SkippableFact]
    public void ShopPointsAreTheMapsTypeFivePoints()
    {
        Skip.IfNot(KrMapsAvailable, $"KR map files not in {KrMapDirectory}");

        var points = Md3PointCatalog.Load(
            new[] { Map(CoralTownMapId, "map_sq11.md3"), Map(CoralShopMapId, "map_sq11_01.md3") },
            KrMapDirectory,
            MapPointTypes.Shop);

        Assert.True(points.TryGet(CoralTownMapId, 1, out var itemGirl));
        Assert.Equal((1330f, 886f), (itemGirl.X, itemGirl.Y));
        Assert.True(points.TryGet(CoralShopMapId, 2, out var cardGirl));
        Assert.Equal((552f, 342f), (cardGirl.X, cardGirl.Y));
        Assert.Equal(2, points.Count);
    }

    [SkippableFact]
    public void NpcPointsAreNotShopPoints()
    {
        Skip.IfNot(KrMapsAvailable, $"KR map files not in {KrMapDirectory}");

        var shopPoints = Md3PointCatalog.Load(new[] { Map(CoralTownMapId, "map_sq11.md3") }, KrMapDirectory, MapPointTypes.Shop);
        var npcPoints = Md3PointCatalog.Load(new[] { Map(CoralTownMapId, "map_sq11.md3") }, KrMapDirectory, MapPointTypes.Npc);

        Assert.True(npcPoints.TryGet(CoralTownMapId, 1, out var npcPoint));
        Assert.True(shopPoints.TryGet(CoralTownMapId, 1, out var shopPoint));
        Assert.NotEqual(npcPoint, shopPoint);
    }

    [Fact]
    public void MapsWithoutAFileAreSkipped()
    {
        var points = Md3PointCatalog.Load(
            new[] { Map(1, " "), Map(2, "no_such_map.md3") },
            Path.GetTempPath(),
            MapPointTypes.Shop);

        Assert.Equal(0, points.Count);
    }

    [SkippableFact]
    public void SkillShopPointsAreTheMapsTypeTenPoints()
    {
        var beachTown = Path.Combine(KrMapDirectory, "maps01.md3");
        Skip.IfNot(File.Exists(beachTown), $"KR map file not at {beachTown}");

        var points = Md3PointCatalog.Load(new[] { Map(33, "maps01.md3") }, KrMapDirectory, MapPointTypes.SkillShop);

        Assert.True(points.TryGet(33, 11, out var louis));
        Assert.Equal((956f, 2449f), (louis.X, louis.Y));
        Assert.Equal(5, points.Count);
    }
}
