using System.Net;

namespace tPoseidon.Core.Data;

public sealed class InMemoryDeletedCharStore : IDeletedCharStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, DeletedChar> _byChar = new();
    private readonly List<CharRestoreLogEntry> _restoreLog = new();
    private long _nextSeq = 1;

    public void SoftDelete(ulong characterId, uint accountId, string characterName, IPAddress? userIp)
    {
        ArgumentNullException.ThrowIfNull(characterName);
        lock (_gate)
        {
            _byChar[characterId] = new DeletedChar
            {
                CharacterId = characterId,
                AccountId = accountId,
                CharacterName = characterName,
                UserIp = userIp,
                DeletedAt = DateTimeOffset.UtcNow,
            };
        }
    }

    public DeletedChar? FindByCharId(ulong characterId)
    {
        lock (_gate)
        {
            return _byChar.TryGetValue(characterId, out var rec) ? rec : null;
        }
    }

    public IReadOnlyList<DeletedChar> ListForAccount(uint accountId)
    {
        lock (_gate)
        {
            return _byChar.Values.Where(d => d.AccountId == accountId)
                .OrderByDescending(d => d.DeletedAt)
                .ToArray();
        }
    }

    public bool Purge(ulong characterId)
    {
        lock (_gate)
        {
            return _byChar.Remove(characterId);
        }
    }

    public bool Restore(ulong characterId, string? newName, string? gmId, string? reason)
    {
        lock (_gate)
        {
            if (!_byChar.TryGetValue(characterId, out var rec)) return false;
            _byChar.Remove(characterId);
            _restoreLog.Add(new CharRestoreLogEntry
            {
                Seq = _nextSeq++,
                AccountId = rec.AccountId,
                CharacterId = characterId,
                OldName = rec.CharacterName,
                NewName = newName ?? rec.CharacterName,
                DeletedAt = rec.DeletedAt,
                RestoredAt = DateTimeOffset.UtcNow,
                GmId = gmId,
                Reason = reason,
            });
            return true;
        }
    }

    public IReadOnlyList<CharRestoreLogEntry> GetRestoreLog(ulong characterId, int limit = 50)
    {
        lock (_gate)
        {
            return _restoreLog.Where(e => e.CharacterId == characterId)
                .OrderByDescending(e => e.Seq)
                .Take(limit)
                .ToArray();
        }
    }
}
