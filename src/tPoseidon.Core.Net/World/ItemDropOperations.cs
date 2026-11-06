using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.World;

public static class ItemDropOperations
{
    public const ushort NoneLeft = 0;

    public static BagOperation Whole(Item held)
    {
        ArgumentNullException.ThrowIfNull(held);
        return new BagOperation(QuestResultFrames.Removed, held.ItemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), NoneLeft, held.ItemSlot);
    }

    public static BagOperation Part(Item held, ushort left)
    {
        ArgumentNullException.ThrowIfNull(held);
        return new BagOperation(QuestResultFrames.StackChanged, held.ItemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), left, held.ItemSlot);
    }

    public static int ShownByClient(ushort heldBefore, BagOperation operation) => heldBefore - operation.Count;
}
