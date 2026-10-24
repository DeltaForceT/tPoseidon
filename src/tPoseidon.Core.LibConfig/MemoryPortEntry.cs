namespace tPoseidon.Core.LibConfig;

public sealed record MemoryPortEntry(int ItemId, int Type, int SlotCount)
{
    public const string TableName = "Item_MemoryPortParam";
}
