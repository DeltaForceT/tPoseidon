namespace tPoseidon.Core.Data;

public interface IMyCampStore
{
    bool TryGrantStarterItems(uint accountId, IReadOnlyList<int> itemIndexes);

    MyCampSlot CreateSlotForCharacter(ulong characterId, string slotName = "");

    MyCampSlot? FindSlot(ulong slotCode);

    IReadOnlyList<MyCampSlot> GetSlotsForCharacter(ulong characterId);

    bool UpdateSlotName(ulong slotCode, string slotName);

    bool DeleteSlot(ulong slotCode);

    MyCampItem AddItemForAccount(uint accountId, int itemIndex, byte itemWhere = MyCampItemWhere.New);

    int AddItemsForAccount(uint accountId, IReadOnlyList<int> itemIndexes, byte itemWhere = MyCampItemWhere.New);

    MyCampItem? FindItem(ulong itemUid);

    IReadOnlyList<MyCampItem> GetItemsForAccount(uint accountId);

    IReadOnlyList<MyCampItem> GetItemsForAccountByWhere(uint accountId, byte itemWhere);

    MyCampItem? GetLatestBuyItem(uint accountId);

    bool MoveItemToInventory(ulong itemUid);

    bool MoveItemTo(ulong itemUid, byte itemWhere);

    bool DeleteItem(ulong itemUid);

    int BulkDeleteItems(IEnumerable<ulong> itemUids);

    MyCampDeco InsertDeco(ulong slotCode, ulong itemUid, short x, short y, byte z, byte direction, ulong parentsUid = 0);

    IReadOnlyList<MyCampDeco> GetDecoForSlot(ulong slotCode);

    IReadOnlySet<ulong> GetPlacedItemUidsForAccount(uint accountId);

    void ReplaceDeco(ulong slotCode, IReadOnlyList<MyCampDeco> placed);

    bool RemoveDeco(ulong slotCode, ulong itemUid);

    int InitDecoForSlot(ulong slotCode);

    int MoveDecoToInventory(ulong slotCode);
}
