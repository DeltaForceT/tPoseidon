namespace tPoseidon.Core.Data;

public interface IGuildStore
{
    Guild CreateGuild(string guildName, uint masterAccountId, ulong masterCharacterId, string guildIntro = "");

    Guild? FindGuild(ulong guildCode);

    Guild? FindGuildByName(string guildName);

    IReadOnlyList<Guild> ListGuilds(int offset = 0, int limit = 50);

    Guild UpdateGuildHeader(Guild guild);

    bool DisbandGuild(ulong guildCode);

    GuildMember AddMember(ulong guildCode, ulong characterId, string memberNick, byte gradeCode = 0);

    bool RemoveMember(ulong guildCode, ulong characterId);

    GuildMember? FindMember(ulong guildCode, ulong characterId);

    IReadOnlyList<GuildMember> GetMembers(ulong guildCode);

    GuildMember? FindMembershipForCharacter(ulong characterId);

    bool UpdateMemberGrade(ulong guildCode, ulong characterId, byte gradeCode);

    int AddMemberPoints(ulong guildCode, ulong characterId, int delta);

    GuildMemberStandby AddStandby(ulong guildCode, ulong characterId, byte standbyState = 0);

    IReadOnlyList<GuildMemberStandby> GetStandbyForGuild(ulong guildCode);

    bool RemoveStandby(ulong guildCode, ulong characterId);

    GuildGrade UpsertGrade(GuildGrade grade);

    IReadOnlyList<GuildGrade> GetGrades(ulong guildCode);

    bool DeleteGrade(ulong guildCode, byte gradeCode);

    GuildRecruit UpsertRecruit(GuildRecruit recruit);

    GuildRecruit? GetRecruit(ulong guildCode);

    bool DeleteRecruit(ulong guildCode);

    GuildNotice AddNotice(ulong guildCode, ulong authorCharId, string content);

    IReadOnlyList<GuildNotice> GetNotices(ulong guildCode, int limit = 20);

    bool DeleteNotice(long noticeCode);

    GuildLogEntry AppendLog(ulong guildCode, byte logType, string message);

    IReadOnlyList<GuildLogEntry> GetLog(ulong guildCode, int limit = 50);

    GuildPenalty AddPenalty(ulong characterId, string guildName, byte penaltyType, TimeSpan duration);

    IReadOnlyList<GuildPenalty> GetPenaltiesForCharacter(ulong characterId);

    GuildPointHistoryEntry AppendPointHistory(ulong guildCode, ulong? characterId, int delta, byte historyType);

    IReadOnlyList<GuildPointHistoryEntry> GetPointHistory(ulong guildCode, int limit = 50);

    GuildLadder UpsertLadder(GuildLadder ladder);

    GuildLadder? GetLadder(ulong guildCode);

    GuildLadderLogEntry AppendLadderLog(ulong guildCode, string guildName, int newLadder, int increase);

    IReadOnlyList<GuildLadderLogEntry> GetLadderLog(ulong guildCode, int limit = 50);

    GuildLadderLogEntry AppendLadderIncreaseUpdate(ulong guildCode, string guildName, int newLadder, int increase);

    GuildMatchEntry RecordMatch(GuildMatchEntry match);

    IReadOnlyList<GuildMatchEntry> GetMatches(ulong guildCode, int limit = 50);

    GuildRankingEntry SnapshotRanking(GuildRankingEntry entry);

    IReadOnlyList<GuildRankingEntry> GetRanking(string weekDay, int limit = 100);

    GuildStateEntry AppendState(ulong guildCode, byte stateType, string note);

    IReadOnlyList<GuildStateEntry> GetStateLog(ulong guildCode, int limit = 50);
}
