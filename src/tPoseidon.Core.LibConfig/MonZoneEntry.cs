namespace tPoseidon.Core.LibConfig;

public sealed record MonZoneEntry
{
    public required string MapName { get; init; }
    public required int MonsterId { get; init; }
    public float Ratio { get; init; }
    public int Avr { get; init; }
    public int Max { get; init; }
    public string TimeTable { get; init; } = string.Empty;
    public int Region { get; init; }
    public int RetrMonNumType { get; init; }
    public int RetrMonNum { get; init; }
}
