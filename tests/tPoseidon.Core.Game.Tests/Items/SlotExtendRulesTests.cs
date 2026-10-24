using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class SlotExtendRulesTests
{
    private static readonly SlotExtendMaterialEntry NateElixir = new(60766, new[] { 50f, 10f, 2f, 0.5f, 0f }, 2 | 4);
    private static readonly SlotExtendMaterialEntry NateSpecialElixir = new(60767, new[] { 70f, 20f, 5f, 1f, 0.01f }, 1 | 2 | 4);
    private static readonly SlotExtendMaterialEntry TestElixirThree = new(410020, new[] { 100f, 100f, 100f, 100f, 100f }, 1);
    private static readonly SlotExtendSubItemEntry LuckyElixir = new(19866, 0, new[] { 30f, 10f, 2f, 1f, 0.04f });
    private static readonly SlotExtendSubItemEntry GreatLuckElixir = new(19867, 0, new[] { 30f, 20f, 4f, 2f, 0.09f });
    private static readonly SlotExtendSubItemEntry Guard = new(19868, 1, new[] { 0f, 0f, 0f, 0f, 0f });

    private static ItemParamEntry Equipment(int optionsEx = 0, int compoundSlot = 1, int itemClass = 2) =>
        new() { Id = 40001, Class = itemClass, CompoundSlot = compoundSlot, ExtendedOptions = optionsEx };

    private static byte Check(ItemParamEntry? equipment, byte slots, SlotExtendMaterialEntry? material, params SlotExtendSubItemEntry?[] subs) =>
        SlotExtendRules.Check(new SlotExtendCandidate(equipment, slots, material, subs));

    [Fact]
    public void EquipmentChecksComeFirst()
    {
        Assert.Equal(SlotExtendRules.NoEquipment, Check(null, 0, NateElixir));
        Assert.Equal(SlotExtendRules.InvalidEquipment, Check(Equipment(itemClass: 1), 0, NateElixir));
        Assert.Equal(SlotExtendRules.InvalidEquipment, Check(Equipment(compoundSlot: 0), 0, NateElixir));
        Assert.Equal(SlotExtendRules.AtMaxSlots, Check(Equipment(optionsEx: 2048), 5, NateSpecialElixir));
        Assert.Equal(SlotExtendRules.NoFifthSlot, Check(Equipment(), 4, NateSpecialElixir));
        Assert.Equal(SlotExtendRules.Ok, Check(Equipment(optionsEx: 2048), 4, NateSpecialElixir));
    }

    [Fact]
    public void MaterialFlagsGateTheFifthSlotAndTheHelpers()
    {
        Assert.Equal(SlotExtendRules.NoMaterial, Check(Equipment(), 0, null));
        Assert.Equal(SlotExtendRules.MaterialNoFifthSlot, Check(Equipment(optionsEx: 2048), 4, NateElixir));
        Assert.Equal(SlotExtendRules.NoLuckElixir, Check(Equipment(), 0, TestElixirThree, LuckyElixir));
        Assert.Equal(SlotExtendRules.NoGuard, Check(Equipment(), 0, TestElixirThree, Guard));
        Assert.Equal(SlotExtendRules.Ok, Check(Equipment(), 0, NateElixir, LuckyElixir, Guard));
    }

    [Fact]
    public void SubItemsAreOnePerKind()
    {
        Assert.Equal(SlotExtendRules.NoFirstSubItem, Check(Equipment(), 0, NateElixir, (SlotExtendSubItemEntry?)null));
        Assert.Equal(SlotExtendRules.NoSecondSubItem, Check(Equipment(), 0, NateElixir, LuckyElixir, null));
        Assert.Equal(SlotExtendRules.SameSubItemKind, Check(Equipment(), 0, NateElixir, LuckyElixir, GreatLuckElixir));
        Assert.Equal(SlotExtendRules.SameSubItemKind, Check(Equipment(), 0, NateElixir, LuckyElixir, Guard, Guard));
    }

    [Fact]
    public void ChanceIsTheMaterialRatioPlusTheElixir()
    {
        Assert.Equal(50.0, SlotExtendRules.Chance(NateElixir, Array.Empty<SlotExtendSubItemEntry>(), 1), 3);
        Assert.Equal(80.0, SlotExtendRules.Chance(NateElixir, new[] { LuckyElixir, Guard }, 1), 3);
        Assert.Equal(0.1, SlotExtendRules.Chance(NateSpecialElixir, new[] { GreatLuckElixir }, 5), 3);
        Assert.True(SlotExtendRules.Succeeds(80.0, 0.79));
        Assert.False(SlotExtendRules.Succeeds(80.0, 0.80));
        Assert.True(SlotExtendRules.Guarded(new[] { Guard }));
        Assert.False(SlotExtendRules.Guarded(new[] { LuckyElixir }));
    }
}
