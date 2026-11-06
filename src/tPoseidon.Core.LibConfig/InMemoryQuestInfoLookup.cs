namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryQuestInfoLookup : IQuestInfoLookup
{
    private readonly Dictionary<int, QuestInfoEntry> _byId;

    public InMemoryQuestInfoLookup(IEnumerable<QuestInfoEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _byId = new Dictionary<int, QuestInfoEntry>();
        foreach (var entry in entries)
        {
            _byId[entry.Id] = entry;
        }
    }


    public QuestInfoEntry? GetById(int questId) =>
        _byId.TryGetValue(questId, out var entry) ? entry : null;

    public int Count => _byId.Count;
}
