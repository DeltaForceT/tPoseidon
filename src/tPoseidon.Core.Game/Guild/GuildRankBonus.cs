using tPoseidon.Core.LibConfig;
using GuildRecord = tPoseidon.Core.Data.Guild;

namespace tPoseidon.Core.Game.Guild;

public readonly record struct GuildBonusStanding(int Rank, float Ratio);

public sealed class GuildRankBonus
{
    public const int NoGuildRank = 9999;
    public const int FirstBonusRank = 1;
    public const int LastBonusRank = 10;

    private readonly GuildRankBonusTable _table;

    public GuildRankBonus(GuildRankBonusTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _table = table;
    }

    public static bool HoldsBonusRank(GuildRecord guild) =>
        guild.GuildRanking is >= FirstBonusRank and <= LastBonusRank;

    public float RatioFor(int rank) => _table.RatioByRank.GetValueOrDefault(rank);

    public GuildBonusStanding? StandingOf(GuildRecord? guild) =>
        guild is not null && HoldsBonusRank(guild) ? new GuildBonusStanding(guild.GuildRanking, RatioFor(guild.GuildRanking)) : null;

    public static bool Changed(GuildBonusStanding? standing, float storedRatio) =>
        standing is { } held ? held.Ratio != storedRatio : storedRatio != 0f;

    public static GuildBonusStanding Report(GuildBonusStanding? standing) =>
        standing ?? new GuildBonusStanding(NoGuildRank, 0f);

    public static long Extra(long baseGain, float ratio)
    {
        if (baseGain <= 0 || ratio <= 0f) return 0;
        return (long)MathF.Truncate(baseGain * ratio);
    }
}
