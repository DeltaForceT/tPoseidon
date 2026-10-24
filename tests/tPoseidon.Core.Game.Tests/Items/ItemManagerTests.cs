using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.GameTests.Items;

public class ItemManagerTests
{
    private const ulong CharId = 0x10000001;

    private static ItemManager Make(out InMemoryItemStore store)
    {
        store = new InMemoryItemStore();
        var catalog = new InMemoryItemParamLookup(new[]
        {
            new ItemParamEntry { Id = 3030, Class = 5, Name = "Weapon" },
            new ItemParamEntry { Id = 4030, Class = 5, Name = "Hat" },
            new ItemParamEntry { Id = 6530, Class = 5, Name = "Shield" },
            new ItemParamEntry { Id = 11805, Class = 4, Name = "Quest Item" },
        });
        return new ItemManager(store, catalog);
    }

    [Fact]
    public void EquipFromInventory_EmptySlot_FiresEquippedEvent()
    {
        var im = Make(out var store);
        var item = store.GrantToCharacter(CharId, 3030, itemClass: 2, slotKind: SlotKind.Inventory, slotPosition: 0);
        ItemEquippedEvent? captured = null;
        im.ItemEquipped += e => captured = e;

        var result = im.EquipFromInventory(CharId, item.ItemUid, EquipmentSlot.Weapon);

        Assert.Equal(EquipResult.Equipped, result);
        Assert.NotNull(captured);
        Assert.Equal(item.ItemUid, captured!.ItemUid);
        Assert.Equal(EquipmentSlot.Weapon, captured.SlotPosition);
        Assert.Null(captured.DisplacedItemUid);

        var moved = store.FindByUid(item.ItemUid);
        Assert.Equal(SlotKind.Equipment, moved!.SlotKind);
        Assert.Equal(EquipmentSlot.Weapon, moved.SlotPosition);
    }

    [Fact]
    public void EquipFromInventory_OccupiedSlot_ReturnsSlotOccupied()
    {
        var im = Make(out var store);
        store.GrantToCharacter(CharId, 3030, itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon);
        var inv = store.GrantToCharacter(CharId, 3030, itemClass: 2, slotKind: SlotKind.Inventory, slotPosition: 0);

        var result = im.EquipFromInventory(CharId, inv.ItemUid, EquipmentSlot.Weapon);

        Assert.Equal(EquipResult.SlotOccupied, result);
        Assert.Equal(SlotKind.Inventory, store.FindByUid(inv.ItemUid)!.SlotKind);
    }

    [Fact]
    public void ExchangeInventoryAndEquip_OccupiedSlot_SwapsAndFiresWithDisplacement()
    {
        var im = Make(out var store);
        var equipped = store.GrantToCharacter(CharId, 3030, itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon);
        var inv = store.GrantToCharacter(CharId, 4030, itemClass: 2, slotKind: SlotKind.Inventory, slotPosition: 0);
        ItemEquippedEvent? captured = null;
        im.ItemEquipped += e => captured = e;

        var result = im.ExchangeInventoryAndEquip(CharId, inv.ItemUid, EquipmentSlot.Weapon);

        Assert.Equal(EquipResult.Equipped, result);
        Assert.Equal(equipped.ItemUid, captured!.DisplacedItemUid);
        Assert.Equal(SlotKind.Equipment, store.FindByUid(inv.ItemUid)!.SlotKind);
        Assert.Equal(SlotKind.Inventory, store.FindByUid(equipped.ItemUid)!.SlotKind);
    }

    [Fact]
    public void EquipFromInventory_NotInInventory_Rejects()
    {
        var im = Make(out var store);
        var equipped = store.GrantToCharacter(CharId, 3030, itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Hat);

        var result = im.EquipFromInventory(CharId, equipped.ItemUid, EquipmentSlot.Weapon);

        Assert.Equal(EquipResult.NotInInventory, result);
    }

    [Fact]
    public void EquipFromInventory_OtherCharactersItem_Rejects()
    {
        var im = Make(out var store);
        var item = store.GrantToCharacter(0x99999, 3030, itemClass: 2, slotKind: SlotKind.Inventory, slotPosition: 0);

        var result = im.EquipFromInventory(CharId, item.ItemUid, EquipmentSlot.Weapon);

        Assert.Equal(EquipResult.NotFound, result);
    }

    [Fact]
    public void ReleaseToInventory_FromEquipment_FiresReleasedEvent()
    {
        var im = Make(out var store);
        var equipped = store.GrantToCharacter(CharId, 3030, itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon);
        ItemReleasedEvent? captured = null;
        im.ItemReleased += e => captured = e;

        var result = im.ReleaseToInventory(CharId, equipped.ItemUid);

        Assert.Equal(ReleaseResult.Released, result);
        Assert.Equal(EquipmentSlot.Weapon, captured!.FromSlotPosition);
        Assert.Equal((byte)0, captured.ToInventorySlot);
        Assert.Equal(SlotKind.Inventory, store.FindByUid(equipped.ItemUid)!.SlotKind);
    }

    [Fact]
    public void ReleaseToInventory_NotEquipped_Rejects()
    {
        var im = Make(out var store);
        var inv = store.GrantToCharacter(CharId, 3030, itemClass: 2, slotKind: SlotKind.Inventory, slotPosition: 0);

        var result = im.ReleaseToInventory(CharId, inv.ItemUid);

        Assert.Equal(ReleaseResult.NotEquipped, result);
    }

    [Fact]
    public void ReleaseAllToInventory_LowLevel_KeepsEarAndTail()
    {
        var im = Make(out var store);
        var weapon = store.GrantToCharacter(CharId, 3030, itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon);
        var ear = store.GrantToCharacter(CharId, 6900, itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Ear);
        var tail = store.GrantToCharacter(CharId, 6950, itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Tail);

        var released = im.ReleaseAllToInventory(CharId, characterLevel: 10);

        Assert.Single(released);
        Assert.Equal(weapon.ItemUid, released[0].ItemUid);
        Assert.Equal(SlotKind.Equipment, store.FindByUid(ear.ItemUid)!.SlotKind);
        Assert.Equal(SlotKind.Equipment, store.FindByUid(tail.ItemUid)!.SlotKind);
    }

    [Fact]
    public void ReleaseAllToInventory_HighLevel_AlsoReleasesEarAndTail()
    {
        var im = Make(out var store);
        var weapon = store.GrantToCharacter(CharId, 3030, itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon);
        var ear = store.GrantToCharacter(CharId, 6900, itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Ear);

        var released = im.ReleaseAllToInventory(CharId, characterLevel: 20);

        Assert.Equal(2, released.Count);
        Assert.Equal(SlotKind.Inventory, store.FindByUid(weapon.ItemUid)!.SlotKind);
        Assert.Equal(SlotKind.Inventory, store.FindByUid(ear.ItemUid)!.SlotKind);
    }

    [Fact]
    public void DeleteItem_FromInventory_FiresDeletedEvent()
    {
        var im = Make(out var store);
        var inv = store.GrantToCharacter(CharId, 3030, itemClass: 2, slotKind: SlotKind.Inventory, slotPosition: 0);
        ItemDeletedEvent? captured = null;
        im.ItemDeleted += e => captured = e;

        Assert.True(im.DeleteItem(CharId, inv.ItemUid));
        Assert.Equal(inv.ItemUid, captured!.Item.ItemUid);
        Assert.Null(store.FindByUid(inv.ItemUid));
    }

    [Fact]
    public void UseStackable_DrainsToZero_AutoDeletes()
    {
        var im = Make(out var store);
        var stack = store.GrantToCharacter(CharId, 3030, itemClass: 1, slotKind: SlotKind.Inventory, slotPosition: 0, stack: 3);
        ItemDeletedEvent? captured = null;
        im.ItemDeleted += e => captured = e;

        Assert.Equal((ushort)2, im.UseStackable(CharId, stack.ItemUid));
        Assert.Equal((ushort)1, im.UseStackable(CharId, stack.ItemUid));
        Assert.Equal((ushort)0, im.UseStackable(CharId, stack.ItemUid));
        Assert.Equal(stack.ItemUid, captured!.Item.ItemUid);
        Assert.Null(store.FindByUid(stack.ItemUid));
    }

    [Fact]
    public void GrantQuestItem_NewMint_FiresGrantedEvent()
    {
        var im = Make(out var store);
        ItemGrantedEvent? captured = null;
        im.ItemGranted += e => captured = e;

        im.GrantQuestItem(CharId, itemIndex: 11805, questId: 1233);

        Assert.NotNull(captured);
        Assert.True(captured!.IsNewlyMinted);
        Assert.Equal(1233, captured.QuestId);
        Assert.False(captured.IsEquipment);
    }

    [Fact]
    public void GrantQuestItem_AlreadyOwned_FiresGrantedWithIsNewlyMintedFalse()
    {
        var im = Make(out var store);
        im.GrantQuestItem(CharId, itemIndex: 11805, questId: 1233);
        ItemGrantedEvent? second = null;
        im.ItemGranted += e => second = e;

        im.GrantQuestItem(CharId, itemIndex: 11805, questId: 1233);

        Assert.NotNull(second);
        Assert.False(second!.IsNewlyMinted);
    }

    [Fact]
    public void RevokeQuestItem_Existing_FiresRevokedEventAndDeletes()
    {
        var im = Make(out var store);
        var granted = im.GrantQuestItem(CharId, itemIndex: 11805, questId: 1233);
        ItemRevokedEvent? captured = null;
        im.ItemRevoked += e => captured = e;

        var revoked = im.RevokeQuestItem(CharId, itemIndex: 11805, questId: 1233);

        Assert.NotNull(revoked);
        Assert.Equal(granted.ItemUid, revoked!.ItemUid);
        Assert.NotNull(captured);
        Assert.Equal(1233, captured!.QuestId);
        Assert.Null(store.FindByUid(granted.ItemUid));
    }

    [Fact]
    public void RevokeQuestItem_NotOwned_ReturnsNullAndNoEvent()
    {
        var im = Make(out var store);
        var fired = false;
        im.ItemRevoked += _ => fired = true;

        var result = im.RevokeQuestItem(CharId, itemIndex: 11805);

        Assert.Null(result);
        Assert.False(fired);
    }
}
