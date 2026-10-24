using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Tests;

public class InMemoryItemStoreTests
{
    private const ulong AliceId = 1001;
    private const ulong BobId = 1002;

    private const int RookieSwordIndex = 3030;
    private const int RookieHatIndex = 4030;
    private const int RookieShieldIndex = 6530;
    private const int DragonHornsIndex = 6903;
    private const int DragonTailIndex = 6953;
    private const int CarrotItemIndex = 11908;

    [Fact]
    public void GrantToCharacter_AssignsUniqueUidsAscending()
    {
        var store = new InMemoryItemStore();
        var i1 = store.GrantToCharacter(AliceId, DragonHornsIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Ear);
        var i2 = store.GrantToCharacter(AliceId, DragonTailIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Tail);
        Assert.True(i2.ItemUid > i1.ItemUid);
        Assert.Equal(InMemoryItemStore.StartingItemUid, i1.ItemUid);
    }

    [Fact]
    public void GrantToCharacter_DuplicateEquipSlot_Throws()
    {
        var store = new InMemoryItemStore();
        store.GrantToCharacter(AliceId, RookieSwordIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon);
        Assert.Throws<InvalidOperationException>(() =>
            store.GrantToCharacter(AliceId, RookieSwordIndex,
                itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon));
    }

    [Fact]
    public void GrantToCharacter_InventorySlotZeroAllowedMultiple()
    {
        var store = new InMemoryItemStore();
        store.GrantToCharacter(AliceId, CarrotItemIndex,
            itemClass: 1, slotKind: SlotKind.Inventory, slotPosition: 0, stack: 5);
        store.GrantToCharacter(AliceId, CarrotItemIndex,
            itemClass: 1, slotKind: SlotKind.Inventory, slotPosition: 0, stack: 3);
        Assert.Equal(2, store.GetForCharacterBySlotKind(AliceId, SlotKind.Inventory).Count);
    }

    [Fact]
    public void GetAllForCharacter_FiltersByOwner()
    {
        var store = new InMemoryItemStore();
        store.GrantToCharacter(AliceId, RookieSwordIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon);
        store.GrantToCharacter(BobId, RookieHatIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Hat);

        var alice = store.GetAllForCharacter(AliceId);
        var bob = store.GetAllForCharacter(BobId);
        Assert.Single(alice);
        Assert.Single(bob);
        Assert.Equal(RookieSwordIndex, alice[0].ItemIndex);
        Assert.Equal(RookieHatIndex, bob[0].ItemIndex);
    }

    [Fact]
    public void GetAllForCharacter_OrdersBySlotKindThenPosition()
    {
        var store = new InMemoryItemStore();
        store.GrantToCharacter(AliceId, CarrotItemIndex,
            itemClass: 1, slotKind: SlotKind.Inventory, slotPosition: 3);
        store.GrantToCharacter(AliceId, RookieHatIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Hat);
        store.GrantToCharacter(AliceId, RookieSwordIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon);

        var all = store.GetAllForCharacter(AliceId);
        Assert.Equal(RookieHatIndex, all[0].ItemIndex);
        Assert.Equal(RookieSwordIndex, all[1].ItemIndex);
        Assert.Equal(CarrotItemIndex, all[2].ItemIndex);
    }

    [Fact]
    public void FindBySlot_ReturnsItemOrNull()
    {
        var store = new InMemoryItemStore();
        var ear = store.GrantToCharacter(AliceId, DragonHornsIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Ear);
        Assert.Equal(ear.ItemUid,
            store.FindBySlot(AliceId, SlotKind.Equipment, EquipmentSlot.Ear)?.ItemUid);
        Assert.Null(store.FindBySlot(AliceId, SlotKind.Equipment, EquipmentSlot.Tail));
    }

    [Fact]
    public void MoveToSlot_RelocatesItem()
    {
        var store = new InMemoryItemStore();
        var sword = store.GrantToCharacter(AliceId, RookieSwordIndex,
            itemClass: 2, slotKind: SlotKind.Inventory, slotPosition: 5);

        Assert.True(store.MoveToSlot(sword.ItemUid, SlotKind.Equipment, EquipmentSlot.Weapon));
        var moved = store.FindByUid(sword.ItemUid);
        Assert.NotNull(moved);
        Assert.Equal(SlotKind.Equipment, moved.SlotKind);
        Assert.Equal(EquipmentSlot.Weapon, moved.SlotPosition);
        Assert.Null(store.FindBySlot(AliceId, SlotKind.Inventory, 5));
    }

    [Fact]
    public void MoveToSlot_OccupiedEquip_Throws()
    {
        var store = new InMemoryItemStore();
        var sword1 = store.GrantToCharacter(AliceId, RookieSwordIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon);
        var sword2 = store.GrantToCharacter(AliceId, RookieSwordIndex,
            itemClass: 2, slotKind: SlotKind.Inventory, slotPosition: 0);
        Assert.Throws<InvalidOperationException>(() =>
            store.MoveToSlot(sword2.ItemUid, SlotKind.Equipment, EquipmentSlot.Weapon));
    }

    [Fact]
    public void Delete_RemovesItem()
    {
        var store = new InMemoryItemStore();
        var item = store.GrantToCharacter(AliceId, CarrotItemIndex,
            itemClass: 1, slotKind: SlotKind.Inventory, slotPosition: 0);
        Assert.True(store.Delete(item.ItemUid));
        Assert.Null(store.FindByUid(item.ItemUid));
        Assert.False(store.Delete(item.ItemUid));
    }

    [Fact]
    public void AdjustStack_PositiveDelta_Adds()
    {
        var store = new InMemoryItemStore();
        var item = store.GrantToCharacter(AliceId, CarrotItemIndex,
            itemClass: 1, slotKind: SlotKind.Inventory, slotPosition: 0, stack: 5);
        Assert.Equal(8, store.AdjustStack(item.ItemUid, +3));
        Assert.Equal((ushort)8, store.FindByUid(item.ItemUid)!.Stack);
    }

    [Fact]
    public void AdjustStack_NegativeDeltaToZero_Deletes()
    {
        var store = new InMemoryItemStore();
        var item = store.GrantToCharacter(AliceId, CarrotItemIndex,
            itemClass: 1, slotKind: SlotKind.Inventory, slotPosition: 0, stack: 2);
        Assert.Equal(0, store.AdjustStack(item.ItemUid, -2));
        Assert.Null(store.FindByUid(item.ItemUid));
    }

    [Fact]
    public void AdjustStack_OverflowClampsToUshortMax()
    {
        var store = new InMemoryItemStore();
        var item = store.GrantToCharacter(AliceId, CarrotItemIndex,
            itemClass: 1, slotKind: SlotKind.Inventory, slotPosition: 0, stack: 100);
        Assert.Equal(ushort.MaxValue, store.AdjustStack(item.ItemUid, int.MaxValue));
    }

    [Fact]
    public void GetForCharacterBySlotKind_FiltersAndOrders()
    {
        var store = new InMemoryItemStore();
        store.GrantToCharacter(AliceId, DragonHornsIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Ear);
        store.GrantToCharacter(AliceId, DragonTailIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Tail);
        store.GrantToCharacter(AliceId, CarrotItemIndex,
            itemClass: 1, slotKind: SlotKind.Inventory, slotPosition: 0, stack: 2);

        var equip = store.GetForCharacterBySlotKind(AliceId, SlotKind.Equipment);
        Assert.Equal(2, equip.Count);
        Assert.Equal(EquipmentSlot.Ear, equip[0].SlotPosition);
        Assert.Equal(EquipmentSlot.Tail, equip[1].SlotPosition);

        var inv = store.GetForCharacterBySlotKind(AliceId, SlotKind.Inventory);
        Assert.Single(inv);
        Assert.Equal(CarrotItemIndex, inv[0].ItemIndex);
    }

    [Fact]
    public void Count_TracksTotalAcrossCharacters()
    {
        var store = new InMemoryItemStore();
        Assert.Equal(0, store.Count);
        store.GrantToCharacter(AliceId, RookieSwordIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Weapon);
        store.GrantToCharacter(BobId, RookieHatIndex,
            itemClass: 2, slotKind: SlotKind.Equipment, slotPosition: EquipmentSlot.Hat);
        Assert.Equal(2, store.Count);
    }
}
