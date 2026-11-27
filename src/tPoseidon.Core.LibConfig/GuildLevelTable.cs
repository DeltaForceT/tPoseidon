namespace tPoseidon.Core.LibConfig;

public sealed record GuildLevelRow(int Level, int Point);

public sealed record GuildLevelTable(IReadOnlyList<GuildLevelRow> Rows)
{
    public const string TableName = "GuildLevelInfo";

    public const int DefaultLevel = 1;

    public static GuildLevelTable Empty { get; } = new(Array.Empty<GuildLevelRow>());

    public int LevelOf(int guildPoint)
    {
        foreach (var row in Rows)
        {
            if (guildPoint <= row.Point) return row.Level;
        }
        return DefaultLevel;
    }
}
