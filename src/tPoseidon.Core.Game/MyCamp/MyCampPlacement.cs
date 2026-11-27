using tPoseidon.Core.Game.Quiz;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.MyCamp;

public static class MyCampPlacement
{
    public const int PortalLeft = 112;
    public const int PortalTop = 64;
    public const int PortalRight = 112;
    public const int PortalBottom = 160;

    public const int PointLeft = 128;
    public const int PointTop = 96;
    public const int PointRight = 128;
    public const int PointBottom = 160;

    private static readonly int[] PointTypes = [MapPointTypes.Npc, MapPointTypes.Shop, MapPointTypes.SkillShop];

    public static byte FloorAt(ZoneGeometry? geometry, int x, int y) => geometry is null ? (byte)0 : OxQuizRules.FloorAt(geometry, x, y);

    public static byte? RefusalCode(ZoneGeometry geometry, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var px = unchecked((short)x);
        var py = unchecked((short)y);
        foreach (var region in geometry.Regions)
        {
            if (region.Type != ZoneGeometry.RegionTypePortal) continue;
            if (Contains(unchecked((short)region.X1), unchecked((short)region.Y1), unchecked((short)region.X2), unchecked((short)region.Y2),
                PortalLeft, PortalTop, PortalRight, PortalBottom, px, py))
            {
                return MyCampRules.OpenNearPortal;
            }
        }
        foreach (var pointType in PointTypes)
        {
            foreach (var point in geometry.PointsOfType(pointType))
            {
                var pointX = unchecked((short)point.X);
                var pointY = unchecked((short)point.Y);
                if (Contains(pointX, pointY, unchecked((short)(pointX + 1)), unchecked((short)(pointY + 1)),
                    PointLeft, PointTop, PointRight, PointBottom, px, py))
                {
                    return MyCampRules.OpenNearPoint;
                }
            }
        }
        return null;
    }

    private static bool Contains(short left, short top, short right, short bottom, int marginLeft, int marginTop, int marginRight, int marginBottom, short x, short y)
    {
        var minX = unchecked((short)(left - marginLeft));
        var minY = unchecked((short)(top - marginTop));
        var maxX = unchecked((short)(right + marginRight));
        var maxY = unchecked((short)(bottom + marginBottom));
        return x >= minX && y >= minY && x < maxX && y < maxY;
    }
}
