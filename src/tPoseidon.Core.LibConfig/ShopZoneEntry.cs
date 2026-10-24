namespace tPoseidon.Core.LibConfig;

public sealed record ShopZoneEntry
{
    public required string MapName { get; init; }
    public required int NpcType { get; init; }
    public int PosId { get; init; }
    public float PurchaseRate { get; init; }
    public float SellingRate { get; init; }
    public string ItemTable { get; init; } = string.Empty;
    public string Script { get; init; } = string.Empty;
}
