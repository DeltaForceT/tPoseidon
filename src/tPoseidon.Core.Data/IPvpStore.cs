namespace tPoseidon.Core.Data;

public interface IPvpStore
{
    PvpMatch RecordMatch(PvpMatch match);

    IReadOnlyList<PvpMatch> GetMatchesForCharacter(ulong characterId, int limit = 50);

    PvpLadder GetOrCreateLadder(ulong characterId);

    PvpLadder UpsertLadder(PvpLadder ladder);

    bool DeleteLadder(ulong characterId);
}

public interface IFriendStore
{
    FriendEntry? FindCharacter(string name);

    IReadOnlyList<FriendGroup> GetGroups(ulong ownerCharacterId);

    void CreateDefaultGroups(ulong ownerCharacterId);

    bool RenameGroup(ulong ownerCharacterId, byte groupNum, string groupName);

    IReadOnlyList<FriendEntry> GetFriends(ulong ownerCharacterId);

    FriendAddResult AddFriend(ulong ownerCharacterId, ulong friendCharacterId, byte groupNum);

    bool SetFriendGroup(ulong ownerCharacterId, ulong friendCharacterId, byte groupNum);

    FriendRemoveResult RemoveFriend(ulong ownerCharacterId, ulong friendCharacterId, bool friendOnline);

    IReadOnlyList<string> TakeDeletedNotifications(ulong ownerCharacterId);

    int CountMessages(ulong ownerCharacterId);

    bool SendMessage(ulong ownerCharacterId, ulong senderCharacterId, byte kind, string content);

    IReadOnlyList<FriendMessage> TakeMessages(ulong ownerCharacterId);

    void RemoveAllFor(ulong characterId);
}

public interface IAccountBlockStore
{
    AccountBlock Add(AccountBlock block);

    IReadOnlyList<AccountBlock> GetActiveBlocks(uint targetAccountId, DateTimeOffset now);

    IReadOnlyList<AccountBlock> GetAllBlocks(uint targetAccountId, int limit = 50);

    bool RemoveBlock(long blockSeq);
}

public interface IAccountLogoutStore
{
    AccountLogoutEntry Append(uint accountId, DateTimeOffset loginAt, DateTimeOffset logoutAt, byte logoutWorld, int? playTime);

    IReadOnlyList<AccountLogoutEntry> GetForAccount(uint accountId, int limit = 50);
}

public interface IFortuneStore
{
    FortuneInfo GetOrCreate(ulong characterId);

    FortuneInfo? Find(ulong characterId);

    FortuneInfo Upsert(FortuneInfo info);

    void Update(FortuneInfo info);

    void AppendLog(FortuneInfo info);

    IReadOnlyList<FortuneInfoLogEntry> GetLog(ulong characterId, int limit = 50);

    bool Delete(ulong characterId);
}

public interface IBroadcastStore
{
    BroadcastMessage AddMessage(BroadcastMessage message);

    IReadOnlyList<BroadcastMessage> GetActiveForIsland(byte islandCode, int limit = 50);

    bool DeleteMessage(long broadcastCode);

    void AppendLog(BroadcastMessage message);

    IReadOnlyList<BroadcastLogEntry> GetLog(ulong characterId, int limit = 50);
}
