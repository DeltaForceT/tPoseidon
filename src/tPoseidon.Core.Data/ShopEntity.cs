namespace tPoseidon.Core.Data;

public sealed record ShopEntity
{
    public required ushort EntityId { get; init; }
    public required ushort MapId { get; init; }
    public required int NpcType { get; init; }
    public required int PosId { get; init; }
    public required float PurchaseRate { get; init; }
    public required float SellingRate { get; init; }
    public required string ItemTable { get; init; }
    public required string Script { get; init; }
    public required ushort SpawnX { get; init; }
    public required ushort SpawnY { get; init; }
}
