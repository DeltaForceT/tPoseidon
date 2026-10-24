using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Guardian;

public static class GuardianOrderRules
{
    public const uint NotACard = 0;
    public const uint AlreadyListed = 1;
    public const uint NotListed = 2;
    public const uint Busy = 3;
    public const int SlotByteMask = 0xFF;

    public static byte SlotOf(int second) => (byte)(second & SlotByteMask);

    public static uint? Refusal(GuardianState? guardian, int skillId, bool remove, bool busy)
    {
        if (busy) return Busy;
        var skills = guardian?.Skills ?? Array.Empty<GuardianSkill>();
        var listed = skills.Any(skill => skill.Type == GuardianSkillType.Order && skill.SkillId == skillId);
        if (remove) return listed ? null : NotListed;
        if (!skills.Any(skill => skill.Type == GuardianSkillType.Card && skill.SkillId == skillId)) return NotACard;
        return listed ? AlreadyListed : null;
    }

    public static GuardianSkill Record(GuardianState guardian, int skillId, byte slot, bool remove)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        var source = remove
            ? guardian.Skills.First(skill => skill.Type == GuardianSkillType.Order && skill.SkillId == skillId)
            : guardian.Skills.First(skill => skill.Type == GuardianSkillType.Card && skill.SkillId == skillId);
        return new GuardianSkill(skillId, GuardianSkillType.Order, source.FType, source.Level, remove ? source.Slot : slot);
    }

    public static GuardianState Apply(GuardianState guardian, GuardianSkill record, bool remove)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(record);
        var kept = guardian.Skills.Where(skill => !(skill.Type == GuardianSkillType.Order && skill.SkillId == record.SkillId)).ToList();
        if (!remove) kept.Add(record);
        return guardian with { Skills = kept };
    }
}
