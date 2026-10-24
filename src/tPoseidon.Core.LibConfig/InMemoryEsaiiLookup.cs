namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryEsaiiLookup : IEsaiiLookup
{
    private readonly IReadOnlyList<EsaiiEntry> _all;
    private readonly Dictionary<(string Skill, int Level), EsaiiEntry> _byKey;
    private readonly Dictionary<string, List<EsaiiEntry>> _bySkill;

    public InMemoryEsaiiLookup(IEnumerable<EsaiiEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        _all = list;
        _byKey = new Dictionary<(string, int), EsaiiEntry>();
        _bySkill = new Dictionary<string, List<EsaiiEntry>>(StringComparer.Ordinal);
        foreach (var e in list)
        {
            _byKey[(e.SkillName, e.Level)] = e;
            if (!_bySkill.TryGetValue(e.SkillName, out var bucket))
            {
                bucket = new List<EsaiiEntry>();
                _bySkill[e.SkillName] = bucket;
            }
            bucket.Add(e);
        }
    }

    public IReadOnlyCollection<EsaiiEntry> All => _all;
    public int Count => _all.Count;

    public EsaiiEntry? GetBySkillAndLevel(string skillName, int level)
        => _byKey.TryGetValue((skillName, level), out var v) ? v : null;

    public IReadOnlyList<EsaiiEntry> GetBySkill(string skillName)
        => _bySkill.TryGetValue(skillName, out var v) ? v : (IReadOnlyList<EsaiiEntry>)Array.Empty<EsaiiEntry>();
}
