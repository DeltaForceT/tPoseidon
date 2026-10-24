namespace tPoseidon.Core.LibConfig;

public sealed record MonsterMatchEntry(int TriggerId, int MonsterId, int Point);

public sealed record MonsterMatchTables(IReadOnlyDictionary<int, MonsterMatchEntry> ByTrigger)
{
    public const string Table = "MonsterMatchParam";

    public static MonsterMatchTables Empty { get; } = new(new Dictionary<int, MonsterMatchEntry>());

    public MonsterMatchEntry? ByMonster(int monsterId) => ByTrigger.Values.FirstOrDefault(entry => entry.MonsterId == monsterId);
}
