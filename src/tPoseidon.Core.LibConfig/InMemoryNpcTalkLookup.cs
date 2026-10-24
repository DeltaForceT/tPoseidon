namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryNpcTalkLookup : INpcTalkLookup
{
    private readonly Dictionary<(int Type, string Map), NpcTalkEntry> _byTypeAndMap;
    private readonly Dictionary<int, NpcTalkEntry> _firstByType;
    private readonly Dictionary<string, List<NpcTalkEntry>> _byMap;

    public InMemoryNpcTalkLookup(IEnumerable<NpcTalkEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byTypeAndMap = new Dictionary<(int, string), NpcTalkEntry>();
        _firstByType = new Dictionary<int, NpcTalkEntry>();
        _byMap = new Dictionary<string, List<NpcTalkEntry>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            _byTypeAndMap[(entry.Type, entry.MapName)] = entry;




            if (!_firstByType.ContainsKey(entry.Type))
            {
                _firstByType[entry.Type] = entry;
            }

            if (!_byMap.TryGetValue(entry.MapName, out var list))
            {
                list = new List<NpcTalkEntry>();
                _byMap[entry.MapName] = list;
            }
            list.Add(entry);
        }


        foreach (var list in _byMap.Values)
        {
            list.Sort((a, b) => a.PosId.CompareTo(b.PosId));
        }
    }

        public static InMemoryNpcTalkLookup LoadFromXml(string xmlPath)
    {
        var reader = new LibConfigXmlReader(xmlPath);
        var entries = reader.LoadAllNpcTalkTables();
        return new InMemoryNpcTalkLookup(entries);
    }

    public NpcTalkEntry? GetByTypeOnMap(int npcType, string mapName)
    {
        return _byTypeAndMap.TryGetValue((npcType, mapName), out var entry) ? entry : null;
    }

    public NpcTalkEntry? GetByTypeAnyMap(int npcType)
    {
        return _firstByType.TryGetValue(npcType, out var entry) ? entry : null;
    }

    public IReadOnlyList<NpcTalkEntry> GetAllByMap(string mapName)
    {
        return _byMap.TryGetValue(mapName, out var list)
            ? list
            : Array.Empty<NpcTalkEntry>();
    }

    public int Count => _byTypeAndMap.Count;

}
