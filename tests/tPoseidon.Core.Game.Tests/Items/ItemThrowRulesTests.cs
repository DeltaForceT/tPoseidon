using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class ItemThrowRulesTests
{
    private static Item Held(byte itemClass, ushort stack, byte slotKind = SlotKind.Inventory) => new()
    {
        ItemUid = 213,
        ItemIndex = 8000,
        ItemClass = itemClass,
        Stack = stack,
        CharacterId = 7,
        SlotKind = slotKind,
        SlotPosition = 2,
    };

    [Fact]
    public void TheThrowBodyIsIdUidCountAndPosition()
    {
        var body = Convert.FromHexString("401F0000D500000001000000010035" + "01B801");

        Assert.True(ItemThrowRules.TryRead(body, out var request));

        Assert.Equal(new ItemThrow(8000, 0x1_0000_00D5, 1, 0x135, 0x1B8), request);
    }

    [Fact]
    public void ARareItemIsThrownWholeWithCountOne()
    {
        Assert.Equal(ThrowOutcome.WholeItem, ItemThrowRules.Judge(Held(Item.RareClass, 600), 7, 1, keptInBag: false));
        Assert.Equal(ThrowOutcome.Refused, ItemThrowRules.Judge(Held(Item.RareClass, 600), 7, 600, keptInBag: false));
    }

    [Fact]
    public void PartOfAStackCanBeThrown()
    {
        Assert.Equal(ThrowOutcome.PartOfStack, ItemThrowRules.Judge(Held(1, 10), 7, 4, keptInBag: false));
        Assert.Equal(ThrowOutcome.WholeItem, ItemThrowRules.Judge(Held(1, 10), 7, 10, keptInBag: false));
        Assert.Equal(ThrowOutcome.Refused, ItemThrowRules.Judge(Held(1, 10), 7, 11, keptInBag: false));
        Assert.Equal(ThrowOutcome.Refused, ItemThrowRules.Judge(Held(1, 10), 7, 0, keptInBag: false));
    }

    [Fact]
    public void WornItemsAndOtherPeoplesItemsAreRefused()
    {
        Assert.Equal(ThrowOutcome.Refused, ItemThrowRules.Judge(Held(Item.RareClass, 1, SlotKind.Equipment), 7, 1, keptInBag: false));
        Assert.Equal(ThrowOutcome.Refused, ItemThrowRules.Judge(Held(Item.RareClass, 1), 8, 1, keptInBag: false));
        Assert.Equal(ThrowOutcome.Refused, ItemThrowRules.Judge(null, 7, 1, keptInBag: false));
    }

    [Fact]
    public void ItemsKeptInTheBagAreIgnoredWithoutAReply()
    {
        Assert.Equal(ThrowOutcome.Ignored, ItemThrowRules.Judge(Held(Item.RareClass, 1), 7, 1, keptInBag: true));
    }

    [Fact]
    public void AThrowLandsWhereAimedWithinReachElseAtTheThrower()
    {
        Assert.Equal(((ushort)330, (ushort)440), ItemThrowRules.Landing(300, 400, 330, 440));
        Assert.Equal(((ushort)300, (ushort)400), ItemThrowRules.Landing(300, 400, 400, 400));
    }
}
