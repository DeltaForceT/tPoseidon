using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemorySkillStore : ISkillStore
{
    private readonly record struct SkillKey(ulong CharacterId, int SkillId);

    private readonly ConcurrentDictionary<SkillKey, Skill> _skills = new();

    public IReadOnlyList<Skill> GetForCharacter(ulong characterId)
    {
        return _skills.Values
            .Where(s => s.CharacterId == characterId)
            .OrderBy(s => s.SkillId)
            .ToList();
    }

    public Skill? Find(ulong characterId, int skillId)
    {
        var key = new SkillKey(characterId, skillId);
        return _skills.TryGetValue(key, out var s) ? s : null;
    }

    public void Upsert(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        var key = new SkillKey(skill.CharacterId, skill.SkillId);
        _skills[key] = skill;
    }

    public int DeleteAllForCharacter(ulong characterId)
    {
        var toRemove = _skills.Keys.Where(k => k.CharacterId == characterId).ToList();
        var removed = 0;
        foreach (var k in toRemove)
        {
            if (_skills.TryRemove(k, out _)) removed++;
        }
        return removed;
    }

    public int DeleteAboveLevel(ulong characterId, int maxLevel)
    {
        var toRemove = _skills
            .Where(kv => kv.Key.CharacterId == characterId && kv.Value.SkillLevel > maxLevel)
            .Select(kv => kv.Key)
            .ToList();
        var removed = 0;
        foreach (var k in toRemove)
        {
            if (_skills.TryRemove(k, out _)) removed++;
        }
        return removed;
    }
}
