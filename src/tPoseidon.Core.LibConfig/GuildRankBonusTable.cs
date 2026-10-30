namespace tPoseidon.Core.LibConfig;

public sealed record GuildRankBonusTable(IReadOnlyDictionary<int, float> RatioByRank)
{
    public const string TableName = "GuildRankBonusExp";

    public static GuildRankBonusTable Empty { get; } = new(new Dictionary<int, float>());

    public int RankCount => RatioByRank.Count;
}
