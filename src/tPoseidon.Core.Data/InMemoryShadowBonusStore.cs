using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemoryShadowBonusStore : IShadowBonusStore
{
    private readonly ConcurrentDictionary<ulong, ShadowBonusRecord> _records = new();

    public ShadowBonusRecord? Load(ulong characterId) => _records.GetValueOrDefault(characterId);

    public void Save(ShadowBonusRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.CharacterId] = record;
    }
}
