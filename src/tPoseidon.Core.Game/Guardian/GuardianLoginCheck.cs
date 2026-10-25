using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Guardian;

public static class GuardianLoginCheck
{
    public const short ResetCardLevel = 1;

    public static GuardianState Validate(GuardianState guardian, IReadOnlyDictionary<int, int> nextExpByLevel)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(nextExpByLevel);
        var checkedGuardian = guardian;
        var spent = guardian.Skills.Where(skill => skill.Type == GuardianSkillType.Card).Sum(skill => skill.Level - 1);
        if (guardian.SkillPoint + spent != guardian.Level)
        {
            checkedGuardian = checkedGuardian with
            {
                SkillPoint = guardian.Level,
                Skills = guardian.Skills.Select(skill => skill.Type == GuardianSkillType.Card ? skill with { Level = ResetCardLevel } : skill).ToArray(),
            };
        }
        var threshold = GuardianGrowth.Threshold(nextExpByLevel, guardian.Level);
        return threshold > 0 && guardian.Exp >= threshold ? checkedGuardian with { Exp = 0 } : checkedGuardian;
    }
}
