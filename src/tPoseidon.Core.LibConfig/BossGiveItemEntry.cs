namespace tPoseidon.Core.LibConfig;

public readonly record struct BossGiveItemSlot(int ItemId, float Rate);

public sealed record BossGiveItemEntry
{
    public const string TableName = "BossMonsterparamEx";
    public const int SlotCount = 10;

    public required int MonsterId { get; init; }
    public required IReadOnlyList<BossGiveItemSlot> Slots { get; init; }

    public int FilledSlots => Slots.Count(slot => slot.ItemId != 0);
}
