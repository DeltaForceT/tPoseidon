namespace tPoseidon.Core.Data;

public sealed record Guild
{
    public required ulong GuildCode { get; init; }
    public required string GuildName { get; init; }
    public string GuildPassword { get; init; } = "";
    public required uint MasterAccountId { get; init; }
    public required ulong MasterCharacterId { get; init; }
    public DateTimeOffset GuildCreatedAt { get; init; }
    public long GuildGelder { get; init; }
    public int GuildPoint { get; init; }
    public short GuildMembers { get; init; } = 1;
    public string GuildIntro { get; init; } = "";
    public string GuildNotice { get; init; } = "";
    public int EmblemCode { get; init; }
    public byte GuildJoinPoint { get; init; } = 1;
    public byte GuildSmsPoint { get; init; } = 1;
    public string GuildUrl { get; init; } = "";
    public string GuildPw { get; init; } = "";
    public int GuildPointIncrease { get; init; }
    public int GuildRanking { get; init; }
    public int GuildRankingPoint { get; init; }
}

public sealed record GuildMember
{
    public required ulong GuildCode { get; init; }
    public required ulong CharacterId { get; init; }
    public string MemberNick { get; init; } = "";
    public DateTimeOffset MemberCreatedAt { get; init; }
    public int MemberPoint { get; init; }
    public byte GradeCode { get; init; }
    public short MemberRight { get; init; }
}

public sealed record GuildMemberStandby
{
    public required ulong GuildCode { get; init; }
    public required ulong CharacterId { get; init; }
    public byte StandbyState { get; init; }
    public DateTimeOffset StandbyAt { get; init; }
}

public sealed record GuildGrade
{
    public required ulong GuildCode { get; init; }
    public required byte GradeCode { get; init; }
    public int GradePoint { get; init; }
    public string GradeName { get; init; } = "";
}

public sealed record GuildRecruit
{
    public required ulong GuildCode { get; init; }
    public required ulong CharacterId { get; init; }
    public string RecruitTitle { get; init; } = "";
    public string RecruitContent { get; init; } = "";
    public DateTimeOffset RecruitUpdatedAt { get; init; }
}

public sealed record GuildNotice
{
    public long NoticeCode { get; init; }
    public required ulong GuildCode { get; init; }
    public required ulong CharacterId { get; init; }
    public string NoticeContent { get; init; } = "";
    public DateTimeOffset NoticeCreatedAt { get; init; }
}

public sealed record GuildLogEntry
{
    public long LogSeq { get; init; }
    public DateTimeOffset LogAt { get; init; }
    public required byte LogType { get; init; }
    public required ulong GuildCode { get; init; }
    public string LogMsg { get; init; } = "";
}

public sealed record GuildPenalty
{
    public required ulong CharacterId { get; init; }
    public string GuildName { get; init; } = "";
    public required byte PenaltyType { get; init; }
    public DateTimeOffset SecedeAt { get; init; }
    public required DateTimeOffset ReleaseAt { get; init; }
}

public sealed record GuildPointHistoryEntry
{
    public long Seq { get; init; }
    public ulong? CharacterId { get; init; }
    public int AddGuildPoint { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public byte HistoryType { get; init; }
    public required ulong GuildCode { get; init; }
}

public sealed record GuildLadder
{
    public required ulong GuildCode { get; init; }
    public int LadderScore { get; init; } = 1000;
    public short MatchCount { get; init; }
    public short WinCount { get; init; }
    public short LoseCount { get; init; }
    public short DrawCount { get; init; }
    public short DiscCount { get; init; }
    public int LadderIncrease { get; init; } = 1000;
}

public sealed record GuildLadderLogEntry
{
    public long Seq { get; init; }
    public ulong? GuildCode { get; init; }
    public string? GuildName { get; init; }
    public int? GuildLadder { get; init; }
    public int? LadderIncrease { get; init; }
    public DateTimeOffset LogAt { get; init; }
}

public sealed record GuildMatchEntry
{
    public long MatchCode { get; init; }
    public required ulong GuildCode { get; init; }
    public byte JoinType { get; init; }
    public byte JoinUsers { get; init; }
    public byte KillCount { get; init; }
    public byte DeathCount { get; init; }
    public short GetLadder { get; init; }
    public int TotalLadder { get; init; }
    public int GuildValue { get; init; }
    public DateTimeOffset MatchedAt { get; init; }
}

public sealed record GuildRankingEntry
{
    public long Seq { get; init; }
    public string WeekDay { get; init; } = "";
    public required int Ranking { get; init; }
    public required ulong GuildCode { get; init; }
    public string GuildName { get; init; } = "";
    public int GuildPointIncrease { get; init; }
    public int GuildRankingPoint { get; init; }
    public DateTimeOffset SnapshotAt { get; init; }
}

public sealed record GuildStateEntry
{
    public long Seq { get; init; }
    public required ulong GuildCode { get; init; }
    public required byte StateType { get; init; }
    public string StateNote { get; init; } = "";
    public DateTimeOffset RegAt { get; init; }
}
