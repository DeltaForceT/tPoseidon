namespace tPoseidon.Core.LibConfig;

public sealed class InMemorySkillShopZoneLookup : ISkillShopZoneLookup
{
    private readonly IReadOnlyList<SkillShopZoneEntry> _all;
    private readonly Dictionary<string, List<SkillShopZoneEntry>> _byMap;

    public InMemorySkillShopZoneLookup(IEnumerable<SkillShopZoneEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        _all = list;
        _byMap = new Dictionary<string, List<SkillShopZoneEntry>>(StringComparer.Ordinal);
        foreach (var e in list)
        {
            if (!_byMap.TryGetValue(e.MapName, out var bucket))
            {
                bucket = new List<SkillShopZoneEntry>();
                _byMap[e.MapName] = bucket;
            }
            bucket.Add(e);
        }
    }

    public IReadOnlyCollection<SkillShopZoneEntry> All => _all;
    public int Count => _all.Count;

    public IReadOnlyList<SkillShopZoneEntry> GetByMap(string mapName)
        => _byMap.TryGetValue(mapName, out var v) ? v : (IReadOnlyList<SkillShopZoneEntry>)Array.Empty<SkillShopZoneEntry>();
}
