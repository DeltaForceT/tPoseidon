namespace tPoseidon.Core.Data;

public interface IItemStore
{
        Item GrantToCharacter(
        ulong characterId,
        int itemIndex,
        byte itemClass,
        byte slotKind,
        byte slotPosition,
        ushort stack = 1);

        IReadOnlyList<Item> GetAllForCharacter(ulong characterId);

        IReadOnlyList<Item> GetForCharacterBySlotKind(ulong characterId, byte slotKind);

        Item? FindByUid(ulong itemUid);

        Item? FindBySlot(ulong characterId, byte slotKind, byte slotPosition);

        bool MoveToSlot(ulong itemUid, byte newSlotKind, byte newSlotPosition);

        bool UnassignFromCharacter(ulong itemUid);

        bool AssignToCharacter(ulong itemUid, ulong characterId, byte slotKind, byte slotPosition);

        bool Delete(ulong itemUid);

        ushort AdjustStack(ulong itemUid, int delta);

    bool SetStack(ulong itemUid, ushort stack);
}
