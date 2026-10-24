using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemoryQuestHistoryStore : IQuestHistoryStore
{
    private readonly record struct HistoryKey(ulong CharacterId, byte QuestType, int QuestId, int MonsterId);

    private readonly ConcurrentDictionary<HistoryKey, QuestHistory> _history = new();
    private readonly object _logSync = new();
    private readonly List<QuestHistory> _log = new();

    public QuestHistory? Find(ulong characterId, byte questType, int questId, int monsterId)
    {
        var key = new HistoryKey(characterId, questType, questId, monsterId);
        return _history.TryGetValue(key, out var row) ? row : null;
    }

    public void Insert(QuestHistory row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var key = new HistoryKey(row.CharacterId, row.QuestType, row.QuestId, row.MonsterId);
        if (!_history.TryAdd(key, row))
        {
            throw new InvalidOperationException(
                $"Quest history row already exists: char={row.CharacterId}, " +
                $"type={row.QuestType}, id={row.QuestId}, monster={row.MonsterId}");
        }
    }

    public bool Update(QuestHistory row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var key = new HistoryKey(row.CharacterId, row.QuestType, row.QuestId, row.MonsterId);
        if (!_history.TryGetValue(key, out var existing)) return false;
        return _history.TryUpdate(key, row, existing);
    }

    public IReadOnlyList<QuestHistory> GetForCharacter(ulong characterId)
    {
        return _history.Values
            .Where(q => q.CharacterId == characterId)
            .OrderByDescending(q => q.CompletedAt)
            .ToList();
    }

    public void AppendLog(QuestHistory row)
    {
        ArgumentNullException.ThrowIfNull(row);
        lock (_logSync) _log.Add(row);
    }

        public IReadOnlyList<QuestHistory> SnapshotLog()
    {
        lock (_logSync) return _log.ToArray();
    }

    public bool CompletedToday(ulong characterId, byte questType, int questId, int monsterId, DateTime referenceUtc)
    {
        var row = Find(characterId, questType, questId, monsterId);
        if (row is null) return false;
        return row.CompletedAt.UtcDateTime.Date == referenceUtc.Date;
    }

    public bool HasCompletedAtLeast(ulong characterId, byte questType, int questId, int monsterId, byte threshold)
    {
        if (threshold == 0) return true;
        var row = Find(characterId, questType, questId, monsterId);
        if (row is null) return false;
        return row.CompletionCount >= threshold;
    }
}
