namespace tPoseidon.Core.Data;

public static class GmSkillSwapRules
{
    public static IReadOnlyList<Skill> Restored(IReadOnlyList<Skill> saved, IReadOnlyList<Skill> current, IReadOnlySet<int> gmSkillIds)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(gmSkillIds);
        var restored = saved.ToDictionary(skill => skill.SkillId);
        foreach (var learned in current.Where(skill => !gmSkillIds.Contains(skill.SkillId)))
        {
            if (!restored.TryGetValue(learned.SkillId, out var kept) || learned.SkillLevel > kept.SkillLevel)
            {
                restored[learned.SkillId] = learned;
            }
        }
        return restored.Values.OrderBy(skill => skill.SkillId).ToList();
    }
}
