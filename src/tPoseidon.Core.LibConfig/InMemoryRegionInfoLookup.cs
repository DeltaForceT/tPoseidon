namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryRegionInfoLookup : IRegionInfoLookup
{
    private readonly IReadOnlyList<RegionInfoEntry> _all;
    private readonly Dictionary<(string Map, int Region), RegionInfoEntry> _byKey;
    private readonly Dictionary<string, List<RegionInfoEntry>> _byMap;

    public InMemoryRegionInfoLookup(IEnumerable<RegionInfoEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        _all = list;
        _byKey = new Dictionary<(string, int), RegionInfoEntry>();
        _byMap = new Dictionary<string, List<RegionInfoEntry>>(StringComparer.Ordinal);
        foreach (var e in list)
        {
            _byKey[(e.MapName, e.RegionId)] = e;
            if (!_byMap.TryGetValue(e.MapName, out var bucket))
            {
                bucket = new List<RegionInfoEntry>();
                _byMap[e.MapName] = bucket;
            }
            bucket.Add(e);
        }
    }

    public IReadOnlyCollection<RegionInfoEntry> All => _all;
    public int Count => _all.Count;

    public RegionInfoEntry? GetByMapAndRegion(string mapName, int regionId)
        => _byKey.TryGetValue((mapName, regionId), out var v) ? v : null;

    public IReadOnlyList<RegionInfoEntry> GetByMap(string mapName)
        => _byMap.TryGetValue(mapName, out var v) ? v : (IReadOnlyList<RegionInfoEntry>)Array.Empty<RegionInfoEntry>();
}
