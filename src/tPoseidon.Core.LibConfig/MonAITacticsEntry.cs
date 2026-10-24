namespace tPoseidon.Core.LibConfig;

public sealed record MonAITacticsEntry
{
    public required int MonsterId { get; init; }
    public required int StateId { get; init; }
    public string Action { get; init; } = string.Empty;
    public int Param1 { get; init; }
    public int Param2 { get; init; }
    public int Param3 { get; init; }
}
