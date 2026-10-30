namespace tPoseidon.Core.LibConfig;

public sealed record GuildGameRewardEntry(int Score, int IconNo, IReadOnlyList<(int ItemId, int Count)> Items);

public sealed record GuildGameRewardTables(IReadOnlyList<GuildGameRewardEntry> Rows)
{
    public const string Table = "GuildGameRewardInfo";
    public const int ItemColumns = 3;

    public static GuildGameRewardTables Empty { get; } = new(Array.Empty<GuildGameRewardEntry>());

    public GuildGameRewardEntry? RowFor(int averageLadder) => Rows.FirstOrDefault(row => row.Score < averageLadder);
}
