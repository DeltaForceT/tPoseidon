namespace tPoseidon.Core.Game.Skills;

public enum SkillGrantOutcome
{
    Learn,
    Raise,
    UnknownSkill,
    LevelOutOfRange,
    NotHigher,
}

public static class SkillGrantRules
{
    public static int HighestLevelOf(SkillDefinition skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return Math.Min(skill.Row.MaxSkillLevel, SkillDefinition.HighestLevel);
    }

    public static SkillGrantOutcome Decide(SkillDefinition? skill, int? heldLevel, int level)
    {
        if (skill is null) return SkillGrantOutcome.UnknownSkill;
        if (level < SkillDefinition.LowestLevel || level > HighestLevelOf(skill)) return SkillGrantOutcome.LevelOutOfRange;
        if (heldLevel is not { } held || held < SkillDefinition.LowestLevel) return SkillGrantOutcome.Learn;
        return level > held ? SkillGrantOutcome.Raise : SkillGrantOutcome.NotHigher;
    }
}
