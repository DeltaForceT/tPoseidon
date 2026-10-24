namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryMonsterTacticsLookup : IMonsterTacticsLookup
{
    private readonly IReadOnlyList<TacticsMonEntry> _allTactics;
    private readonly IReadOnlyList<MonAITacticsEntry> _allAI;
    private readonly Dictionary<int, List<TacticsMonEntry>> _tacticsByMonster;
    private readonly Dictionary<int, List<MonAITacticsEntry>> _aiByMonster;

    public InMemoryMonsterTacticsLookup(
        IEnumerable<TacticsMonEntry> tactics,
        IEnumerable<MonAITacticsEntry> ai)
    {
        ArgumentNullException.ThrowIfNull(tactics);
        ArgumentNullException.ThrowIfNull(ai);

        var tlist = tactics.ToList();
        var alist = ai.ToList();
        _allTactics = tlist;
        _allAI = alist;
        _tacticsByMonster = new Dictionary<int, List<TacticsMonEntry>>();
        _aiByMonster = new Dictionary<int, List<MonAITacticsEntry>>();

        foreach (var t in tlist)
        {
            if (!_tacticsByMonster.TryGetValue(t.MonsterId, out var bucket))
            {
                bucket = new List<TacticsMonEntry>();
                _tacticsByMonster[t.MonsterId] = bucket;
            }
            bucket.Add(t);
        }
        foreach (var a in alist)
        {
            if (!_aiByMonster.TryGetValue(a.MonsterId, out var bucket))
            {
                bucket = new List<MonAITacticsEntry>();
                _aiByMonster[a.MonsterId] = bucket;
            }
            bucket.Add(a);
        }
    }

    public IReadOnlyCollection<TacticsMonEntry> AllTactics => _allTactics;
    public IReadOnlyCollection<MonAITacticsEntry> AllAI => _allAI;
    public int TacticsCount => _allTactics.Count;
    public int AICount => _allAI.Count;

    public IReadOnlyList<TacticsMonEntry> GetTactics(int monsterId)
        => _tacticsByMonster.TryGetValue(monsterId, out var v) ? v : (IReadOnlyList<TacticsMonEntry>)Array.Empty<TacticsMonEntry>();

    public IReadOnlyList<MonAITacticsEntry> GetAI(int monsterId)
        => _aiByMonster.TryGetValue(monsterId, out var v) ? v : (IReadOnlyList<MonAITacticsEntry>)Array.Empty<MonAITacticsEntry>();
}
