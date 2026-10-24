namespace tPoseidon.Core.LibConfig;

public sealed record EsActionEntry
{
    public required string SkillName { get; init; }
    public required int Level { get; init; }
    public int Ratio1 { get; init; }
    public int Ratio2 { get; init; }
    public float ApplyRatio { get; init; }
}
