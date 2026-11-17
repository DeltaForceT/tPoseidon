namespace tPoseidon.Core.Data;

public static class ItemStoreExtensions
{
    public static int BagSlotsInUse(this IItemStore store, ulong characterId)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).Count
            + store.GetForCharacterBySlotKind(characterId, SlotKind.Equipment).Count;
    }
}
