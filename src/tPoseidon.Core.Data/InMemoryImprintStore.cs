using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemoryImprintStore : IImprintStore
{
    private readonly ConcurrentDictionary<(ulong CharacterId, int Type), ImprintRecord> _records = new();

    public IReadOnlyList<ImprintRecord> Load(ulong characterId) =>
        _records.Values.Where(record => record.CharacterId == characterId).OrderBy(record => record.Type).ToArray();

    public void Save(ImprintRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[(record.CharacterId, record.Type)] = record;
    }
}
