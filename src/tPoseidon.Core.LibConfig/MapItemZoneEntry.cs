namespace tPoseidon.Core.LibConfig;

public sealed record MapItemZoneEntry
{
    public required string MapName { get; init; }
    public required int ItemId { get; init; }
    public int MonsterId { get; init; }
    public int MinDepth { get; init; }
    public int MaxDepth { get; init; }
    public int Avr { get; init; }
    public int Max { get; init; }
    public int Priority { get; init; }
    public string TimeTable { get; init; } = string.Empty;
}
