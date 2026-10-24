namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryMonZoneLookup : IMonZoneLookup
{
    private readonly IReadOnlyList<MonZoneEntry> _all;
    private readonly Dictionary<string, List<MonZoneEntry>> _byMap;

    public InMemoryMonZoneLookup(IEnumerable<MonZoneEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        _all = list;
        _byMap = new Dictionary<string, List<MonZoneEntry>>(StringComparer.Ordinal);
        foreach (var e in list)
        {
            if (!_byMap.TryGetValue(e.MapName, out var bucket))
            {
                bucket = new List<MonZoneEntry>();
                _byMap[e.MapName] = bucket;
            }
            bucket.Add(e);
        }
    }

    public IReadOnlyCollection<MonZoneEntry> All => _all;
    public int Count => _all.Count;

    public IReadOnlyList<MonZoneEntry> GetByMap(string mapName)
        => _byMap.TryGetValue(mapName, out var v) ? v : (IReadOnlyList<MonZoneEntry>)Array.Empty<MonZoneEntry>();
}
