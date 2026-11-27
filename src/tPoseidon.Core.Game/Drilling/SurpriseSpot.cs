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

    public SurpriseSpot MovedTo(short x, short y) => new(ItemId, Zone, x, y, EndsAt, IsTreasure) { CreviceFound = CreviceFound };
}

public static class SurpriseSpotRules
{
    public const int TeleportMapEffect = 44;
    public const int TreasureMapEffect = 43;
    public const byte Created = 1;
    public const byte Failed = 3;
    public const int SpotReachCells = 2;
    public const ushort LifeOverWord = 0;

    public static byte[] LifeOverPayload() => BitConverter.GetBytes(LifeOverWord);
    public const byte CreviceOpened = 1;
    public const byte CreviceClosedAsDeclined = 1;
    public const byte CreviceUsed = 0;
    public static readonly TimeSpan TeleportProtection = TimeSpan.FromMilliseconds(2000);
    public static readonly TimeSpan TeleportDelay = TimeSpan.FromMilliseconds(2000);

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

    public static ZonePoint? PickPoint(IReadOnlyList<ZonePoint> monsterPoints, Func<ZonePoint, bool> diggable, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(monsterPoints);
        ArgumentNullException.ThrowIfNull(diggable);
        ArgumentNullException.ThrowIfNull(next);
        var candidates = monsterPoints.Where(diggable).ToList();
        return candidates.Count == 0 ? null : candidates[next(candidates.Count)];
    }

    public static SurpriseSpot OnEnteringMap(SurpriseSpot spot, ushort mapId, Func<ZonePoint?> pickPoint)
    {
        ArgumentNullException.ThrowIfNull(spot);
        ArgumentNullException.ThrowIfNull(pickPoint);
        if (spot.Zone != mapId || pickPoint() is not { } point) return spot;
        return spot.MovedTo((short)point.X, (short)point.Y);
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
