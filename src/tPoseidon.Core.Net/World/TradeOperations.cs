using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.World;

public static class TradeOperations
{
    public static BagOperation Of(TradeItemChange change, Item? arrived, IReadOnlyList<ItemProperty>? properties)
    {
        ArgumentNullException.ThrowIfNull(change);
        var kind = change.Kind switch
        {
            TradeChangeKind.StackChanged => QuestResultFrames.StackChanged,
            TradeChangeKind.Removed => QuestResultFrames.Removed,
            _ => QuestResultFrames.Created,
        };
        var operation = new BagOperation(kind, change.ItemClass, (uint)change.ItemIndex, BagItemIds.OnWire(change.ItemUid), change.Count, change.MixSlot);
        if (change.Kind != TradeChangeKind.Created || arrived is null || arrived.ItemUid != change.ItemUid) return operation;
        return operation with
        {
            MixSlot = arrived.ItemSlot,
            RefineType = (byte)arrived.RefineType,
            RefineLevel = arrived.RefineLevel,
            RefineState = arrived.RefineState,
            Options = properties is { Count: > 0 } ? properties : null,
        };
    }
}
