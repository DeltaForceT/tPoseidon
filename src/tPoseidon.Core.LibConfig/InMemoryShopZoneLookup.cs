namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryShopZoneLookup : IShopZoneLookup
{
    private readonly IReadOnlyList<ShopZoneEntry> _all;
    private readonly Dictionary<string, List<ShopZoneEntry>> _byMap;

    public InMemoryShopZoneLookup(IEnumerable<ShopZoneEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        _all = list;
        _byMap = new Dictionary<string, List<ShopZoneEntry>>(StringComparer.Ordinal);
        foreach (var e in list)
        {
            if (!_byMap.TryGetValue(e.MapName, out var bucket))
            {
                bucket = new List<ShopZoneEntry>();
                _byMap[e.MapName] = bucket;
            }
            bucket.Add(e);
        }
    }

    public IReadOnlyCollection<ShopZoneEntry> All => _all;
    public int Count => _all.Count;

    public IReadOnlyList<ShopZoneEntry> GetByMap(string mapName)
        => _byMap.TryGetValue(mapName, out var v) ? v : (IReadOnlyList<ShopZoneEntry>)Array.Empty<ShopZoneEntry>();
}
