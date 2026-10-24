namespace tPoseidon.Core.Data;

public readonly record struct GuildLeagueRecord(int Battles, int Wins, int Losses)
{
    public static GuildLeagueRecord None => default;
}

public sealed record GuildLadderStanding(uint GuildCode, int Ladder, int Matches, int Wins, int Losses, int Draws, int Disconnects)
{
    public int Decided => Wins + Losses + Draws + Disconnects;

    public GuildLeagueRecord League => new(Matches, Wins, Losses);
}

public static class GuildMatchOutcome
{
    public const byte Invalid = 0;
    public const byte Win = 1;
    public const byte Lose = 2;
    public const byte Draw = 3;
    public const byte Disconnect = 4;
}

public sealed record GuildMatchSideRecord(uint GuildCode, byte JoinUsers, byte Kills, byte Deaths, short GetLadder, int TotalLadder, int GuildValue, byte Outcome);

public sealed record GuildMatchRecord(
    DateTimeOffset Begin,
    DateTimeOffset End,
    byte MatchType,
    ushort MatchMap,
    byte TimeoutMinutes,
    ushort WinningCount,
    short EtcNum,
    GuildMatchSideRecord Make,
    GuildMatchSideRecord Challenge);

public static class GuildMatchStoreErrors
{
    public const int Ok = 0;
    public const int MakeLadderMissing = 104;
    public const int ChallengeLadderMissing = 105;
}

public sealed record GuildLadderRankRow(string GuildName, GuildLadderStanding Standing);

public interface IGuildMatchStore
{
    GuildLadderStanding? Standing(uint guildCode);

    IReadOnlyList<GuildLadderRankRow> TopLadder(int count);

    int LadderPosition(uint guildCode);

    int MatchesBetween(uint guildA, uint guildB, DateTimeOffset dayStart, DateTimeOffset dayEnd);

    int Insert(GuildMatchRecord match);
}
