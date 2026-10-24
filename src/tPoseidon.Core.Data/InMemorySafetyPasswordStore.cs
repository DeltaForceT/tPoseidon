using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemorySafetyPasswordStore : ISafetyPasswordStore
{
    private readonly ConcurrentDictionary<uint, SafetyPassword> _byAccount = new();
    private readonly List<SafetyPasswordLogEntry> _log = new();
    private readonly object _logGate = new();
    private long _nextLogSeq = 1;

    public SafetyPassword? Find(uint accountId) =>
        _byAccount.TryGetValue(accountId, out var record) ? record : null;

    public void Upsert(SafetyPassword record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _byAccount[record.AccountId] = record;
    }

    public bool SetLockFlag(uint accountId, byte lockFlag)
    {
        if (!_byAccount.TryGetValue(accountId, out var existing)) return false;
        var updated = existing with { LockFlag = lockFlag, UpdatedAt = DateTimeOffset.UtcNow };
        return _byAccount.TryUpdate(accountId, updated, existing);
    }

    public bool Delete(uint accountId) => _byAccount.TryRemove(accountId, out _);

    public void AppendLog(SafetyPasswordLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_logGate)
        {
            var seq = _nextLogSeq++;
            _log.Add(entry with
            {
                LogSeq = seq,
                CreatedAt = entry.CreatedAt == default ? DateTimeOffset.UtcNow : entry.CreatedAt,
            });
        }
    }

    public IReadOnlyList<SafetyPasswordLogEntry> GetLog(uint accountId, int limit = 50)
    {
        lock (_logGate)
        {
            return _log
                .Where(e => e.AccountId == accountId)
                .OrderByDescending(e => e.LogSeq)
                .Take(limit)
                .ToArray();
        }
    }
}
