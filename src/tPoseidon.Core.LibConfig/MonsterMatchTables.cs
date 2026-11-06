using tPoseidon.Core.Common.Indexing;

namespace tPoseidon.Core.LibConfig;

public sealed record MonsterMatchEntry(int TriggerId, int MonsterId, int Point);

public sealed record MonsterMatchTables(IReadOnlyDictionary<int, MonsterMatchEntry> ByTrigger)
{
    public const string Table = "MonsterMatchParam";

    public static MonsterMatchTables Empty { get; } = new(new Dictionary<int, MonsterMatchEntry>());

    private readonly SourceBoundIndex<int, MonsterMatchEntry> _byMonster = new(entry => entry.MonsterId);

    public MonsterMatchEntry? ByMonster(int monsterId) => _byMonster.Over(ByTrigger, ByTrigger.Values).Find(monsterId);
}
