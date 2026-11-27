namespace tPoseidon.Core.Game.Skills.Effects;

public static class SkillResist
{
    public const int Most = 100;

    public static int Of(IReadOnlyDictionary<int, int>? protections, int skillId) =>
        protections is not null && protections.TryGetValue(skillId, out var ratio) && ratio > 0 ? Math.Min(ratio, Most) : 0;

    public static int OfEffects(IEnumerable<SkillEffect> held, int skillId)
    {
        ArgumentNullException.ThrowIfNull(held);
        var total = 0;
        foreach (var effect in held)
        {
            if (!effect.IsApplied || !effect.Kind.ResistsSkills.Contains(skillId)) continue;
            total += (int)Math.Truncate(effect.Amount * 100.0);
        }
        return Math.Min(total, Most);
    }

    public static SkillEffectMode Roll(SkillEffectMode mode, int percent, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (mode != SkillEffectMode.Applied || percent <= 0) return mode;
        return rng.Next(100) < Math.Clamp(100 - percent, 0, 100) ? SkillEffectMode.Applied : SkillEffectMode.Resisted;
    }
}
