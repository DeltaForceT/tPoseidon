namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryEsActionLookup : IEsActionLookup
{
    private readonly IReadOnlyList<EsActionEntry> _all;
    private readonly Dictionary<(string Skill, int Level), EsActionEntry> _byKey;
    private readonly Dictionary<string, List<EsActionEntry>> _bySkill;

    public InMemoryEsActionLookup(IEnumerable<EsActionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        _all = list;
        _byKey = new Dictionary<(string, int), EsActionEntry>();
        _bySkill = new Dictionary<string, List<EsActionEntry>>(StringComparer.Ordinal);
        foreach (var e in list)
        {
            _byKey[(e.SkillName, e.Level)] = e;
            if (!_bySkill.TryGetValue(e.SkillName, out var bucket))
            {
                bucket = new List<EsActionEntry>();
                _bySkill[e.SkillName] = bucket;
            }
            bucket.Add(e);
        }
    }

    public IReadOnlyCollection<EsActionEntry> All => _all;
    public int Count => _all.Count;

    public EsActionEntry? GetBySkillAndLevel(string skillName, int level)
        => _byKey.TryGetValue((skillName, level), out var v) ? v : null;

    public IReadOnlyList<EsActionEntry> GetBySkill(string skillName)
        => _bySkill.TryGetValue(skillName, out var v) ? v : (IReadOnlyList<EsActionEntry>)Array.Empty<EsActionEntry>();
}
