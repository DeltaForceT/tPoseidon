using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ReleaseListRulesTests
{
    private static readonly ItemParamEntry Hat = new() { Id = 100, Class = 2, Type = 3, Options = EquipmentRules.EquipableOption };
    private static readonly ItemParamEntry Ears = new() { Id = 101, Class = 2, Type = EquipmentRules.EarsType, Options = EquipmentRules.EquipableOption };
    private static readonly ItemParamEntry Bullet = new() { Id = 200, Class = 1, Type = EquipmentRules.BulletType };

    private static WornItem Worn(byte slot, ulong uid, ItemParamEntry definition, byte itemClass) =>
        new(slot, new Item { ItemUid = uid, ItemIndex = definition.Id, ItemClass = itemClass, CharacterId = 7, SlotKind = SlotKind.Equipment, SlotPosition = slot }, definition);

    private static ulong Wire(ulong uid) => BagItemIds.OnWire(uid);

    [Fact]
    public void EveryListedWornItemIsPlannedInOrder()
    {
        var worn = new[] { Worn(EquipmentSlot.Hat, 5, Hat, 2), Worn(EquipmentSlot.Common, 6, Bullet, 1) };
        var entries = new[]
        {
            new ReleaseEntry(EquipmentFrames.CommonItemClass, Wire(6), EquipmentSlot.Common),
            new ReleaseEntry(EquipmentFrames.RareItemClass, Wire(5), EquipmentSlot.Hat),
        };

        var plan = ReleaseListRules.Plan(entries, worn, 50);

        Assert.Null(plan.Refusal);
        Assert.Equal(new ulong[] { 6, 5 }, plan.Items.Select(item => item.Item.ItemUid));
    }

    [Fact]
    public void AnEmptyListPlansNothing()
    {
        var plan = ReleaseListRules.Plan(Array.Empty<ReleaseEntry>(), Array.Empty<WornItem>(), 1);

        Assert.Null(plan.Refusal);
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void AnItemThatIsNotWornRefusesTheWholeList()
    {
        var worn = new[] { Worn(EquipmentSlot.Hat, 5, Hat, 2) };
        var entries = new[]
        {
            new ReleaseEntry(EquipmentFrames.RareItemClass, Wire(5), EquipmentSlot.Hat),
            new ReleaseEntry(EquipmentFrames.RareItemClass, Wire(9), EquipmentSlot.Weapon),
        };

        var plan = ReleaseListRules.Plan(entries, worn, 50);

        Assert.Equal("item 0x9: the item is not worn", plan.Refusal);
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void EarsBelowTheReleaseLevelRefuseTheWholeList()
    {
        var worn = new[] { Worn(EquipmentSlot.Hat, 5, Hat, 2), Worn(EquipmentSlot.Ear, 7, Ears, 2) };
        var entries = new[]
        {
            new ReleaseEntry(EquipmentFrames.RareItemClass, Wire(5), EquipmentSlot.Hat),
            new ReleaseEntry(EquipmentFrames.RareItemClass, Wire(7), EquipmentSlot.Ear),
        };

        var plan = ReleaseListRules.Plan(entries, worn, EquipmentRules.EarsAndTailReleaseLevel - 1);

        Assert.Equal($"item 0x7: {ReleaseRefusal.EarsOrTailBelowLevel}", plan.Refusal);
    }

    [Fact]
    public void ASlotThatHoldsAnotherItemRefusesTheList()
    {
        var worn = new[] { Worn(EquipmentSlot.Hat, 5, Hat, 2), Worn(EquipmentSlot.Weapon, 6, Hat, 2) };
        var entries = new[] { new ReleaseEntry(EquipmentFrames.RareItemClass, Wire(5), EquipmentSlot.Weapon) };

        Assert.Equal($"item 0x5: {ReleaseRefusal.SlotHoldsAnotherItem}", ReleaseListRules.Plan(entries, worn, 50).Refusal);
    }

    [Fact]
    public void ACommonItemThatIsNotLoadedRefusesTheList()
    {
        var worn = new[] { Worn(EquipmentSlot.Hat, 5, Hat, 2) };
        var entries = new[] { new ReleaseEntry(EquipmentFrames.CommonItemClass, Wire(5), EquipmentSlot.Common) };

        Assert.Equal("item 0x5: the item is not loaded", ReleaseListRules.Plan(entries, worn, 50).Refusal);
    }

    [Fact]
    public void ADuplicatedItemRefusesTheList()
    {
        var worn = new[] { Worn(EquipmentSlot.Hat, 5, Hat, 2) };
        var entry = new ReleaseEntry(EquipmentFrames.RareItemClass, Wire(5), EquipmentSlot.Hat);

        Assert.Equal("item 0x5: the item is listed twice", ReleaseListRules.Plan(new[] { entry, entry }, worn, 50).Refusal);
    }

    [Fact]
    public void AnUnknownClassRefusesTheList()
    {
        var worn = new[] { Worn(EquipmentSlot.Hat, 5, Hat, 2) };
        var entries = new[] { new ReleaseEntry(3, Wire(5), EquipmentSlot.Hat) };

        Assert.Equal("item 0x5: class 3 is neither common nor rare", ReleaseListRules.Plan(entries, worn, 50).Refusal);
    }
}
