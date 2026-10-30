namespace tPoseidon.Core.Data;

public sealed record SkillSwapResult(IReadOnlyList<Skill> Before, IReadOnlyList<Skill> After);

public interface IGmSkillSwapStore
{
    SkillSwapResult? Swap(ulong characterId, IReadOnlyList<Skill> gmSkills);

    SkillSwapResult? Restore(ulong characterId, IReadOnlySet<int> gmSkillIds);
}
