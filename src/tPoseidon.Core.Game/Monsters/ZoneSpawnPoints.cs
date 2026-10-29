using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public sealed class ZoneSpawnPoints
{
    public const ushort FallbackX = 600;
    public const ushort FallbackY = 400;

    private const int NoRegion = 0;

    private readonly Dictionary<int, List<ZonePoint>> _regionPoints = new();
    private readonly List<ZonePoint> _generalPoints = new();
    private readonly ZonePoint? _firstEntry;

    public ZoneSpawnPoints(ZoneGeometry? geometry)
    {
        if (geometry is null) return;
        foreach (var point in geometry.Points)
        {
            switch (point.Type)
            {
                case ZoneGeometry.PointTypeRegionSpawn:
                    if (!_regionPoints.TryGetValue(point.Id, out var ofRegion))
                    {
                        ofRegion = new List<ZonePoint>();
                        _regionPoints[point.Id] = ofRegion;
                    }
                    ofRegion.Add(point);
                    break;
                case ZoneGeometry.PointTypeSpawn:
                    _generalPoints.Add(point);
                    break;
                case ZoneGeometry.PointTypeEntry:
                    _firstEntry ??= point;
                    break;
            }
        }
    }

    public (ushort X, ushort Y) Pick(int region, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (region != NoRegion && _regionPoints.TryGetValue(region, out var ofRegion) && ofRegion.Count > 0)
        {
            return At(ofRegion[rng.Next(ofRegion.Count)]);
        }
        if (_generalPoints.Count > 0)
        {
            return At(_generalPoints[rng.Next(_generalPoints.Count)]);
        }
        return _firstEntry is { } entry ? At(entry) : (FallbackX, FallbackY);
    }

    private static (ushort X, ushort Y) At(ZonePoint point) =>
        ((ushort)Math.Clamp(point.X, 0, ushort.MaxValue), (ushort)Math.Clamp(point.Y, 0, ushort.MaxValue));
}
