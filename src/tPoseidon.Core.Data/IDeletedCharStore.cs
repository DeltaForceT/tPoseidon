using System.Net;

namespace tPoseidon.Core.Data;

public interface IDeletedCharStore
{
    void SoftDelete(ulong characterId, uint accountId, string characterName, IPAddress? userIp);

    DeletedChar? FindByCharId(ulong characterId);

    IReadOnlyList<DeletedChar> ListForAccount(uint accountId);

    bool Purge(ulong characterId);

    bool Restore(ulong characterId, string? newName, string? gmId, string? reason);

    IReadOnlyList<CharRestoreLogEntry> GetRestoreLog(ulong characterId, int limit = 50);
}
