namespace tPoseidon.Core.Game.Skills.Effects;

public static class SkillResist
{
    public const int Most = 100;

    public static int Of(IReadOnlyDictionary<int, int>? protections, int skillId) =>
        protections is not null && protections.TryGetValue(skillId, out var ratio) && ratio > 0 ? Math.Min(ratio, Most) : 0;
}
