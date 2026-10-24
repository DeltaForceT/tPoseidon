using System.Collections.Generic;

namespace tPoseidon.Core.LibConfig;

public sealed class InMemorySkillParam2Lookup : ISkillParam2Lookup
{
    private readonly Dictionary<int, SkillParam2Entry> _byId;

    public InMemorySkillParam2Lookup(IEnumerable<SkillParam2Entry> entries)
    {
        _byId = new Dictionary<int, SkillParam2Entry>();
        foreach (var entry in entries)
        {
            _byId[entry.Id] = entry;
        }
    }

    public SkillParam2Entry? GetById(int skillId)
        => _byId.TryGetValue(skillId, out var entry) ? entry : null;

    public IEnumerable<SkillParam2Entry> All() => _byId.Values;

    public int Count => _byId.Count;
}
