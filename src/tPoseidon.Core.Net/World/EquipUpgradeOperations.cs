using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.World;

public static class EquipUpgradeOperations
{
    public static EquipUpgradeItem Delete(Item item) => Row(EquipUpgradeAction.Delete, item, item.Stack, item.ItemSlot);

    public static EquipUpgradeItem Update(Item item, ushort count, byte slot) => Row(EquipUpgradeAction.Update, item, count, slot);

    public static EquipUpgradeItem Take(Item item, ushort count) =>
        item.Stack > count ? Update(item, (ushort)(item.Stack - count), item.ItemSlot) : Delete(item);

    public static EquipUpgradeItem Insert(int itemIndex, byte itemClass, ushort count, byte slot, short refineType = 0, byte refineLevel = 0) =>
        new(EquipUpgradeAction.Insert, 0, itemIndex, itemClass, count, slot, refineType, refineLevel, 0);

    public static BagOperation ToBag(EquipUpgradeItem row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var kind = row.Action switch
        {
            EquipUpgradeAction.Delete => QuestResultFrames.Removed,
            EquipUpgradeAction.Update => QuestResultFrames.StackChanged,
            _ => QuestResultFrames.Created,
        };
        var count = row.Action == EquipUpgradeAction.Delete ? (ushort)0 : row.Count;
        return new BagOperation(kind, row.ItemClass, (uint)row.ItemIndex, BagItemIds.OnWire(row.ItemUid), count, row.Slot,
            (byte)row.RefineType, row.RefineLevel, row.RefineState);
    }

    private static EquipUpgradeItem Row(EquipUpgradeAction flag, Item item, ushort count, byte slot)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new EquipUpgradeItem(flag, item.ItemUid, item.ItemIndex, item.ItemClass, count, slot, item.RefineType, item.RefineLevel, item.RefineState);
    }
}
