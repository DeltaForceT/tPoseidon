using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.HarconDefence;

public static class HarconSpawnPlaces
{
    private static readonly HashSet<int> NonSpawnPointTypes =
    [
        ZoneGeometry.PointTypeEntry,
        ZoneGeometry.PointTypeRestart,
        ZoneGeometry.PointTypeTeleportArrival,
    ];

    public static IReadOnlyList<(int X, int Y)> PointsOf(ZoneGeometry? geometry, int regionId)
    {
        if (geometry is null) return Array.Empty<(int, int)>();
        return geometry.Points
            .Where(point => point.Id == regionId && !NonSpawnPointTypes.Contains(point.Type))
            .Select(point => (point.X, point.Y))
            .Distinct()
            .ToList();
    }
}
