using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Warp;

public static class RestartArrival
{
    public const ushort FallbackX = PortalArrivalRule.FallbackX;
    public const ushort FallbackY = PortalArrivalRule.FallbackY;
    public const byte ChangeZoneType = 1;

    public const ushort FirstReturnMap = 1;
    public const ushort LastReturnMap = 999;

    public static ushort NormalizeReturnMap(ushort stored) =>
        stored >= FirstReturnMap && stored <= LastReturnMap ? stored : FirstReturnMap;

    public static (ushort X, ushort Y) Pick(ZoneGeometry? geometry, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (geometry is not null)
        {
            var restartPoints = geometry.PointsOfType(ZoneGeometry.PointTypeRestart);
            if (restartPoints.Count > 0) return At(restartPoints[rng.Next(restartPoints.Count)]);
            var entryPoints = geometry.PointsOfType(ZoneGeometry.PointTypeEntry);
            if (entryPoints.Count > 0) return At(entryPoints[rng.Next(entryPoints.Count)]);
        }
        return (FallbackX, FallbackY);
    }

    private static (ushort X, ushort Y) At(ZonePoint point) =>
        ((ushort)Math.Clamp(point.X, 0, ushort.MaxValue), (ushort)Math.Clamp(point.Y, 0, ushort.MaxValue));
}
