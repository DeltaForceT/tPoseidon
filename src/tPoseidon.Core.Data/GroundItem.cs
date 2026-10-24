namespace tPoseidon.Core.Data;

public sealed class GroundItem
{
    public required ushort EntityId { get; init; }
    public required ushort MapId { get; init; }
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
    public required int ItemIndex { get; init; }
    public required int Count { get; init; }
    public required ulong DroppedByMobEntityId { get; init; }
    public DateTimeOffset DroppedAt { get; init; } = DateTimeOffset.UtcNow;
}
