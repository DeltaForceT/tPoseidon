namespace tPoseidon.Core.LibConfig;

public sealed record ShadowAiEntry(int MonsterId, int TargetId, int AiType);

public sealed class ShadowAiTable
{
    public const string TableName = "ShadowAI";

    private readonly IReadOnlyDictionary<int, ShadowAiEntry> _byMonster;

    public ShadowAiTable(IEnumerable<ShadowAiEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byMonster = entries.GroupBy(entry => entry.MonsterId).ToDictionary(group => group.Key, group => group.First());
    }

    public static ShadowAiTable Empty { get; } = new(Array.Empty<ShadowAiEntry>());

    public int Count => _byMonster.Count;

    public ShadowAiEntry? Find(int monsterId) => _byMonster.GetValueOrDefault(monsterId);
}
