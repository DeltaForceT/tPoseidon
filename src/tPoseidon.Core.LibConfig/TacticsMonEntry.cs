namespace tPoseidon.Core.LibConfig;

public sealed record TacticsMonEntry
{
    public required int MonsterId { get; init; }
    public required int TacticsId { get; init; }
    public int Probability { get; init; }
    public int MinHpRatio { get; init; }
    public int MaxHpRatio { get; init; }
    public string Action { get; init; } = string.Empty;
    public int Param1 { get; init; }
    public int Param2 { get; init; }
    public int Param3 { get; init; }
}
