using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Warp;

public static class TeleportArrival
{
    public const byte ChangeZoneType = 3;

    public static (ushort X, ushort Y) Pick(ZoneGeometry? geometry, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var points = geometry?.PointsOfType(ZoneGeometry.PointTypeTeleportArrival);
        if (points is { Count: > 0 })
        {
            var point = points[rng.Next(points.Count)];
            return ((ushort)Math.Clamp(point.X, 0, ushort.MaxValue), (ushort)Math.Clamp(point.Y, 0, ushort.MaxValue));
        }
        return RestartArrival.Pick(geometry, rng);
    }
}
