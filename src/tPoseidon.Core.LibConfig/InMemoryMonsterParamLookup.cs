namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryMonsterParamLookup : IMonsterParamLookup
{
    private readonly Dictionary<int, MonsterParamEntry> _byId;

    public InMemoryMonsterParamLookup(IEnumerable<MonsterParamEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byId = new Dictionary<int, MonsterParamEntry>();
        foreach (var entry in entries)
        {
            _byId[entry.Id] = entry;
        }
    }

    public MonsterParamEntry? GetById(int monsterId) =>
        _byId.TryGetValue(monsterId, out var entry) ? entry : null;

    public IReadOnlyCollection<MonsterParamEntry> All => _byId.Values;

    public int Count => _byId.Count;
}
