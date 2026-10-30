using tPoseidon.Core.Game.MyCamp;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.CardBattle;

public static class CardBattleTitleZones
{
    public const byte NearRegion = 1;
    public const byte NearPoint = 2;

    public const int MarginLeft = 144;
    public const int MarginTop = 64;
    public const int MarginRight = 144;
    public const int MarginBottom = 144;

    private static readonly int[] PointTypes = [MapPointTypes.Npc, MapPointTypes.Shop, MapPointTypes.SkillShop];

    public static bool Accepts(ushort mapId) => !MyCampRules.IsShellZone(mapId);

    public static byte? RefusalCode(ZoneGeometry geometry, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var px = unchecked((short)x);
        var py = unchecked((short)y);
        foreach (var region in geometry.Regions)
        {
            if (region.Type != ZoneGeometry.RegionTypePortal) continue;
            if (Contains(unchecked((short)region.X1), unchecked((short)region.Y1), unchecked((short)region.X2), unchecked((short)region.Y2), px, py))
            {
                return NearRegion;
            }
        }
        foreach (var pointType in PointTypes)
        {
            foreach (var point in geometry.PointsOfType(pointType))
            {
                var pointX = unchecked((short)point.X);
                var pointY = unchecked((short)point.Y);
                if (Contains(pointX, pointY, unchecked((short)(pointX + 1)), unchecked((short)(pointY + 1)), px, py)) return NearPoint;
            }
        }
        return null;
    }

    private static bool Contains(short left, short top, short right, short bottom, short x, short y)
    {
        var minX = unchecked((short)(left - MarginLeft));
        var minY = unchecked((short)(top - MarginTop));
        var maxX = unchecked((short)(right + MarginRight));
        var maxY = unchecked((short)(bottom + MarginBottom));
        return x >= minX && y >= minY && x < maxX && y < maxY;
    }
}
