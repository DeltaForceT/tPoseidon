using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemoryMonsterHuntStore : IMonsterHuntStore
{
    private readonly ConcurrentDictionary<ulong, MonsterHuntRecord> _hunts = new();

    public MonsterHuntRecord? Find(ulong characterId) =>
        _hunts.TryGetValue(characterId, out var record) ? record : null;

    public void Save(MonsterHuntRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _hunts[record.CharacterId] = record;
    }

    public bool Delete(ulong characterId) => _hunts.TryRemove(characterId, out _);
}
