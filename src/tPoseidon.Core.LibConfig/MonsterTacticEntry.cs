namespace tPoseidon.Core.LibConfig;

public sealed record MonsterTacticEntry
{
    public required int Id { get; init; }
    public required string TacticsName { get; init; }
    public int FactorType { get; init; }
    public int FactorValue { get; init; }
    public int ActionType { get; init; }
    public int SkillId { get; init; }
    public int SkillLevel { get; init; }
    public int TimeSeconds { get; init; }
    public int ActionRatio { get; init; }
    public int Option { get; init; }
    public int RandomWeight { get; init; }
    public string Speech { get; init; } = string.Empty;
}
