using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Guild;

public static class GuildArenaSpawn
{
    public static int RegionIdOf(int team, int regionCount, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        var pick = next(regionCount);
        return team == 0 ? pick | 1 : (pick & ~1) + 2;
    }

    public static (ushort X, ushort Y)? Pick(ZoneGeometry? geometry, int team, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (geometry is null) return null;
        var count = geometry.CountRegionsOfType(ZoneGeometry.RegionTypeGuildSpawn);
        if (count == 0) return null;
        if (geometry.FindRegion(ZoneGeometry.RegionTypeGuildSpawn, RegionIdOf(team, count, next)) is not { } region) return null;
        var left = Math.Min(region.X1, region.X2);
        var top = Math.Min(region.Y1, region.Y2);
        var width = Math.Abs(region.X2 - region.X1);
        var height = Math.Abs(region.Y2 - region.Y1);
        var x = left + (width > 0 ? next(width) : 0);
        var y = top + (height > 0 ? next(height) : 0);
        return ((ushort)Math.Clamp(x, 0, ushort.MaxValue), (ushort)Math.Clamp(y, 0, ushort.MaxValue));
    }
}
