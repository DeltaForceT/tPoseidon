namespace tPoseidon.Core.Data;

public sealed class InMemoryGmSkillSwapStore : IGmSkillSwapStore
{
    private readonly ISkillStore _skills;
    private readonly Dictionary<ulong, IReadOnlyList<Skill>> _saved = new();
    private readonly object _gate = new();

    public InMemoryGmSkillSwapStore(ISkillStore skills)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
    }

    public SkillSwapResult? Swap(ulong characterId, IReadOnlyList<Skill> gmSkills)
    {
        ArgumentNullException.ThrowIfNull(gmSkills);
        lock (_gate)
        {
            if (_saved.ContainsKey(characterId)) return null;
            var before = _skills.GetForCharacter(characterId);
            _saved[characterId] = before;
            Replace(characterId, gmSkills);
            return new SkillSwapResult(before, _skills.GetForCharacter(characterId));
        }
    }

    public SkillSwapResult? Restore(ulong characterId, IReadOnlySet<int> gmSkillIds)
    {
        lock (_gate)
        {
            if (!_saved.Remove(characterId, out var saved)) return null;
            var before = _skills.GetForCharacter(characterId);
            Replace(characterId, GmSkillSwapRules.Restored(saved, before, gmSkillIds));
            return new SkillSwapResult(before, _skills.GetForCharacter(characterId));
        }
    }

    private void Replace(ulong characterId, IReadOnlyList<Skill> skills)
    {
        _skills.DeleteAllForCharacter(characterId);
        foreach (var skill in skills) _skills.Upsert(skill with { CharacterId = characterId });
    }
}
