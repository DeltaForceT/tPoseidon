namespace tPoseidon.Core.LibConfig;

public readonly record struct SkillRequirement(int SkillId, int Level);

public sealed record SkillCardEntry
{
    public required int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int CardType { get; init; }
    public int TmLevel { get; init; }
    public int SubCardType { get; init; }
    public int SubTmLevel { get; init; }
    public int AffectionPoint { get; init; }
    public int AffectionSkill { get; init; }
    public int RegisterLevel { get; init; }
    public IReadOnlyList<SkillRequirement> PreSkills { get; init; } = Array.Empty<SkillRequirement>();
    public string Job { get; init; } = string.Empty;
    public int Options { get; init; }
    public int JobType1 { get; init; }
    public int JobType2 { get; init; }
}
