namespace tPoseidon.Core.LibConfig;

public sealed record RandomStatValueEntry
{
    public required int Id { get; init; }
    public int StatType { get; init; }
    public int MinValue { get; init; }
    public int MaxValue { get; init; }
    public int Probability { get; init; }
}
