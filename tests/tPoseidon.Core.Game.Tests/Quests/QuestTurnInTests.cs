using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Quests;

public sealed class QuestTurnInTests
{
    private const int Carrot = 11908;
    private const int Form = 11805;
    private const int Badge = 12000;

    private static readonly InMemoryItemParamLookup Items = new(new[]
    {
        new ItemParamEntry { Id = Carrot, Class = 1, Type = 14, Weight = 10 },
        new ItemParamEntry { Id = Form, Class = 1, Type = 14 },
        new ItemParamEntry { Id = Badge, Class = 1, Type = 14, Options = QuestTurnIn.SingleOnlyOption },
    });

    private static Item Held(ulong uid, int itemId, ushort stack) => new()
    {
        ItemUid = uid,
        ItemIndex = itemId,
        ItemClass = 1,
        Stack = stack,
        CharacterId = 1,
        SlotKind = SlotKind.Inventory,
        SlotPosition = (byte)uid,
    };

    [Fact]
    public void RequiredItemsAreTheFilledColumnsWithTheirCounts()
    {
        var quest = new QuestInfoEntry { Id = 1234, Name = "q", SrcItem0 = Carrot, Cnt0 = 2, SrcItem2 = Form, Cnt2 = 1 };

        Assert.Equal(
            new[] { new RequiredItem(Carrot, 2), new RequiredItem(Form, 1) },
            QuestTurnIn.Required(quest, Items));
    }

    [Fact]
    public void AnItemFlaggedSingleOnlyIsRequiredOnceWhateverTheCountColumnSays()
    {
        var quest = new QuestInfoEntry { Id = 1, Name = "q", SrcItem0 = Badge, Cnt0 = 0 };

        Assert.Equal(new[] { new RequiredItem(Badge, 1) }, QuestTurnIn.Required(quest, Items));
    }

    [Fact]
    public void OneBagItemMustHoldTheWholeCount()
    {
        var needed = new[] { new RequiredItem(Carrot, 2) };

        Assert.False(QuestTurnIn.HoldsAll(new[] { Held(1, Carrot, 1) }, needed));
        Assert.False(QuestTurnIn.HoldsAll(new[] { Held(1, Carrot, 1), Held(2, Carrot, 1) }, needed));
        Assert.True(QuestTurnIn.HoldsAll(new[] { Held(1, Carrot, 1), Held(2, Carrot, 3) }, needed));
        Assert.Equal(2UL, QuestTurnIn.Holding(new[] { Held(1, Carrot, 1), Held(2, Carrot, 3) }, needed[0])!.ItemUid);
    }

    [Fact]
    public void ACountBelowOneIsNeverHeldAndNoRequirementIsAlwaysHeld()
    {
        Assert.False(QuestTurnIn.HoldsAll(new[] { Held(1, Carrot, 5) }, new[] { new RequiredItem(Carrot, 0) }));
        Assert.True(QuestTurnIn.HoldsAll(Array.Empty<Item>(), Array.Empty<RequiredItem>()));
    }
}
