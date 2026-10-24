namespace tPoseidon.Core.LibConfig;

public sealed record MonsterTacticsExEntry
{
    public required int Id { get; init; }
    public required int MonsterId { get; init; }
    public int TacticsId { get; init; }
    public string ExtraData { get; init; } = string.Empty;
}
