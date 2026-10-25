using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class SpecialBoxUseTests
{
    private const ushort Refused = 0x005A;
    private const ushort Effect = 0x0097;
    private const ushort Opened = 0x024E;
    private const ushort PresentRefused = 0x024F;
    private const int BoxId = 431762;
    private const int SelectId = 71915;
    private const int PickBoxId = 431763;
    private const int UnpickBoxId = 431764;
    private static readonly int[] UnpickedPrizes = [101, 441093];
    private static readonly int[] Prizes = [SelectId, 441093, 19832, 101];

    private static PresentItemEntry Box(int id, params int[] items) =>
        new(id, tPoseidon.Core.Game.Items.PresentRolls.WeightedType, 1, items.Select(item => new PresentDrop(item, 100, 1)).ToArray());

    private static SpecialItemHarness Harness(int effect, Action<SpecialItemHarness>? arrange = null)
    {
        var harness = new SpecialItemHarness();
        harness.Define(new ItemParamEntry { Id = BoxId, Type = 15, Effect = effect, Options = ItemParamEntry.UsableOption, Class = 1 });
        foreach (var item in Prizes) harness.Define(new ItemParamEntry { Id = item, Type = 14, Class = 1 });
        arrange?.Invoke(harness);
        return harness;
    }

    [Fact]
    public async Task ARandomBoxHoldingTheSelectedItemAlsoGivesThePickBonus()
    {
        var harness = Harness(47, h =>
        {
            h.Presents[BoxId] = Box(BoxId, SelectId);
            h.Presents[PickBoxId] = Box(PickBoxId, 19832);
            h.Presents[UnpickBoxId] = Box(UnpickBoxId, 101);
            h.Bonuses[BoxId] = new BonusPresentEntry(BoxId, SelectId, PickBoxId, UnpickBoxId);
        });
        var box = harness.Give(BoxId);
        harness.Build();

        await harness.UseAsync(box);

        Assert.Equal(new[] { Effect, Opened }, harness.SentWords);
        var held = harness.Items.GetForCharacterBySlotKind(SpecialItemHarness.CharacterId, SlotKind.Inventory).Select(item => item.ItemIndex).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { 19832, SelectId }, held);
    }

    [Fact]
    public async Task ARandomBoxWithoutTheSelectedItemGivesTheUnpickBonus()
    {
        var harness = Harness(47, h =>
        {
            h.Presents[BoxId] = Box(BoxId, 441093);
            h.Presents[PickBoxId] = Box(PickBoxId, 19832);
            h.Presents[UnpickBoxId] = Box(UnpickBoxId, 101);
            h.Bonuses[BoxId] = new BonusPresentEntry(BoxId, SelectId, PickBoxId, UnpickBoxId);
        });
        var box = harness.Give(BoxId);
        harness.Build();

        await harness.UseAsync(box);

        var held = harness.Items.GetForCharacterBySlotKind(SpecialItemHarness.CharacterId, SlotKind.Inventory).Select(item => item.ItemIndex).OrderBy(id => id).ToArray();
        Assert.Equal(UnpickedPrizes, held);
    }

    [Fact]
    public async Task ARandomBoxWithoutARowIsRefusedAndKept()
    {
        var harness = Harness(47);
        var box = harness.Give(BoxId);
        harness.Build();

        await harness.UseAsync(box);

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(box));
    }

    [Fact]
    public async Task AnExpPotionStartsItsBoostAndIsConsumed()
    {
        var harness = Harness(51);
        var potion = harness.Give(BoxId);
        harness.Build();

        await harness.UseAsync(potion);

        Assert.Equal(new[] { Effect }, harness.SentWords);
        Assert.Equal((BoxId, 0), Assert.Single(harness.State.Potions));
        Assert.False(harness.InBag(potion));
    }

    [Fact]
    public async Task AnExpPotionWithoutARowIsRefusedAndKept()
    {
        var harness = Harness(51, h => h.State.PotionKnown = false);
        var potion = harness.Give(BoxId);
        harness.Build();

        await harness.UseAsync(potion);

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(potion));
    }

    [Fact]
    public async Task AnImprintIsRegisteredAndConsumed()
    {
        var harness = Harness(52);
        var imprint = harness.Give(BoxId);
        harness.Build();

        await harness.UseAsync(imprint);

        Assert.Equal(new[] { Effect }, harness.SentWords);
        Assert.Equal(BoxId, Assert.Single(harness.State.Imprints));
        Assert.False(harness.InBag(imprint));
    }

    [Fact]
    public async Task AnImprintWithoutARowIsRefusedAndKept()
    {
        var harness = Harness(52, h => h.State.ImprintKnown = false);
        var imprint = harness.Give(BoxId);
        harness.Build();

        await harness.UseAsync(imprint);

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(imprint));
    }

    [Fact]
    public async Task APetPiercingIsRefusedAndKeptBecauseThePetWindowAppliesIt()
    {
        var harness = Harness(48);
        var piercing = harness.Give(BoxId);
        harness.Build();

        await harness.UseAsync(piercing);

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(piercing));
    }
}
