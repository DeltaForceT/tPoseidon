namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryMonsterQuestInfoLookup : IMonsterQuestInfoLookup
{
    private readonly Dictionary<int, MonsterQuestInfoEntry> _byId;

    public InMemoryMonsterQuestInfoLookup(IEnumerable<MonsterQuestInfoEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byId = new Dictionary<int, MonsterQuestInfoEntry>();
        foreach (var entry in entries)
        {
            _byId[entry.Id] = entry;
        }
    }

    public MonsterQuestInfoEntry? GetById(int questId) =>
        _byId.TryGetValue(questId, out var entry) ? entry : null;

    public int Count => _byId.Count;

    public IEnumerable<MonsterQuestInfoEntry> All() => _byId.Values;
}
