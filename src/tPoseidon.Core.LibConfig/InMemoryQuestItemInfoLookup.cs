namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryQuestItemInfoLookup : IQuestItemInfoLookup
{
    private readonly Dictionary<int, List<QuestItemInfoEntry>> _byQuestId;
    private readonly Dictionary<int, QuestItemInfoEntry> _byItemId;

    public InMemoryQuestItemInfoLookup(IEnumerable<QuestItemInfoEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byQuestId = new Dictionary<int, List<QuestItemInfoEntry>>();
        _byItemId = new Dictionary<int, QuestItemInfoEntry>();
        foreach (var entry in entries)
        {
            if (!_byQuestId.TryGetValue(entry.QuestId, out var list))
            {
                list = new List<QuestItemInfoEntry>();
                _byQuestId[entry.QuestId] = list;
            }
            list.Add(entry);
            _byItemId[entry.ItemId] = entry;
        }
    }

    public IReadOnlyList<QuestItemInfoEntry> GetByQuestId(int questId) =>
        _byQuestId.TryGetValue(questId, out var list)
            ? list
            : Array.Empty<QuestItemInfoEntry>();

    public QuestItemInfoEntry? GetByItemId(int itemId) =>
        _byItemId.TryGetValue(itemId, out var entry) ? entry : null;

    public int Count => _byItemId.Count;
}
