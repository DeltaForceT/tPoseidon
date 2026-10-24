using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Drilling;

public sealed class SurpriseSpot
{
    public SurpriseSpot(int itemId, ushort zone, short x, short y, DateTimeOffset endsAt, bool treasure = false)
    {
        IsTreasure = treasure;
        ItemId = itemId;
        Zone = zone;
        X = x;
        Y = y;
        EndsAt = endsAt;
    }

    public int ItemId { get; }
    public ushort Zone { get; }
    public short X { get; }
    public short Y { get; }
    public DateTimeOffset EndsAt { get; }
    public bool CreviceFound { get; set; }
    public bool IsTreasure { get; }
}

public static class SurpriseSpotRules
{
    public const int TeleportMapEffect = 44;
    public const int TreasureMapEffect = 43;
    public const byte Created = 1;
    public const byte Failed = 3;
    public const int SpotReachCells = 2;
    public const byte CreviceOpened = 0;
    public const byte CreviceClosedAsDeclined = 1;
    public const byte CreviceUsed = 0;
    public static readonly TimeSpan TeleportProtection = TimeSpan.FromMilliseconds(2000);

    public static bool IsOnSpot(SurpriseSpot spot, ushort mapId, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(spot);
        return spot.Zone == mapId
            && Math.Abs(ZoneGeometry.CellOf(x) - ZoneGeometry.CellOf(spot.X)) <= SpotReachCells
            && Math.Abs(ZoneGeometry.CellOf(y) - ZoneGeometry.CellOf(spot.Y)) <= SpotReachCells;
    }

    public static bool HasExpired(SurpriseSpot spot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(spot);
        return now >= spot.EndsAt;
    }

    public static IReadOnlyList<ZonePoint> MonsterPoints(ZoneGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return geometry.PointsOfType(ZoneGeometry.PointTypeSpawn).Concat(geometry.PointsOfType(ZoneGeometry.PointTypeRegionSpawn)).ToList();
    }

    public static TreasureTeleportDestination? PickDestination(TreasureTeleportEntry row, Func<double> roll)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(roll);
        var total = row.Destinations.Sum(destination => (double)destination.Ratio);
        if (total <= 0) return null;
        var pick = roll() * total;
        var cumulative = 0.0;
        foreach (var destination in row.Destinations)
        {
            cumulative += destination.Ratio;
            if (pick < cumulative) return destination;
        }
        return row.Destinations[^1];
    }
}
