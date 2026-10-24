namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryTutorialSystemLookup : ITutorialSystemLookup
{
    private readonly Dictionary<int, TutorialSystemEntry> _byId;
    private readonly Dictionary<(int NpcType, int MapId), TutorialSystemEntry> _byStartNpcMap;
    private readonly Dictionary<int, List<TutorialSystemEntry>> _byQuestId;

    public InMemoryTutorialSystemLookup(IEnumerable<TutorialSystemEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byId = new Dictionary<int, TutorialSystemEntry>();
        _byStartNpcMap = new Dictionary<(int, int), TutorialSystemEntry>();
        _byQuestId = new Dictionary<int, List<TutorialSystemEntry>>();

        foreach (var entry in entries)
        {
            _byId[entry.Id] = entry;

            var key = (entry.StartNpcID, entry.StartMapID);
            if (!_byStartNpcMap.ContainsKey(key))
            {
                _byStartNpcMap[key] = entry;
            }

            if (entry.QuestId != 0)
            {
                if (!_byQuestId.TryGetValue(entry.QuestId, out var list))
                {
                    list = new List<TutorialSystemEntry>();
                    _byQuestId[entry.QuestId] = list;
                }
                list.Add(entry);
            }
        }
    }

        public static InMemoryTutorialSystemLookup LoadFromXml(string xmlPath)
    {
        var reader = new LibConfigXmlReader(xmlPath);
        var entries = reader.LoadTutorialSystem();
        return new InMemoryTutorialSystemLookup(entries);
    }

    public TutorialSystemEntry? GetById(int tutorialId) =>
        _byId.TryGetValue(tutorialId, out var entry) ? entry : null;

    public TutorialSystemEntry? FindByStartNpcOnMap(int npcType, int mapId) =>
        _byStartNpcMap.TryGetValue((npcType, mapId), out var entry) ? entry : null;

    public IReadOnlyList<TutorialSystemEntry> FindByQuestId(int questId) =>
        _byQuestId.TryGetValue(questId, out var list)
            ? list
            : Array.Empty<TutorialSystemEntry>();

    public int Count => _byId.Count;
}
