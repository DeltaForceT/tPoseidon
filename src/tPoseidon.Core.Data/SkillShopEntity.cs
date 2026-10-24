namespace tPoseidon.Core.Data;

public sealed record SkillShopEntity
{
    public required ushort EntityId { get; init; }
    public required ushort MapId { get; init; }
    public required int NpcType { get; init; }
    public required int PosId { get; init; }
    public int JobQuestId { get; init; }
    public int LimitJobType { get; init; }
    public int ChangeType { get; init; }
    public int ChangeJob { get; init; }
    public float PurchaseRate { get; init; }
    public float SellingRate { get; init; }
    public string ItemTable { get; init; } = string.Empty;
    public string LimitSkillMaster { get; init; } = string.Empty;
    public IReadOnlyList<(int Kind, string Text)> Pages { get; init; } = Array.Empty<(int Kind, string Text)>();
    public required ushort SpawnX { get; init; }
    public required ushort SpawnY { get; init; }
}
