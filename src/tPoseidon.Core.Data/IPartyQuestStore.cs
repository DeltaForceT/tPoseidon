namespace tPoseidon.Core.Data;

public sealed record PartyQuestRecord(
    ulong PartyUid,
    ulong CharacterId,
    ushort QuestId,
    ushort MonsterId,
    ushort KillCount,
    ushort TargetCount,
    int RecordTime,
    byte MemberCount,
    int ComCount);

public enum PartyQuestError
{
    None = 0,
    PartyQuestWrite = 60001,
    MemberOrHistoryWrite = 60002,
    NotFoundOrAlreadyRewarded = 60003,
    ActiveQuestRemove = 60004,
    HistoryLogWrite = 60005,
    RewardStamp = 60006,
}

public sealed record PartyQuestOutcome(PartyQuestError Error, int ComCount)
{
    public bool Succeeded => Error == PartyQuestError.None;
}

public static class PartyQuestRules
{
    public const byte HistoryQuestType = 7;
    public const int MostCompletions = 99;
    public const byte DeletedAuthority = 2;
    public const int GuildPointPerReward = 1;
    public const byte GuildPointHistoryType = 2;
}

public interface IPartyQuestStore
{
    ulong Create(ushort questId, ushort monsterId, ushort targetCount);

    IReadOnlyDictionary<ulong, int> CompletionCounts(ushort questId, IReadOnlyCollection<ulong> characterIds);

    PartyQuestOutcome Complete(ulong characterId, ulong partyUid, ushort questId, ushort killCount, int recordTime, byte memberCount);

    IReadOnlyList<PartyQuestRecord> ListUnrewarded(ulong characterId);

    PartyQuestOutcome Reward(ulong characterId, ushort questId, ulong partyUid);
}
