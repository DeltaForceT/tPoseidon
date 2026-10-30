using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting.ClientScan;

public sealed record Md3PointPatch(string MapFile, Md3PointObject Point, string Source);

public sealed class ClientPatchCatalog
{
    public ClientPatchCatalog(IEnumerable<Md3PointPatch> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = points.ToList();
    }

    public static ClientPatchCatalog Default { get; } = new([]);

    public IReadOnlyList<Md3PointPatch> Points { get; }

    public Md3PointPatch? FindPoint(string mapFile, int pointType, int pointId) =>
        Points.FirstOrDefault(patch =>
            string.Equals(patch.MapFile, mapFile, StringComparison.OrdinalIgnoreCase)
            && patch.Point.Type == pointType
            && patch.Point.Id == pointId);

    public Md3PointPatch? FindArrival(string mapFile, int fromMapId) =>
        Points.FirstOrDefault(patch =>
            string.Equals(patch.MapFile, mapFile, StringComparison.OrdinalIgnoreCase)
            && patch.Point.Type == JsonPortalLookup.ArrivalPointType
            && patch.Point.MapId == fromMapId);
}
