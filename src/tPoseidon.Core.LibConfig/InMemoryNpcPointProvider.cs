using tPoseidon.Core.Common;

namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryNpcPointProvider : INpcPointProvider
{
    private readonly Dictionary<(ushort MapId, ushort PosId), NpcPoint> _points;

    public InMemoryNpcPointProvider(IEnumerable<((ushort MapId, ushort PosId) Key, NpcPoint Point)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _points = new Dictionary<(ushort, ushort), NpcPoint>();
        foreach (var (key, point) in entries)
        {
            _points[key] = point;
        }
    }

    public bool TryGet(ushort mapId, ushort posId, out NpcPoint point)
        => _points.TryGetValue((mapId, posId), out point);

        public int Count => _points.Count;
}
