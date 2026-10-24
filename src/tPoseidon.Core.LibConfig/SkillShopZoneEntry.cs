namespace tPoseidon.Core.LibConfig;

public sealed record SkillShopZoneEntry
{
    public required string MapName { get; init; }
    public required int NpcType { get; init; }
    public int PosId { get; init; }
    public int JobQuestId { get; init; }
    public int LimitJobType { get; init; }
    public int ChangeType { get; init; }
    public int ChangeJob { get; init; }
    public float PurchaseRate { get; init; }
    public float SellingRate { get; init; }
    public string ItemTable { get; init; } = string.Empty;
    public string LimitSkillMaster { get; init; } = string.Empty;
    public IReadOnlyList<(int Kind, string Text)> Pages { get; init; } = Array.Empty<(int Kind, string Text)>();
}
