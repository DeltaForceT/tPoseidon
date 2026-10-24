namespace tPoseidon.Core.LibConfig;

public sealed record GuildGameTypeEntry(int Type, int TimeLimit, IReadOnlyList<int> Maps)
{
    public int? MapOf(uint choice)
    {
        if (choice != 0 && Maps.Contains((int)Math.Min(choice, int.MaxValue))) return (int)choice;
        return choice < Maps.Count && Maps[(int)choice] != 0 ? Maps[(int)choice] : null;
    }
}

public sealed record GuildGameTypeTables(IReadOnlyDictionary<int, GuildGameTypeEntry> Types)
{
    public const string Table = "GuildGameTypeInfo";
    public const int MapColumns = 6;

    public static GuildGameTypeTables Empty { get; } = new(new Dictionary<int, GuildGameTypeEntry>());
}
