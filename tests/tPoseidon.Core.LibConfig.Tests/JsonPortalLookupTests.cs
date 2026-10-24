namespace tPoseidon.Core.LibConfig.Tests;

public class JsonPortalLookupTests
{
    private static readonly string PortalsJsonPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "portals.json"));

    private static bool FileAvailable => File.Exists(PortalsJsonPath);

    [SkippableFact]
    public void LoadFromPortalsJson_LoadsPortalsFile()
    {
        Skip.IfNot(FileAvailable, $"portals.json not at {PortalsJsonPath}");

        var lookup = JsonPortalLookup.LoadFromPortalsJson(PortalsJsonPath);

        Assert.True(lookup.MapCount >= 100, $"expected ≥100 maps; got {lookup.MapCount}");
        Assert.True(lookup.TotalPortals >= 100, $"expected ≥100 portals; got {lookup.TotalPortals}");
    }

    [SkippableFact]
    public void FindDestination_CoralTownLeftPortal_ResolvesMap153()
    {
        Skip.IfNot(FileAvailable, $"portals.json not at {PortalsJsonPath}");

        var lookup = JsonPortalLookup.LoadFromPortalsJson(PortalsJsonPath);
        var dest = lookup.FindDestination(sourceMapId: 148, posX: 250, posY: 780);

        Assert.NotNull(dest);
        Assert.Equal((ushort)153, dest!.DestMapId);
        Assert.True(dest.SpawnX > 0, "expected non-zero arrival X");
        Assert.True(dest.SpawnY > 0, "expected non-zero arrival Y");
    }

    [Fact]
    public void FindDestination_NoMatchingMap_ReturnsNull()
    {
        var lookup = new JsonPortalLookup(
            new Dictionary<ushort, IReadOnlyList<PortalEntrance>>(),
            new Dictionary<(ushort, ushort), (ushort, ushort)>());
        Assert.Null(lookup.FindDestination(148, 100, 100));
    }
}
