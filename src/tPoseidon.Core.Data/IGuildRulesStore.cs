namespace tPoseidon.Core.Data;

public static class GuildRules
{
    public const int Ok = 0;
    public const int Failed = 50000;

    public const byte NotInGuild = 0;
    public const byte Applying = 1;
    public const byte Joined = 2;

    public const byte StandbyApplying = 0;
    public const byte StandbyMember = 1;
    public const byte StandbyRejected = 2;

    public const byte Approve = 1;
    public const byte Reject = 2;

    public const byte MasterGrade = 1;
    public const byte NewMemberGrade = 7;
    public const string MasterNick = "Master";

    public const byte DissolvePenalty = 0;
    public const byte LeavePenalty = 1;
    public const byte GmPenalty = 2;
    public const int LeavePenaltyDays = 7;
    public const int DissolvePenaltyMonths = 1;

    public const int MinApplyLevel = 10;
    public const byte ClosedState = 1;
    public const int NewGuildPoint = 1;
    public const int NewGuildRanking = 999999;
    public const int NoticesShown = 3;

    public static readonly IReadOnlyList<(string Name, int Point)> DefaultGrades = new[]
    {
        ("Master", 0), ("SubMaster", 0), ("Secretary", 0), ("Secretary", 5000), ("OldMember", 1000), ("Member", 500), ("NewMember", 0),
    };
}

public readonly record struct GuildResult<T>(int Error, T? Value, GuildPenalty? Penalty = null)
{
    public bool Succeeded => Error == GuildRules.Ok;
}

public static class GuildResult
{
    public static GuildResult<T> Ok<T>(T value) => new(GuildRules.Ok, value);

    public static GuildResult<T> Fail<T>(int error, GuildPenalty? penalty = null) => new(error, default, penalty);
}

public sealed record GuildFounded(uint GuildCode, string MasterNick, IReadOnlyList<GuildGrade> Grades);

public sealed record GuildPermitted(uint AccountId, string CharName, int Level, int Job);

public sealed record CharGuildInfo(
    byte State,
    uint GuildCode,
    string MemberNick,
    byte GradeCode,
    int EmblemCode,
    bool NewNotice,
    int GuildPoint,
    short MemberRight);

public sealed record GuildDetail(
    Guild Guild,
    int MaxMembers,
    string MasterName,
    GuildLadder Ladder,
    IReadOnlyList<GuildGrade> Grades,
    IReadOnlyList<GuildNotice> Notices);

public sealed record GuildRosterEntry(
    ulong CharacterId,
    string CharName,
    string MemberNick,
    byte GradeCode,
    int MemberPoint,
    short MemberRight,
    int Level,
    int Job,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastLogoutAt);

public sealed record GuildApplicant(ulong CharacterId, string CharName, int Level, int Job, DateTimeOffset AppliedAt);

public interface IGuildRulesStore
{
    GuildResult<GuildFounded> Create(ulong characterId, string guildName, string guildIntro);

    GuildResult<bool> Apply(ulong characterId, uint guildCode);

    int CancelApplication(ulong characterId);

    GuildResult<GuildPermitted> Permit(ulong characterId, uint guildCode, byte permit);

    int Leave(ulong characterId, uint guildCode);

    GuildResult<uint> Kick(ulong characterId, uint guildCode);

    int Dissolve(ulong chiefCharacterId, uint guildCode);

    CharGuildInfo CharacterGuild(ulong characterId);

    GuildDetail? Detail(uint guildCode);

    IReadOnlyList<GuildRosterEntry> Members(uint guildCode);

    IReadOnlyList<GuildApplicant> Applicants(uint guildCode);
}
