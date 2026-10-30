using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Quests;

public static class NpcCheckRules
{
    public const int Allowed = 1;
    public const int NotAllowed = 2;
    public const int FirstJob = 1;
    public const int LastJob = 33;
    public const int SecondMaskFirstJob = 17;
    public const int SkillLevelLow = 2;
    public const int SkillMissing = 3;
    public const int RandomMoveZones = 5;

    public static int Job(Character character, int jobTypeFlag1, int jobTypeFlag2)
    {
        ArgumentNullException.ThrowIfNull(character);
        return Job(OccupationRules.CurrentJobOf(character), jobTypeFlag1, jobTypeFlag2);
    }

    public static int Job(int job, int jobTypeFlag1, int jobTypeFlag2)
    {
        if (job < FirstJob || job > LastJob) return NotAllowed;
        var allowed = job < SecondMaskFirstJob
            ? (jobTypeFlag1 & (1 << (job - 1))) != 0
            : (jobTypeFlag2 & (1 << (job - SecondMaskFirstJob))) != 0;
        return allowed ? Allowed : NotAllowed;
    }

    public static int SkillSlot((int SkillId, int Level) slot, IReadOnlyDictionary<int, int> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        if (!skills.TryGetValue(slot.SkillId, out var level)) return SkillMissing;
        return level < slot.Level ? SkillLevelLow : Allowed;
    }

    public static int AllSkills(IReadOnlyList<(int SkillId, int Level)> slots, IReadOnlyDictionary<int, int> skills)
    {
        ArgumentNullException.ThrowIfNull(slots);
        foreach (var slot in slots)
        {
            if (slot.SkillId == 0) continue;
            var result = SkillSlot(slot, skills);
            if (result != Allowed) return result;
        }
        return Allowed;
    }
}
