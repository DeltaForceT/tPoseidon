namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryMapItemZoneLookup : IMapItemZoneLookup
{
    private readonly IReadOnlyList<MapItemZoneEntry> _all;
    private readonly Dictionary<string, List<MapItemZoneEntry>> _byMap;

    public InMemoryMapItemZoneLookup(IEnumerable<MapItemZoneEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        _all = list;
        _byMap = new Dictionary<string, List<MapItemZoneEntry>>(StringComparer.Ordinal);
        foreach (var e in list)
        {
            if (!_byMap.TryGetValue(e.MapName, out var bucket))
            {
                bucket = new List<MapItemZoneEntry>();
                _byMap[e.MapName] = bucket;
            }
            bucket.Add(e);
        }
    }

    public IReadOnlyCollection<MapItemZoneEntry> All => _all;
    public int Count => _all.Count;

    public IReadOnlyList<MapItemZoneEntry> GetByMap(string mapName)
        => _byMap.TryGetValue(mapName, out var v) ? v : (IReadOnlyList<MapItemZoneEntry>)Array.Empty<MapItemZoneEntry>();
}
