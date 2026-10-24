using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class WarehouseFramesTests
{
    private static Item Stored(ulong uid, int itemId, ushort stack, byte itemClass) => new()
    {
        ItemUid = uid,
        ItemIndex = itemId,
        ItemClass = itemClass,
        Stack = stack,
        CharacterId = 1,
        SlotKind = 2,
        SlotPosition = 0,
    };

    [Fact]
    public void ContentsStartWithTheTabThenTheBankEntryLayout()
    {
        var payload = WarehouseFrames.Contents(2, new[] { (Stored(5, 2000, 3, 1), (byte)1) });

        Assert.Equal(new byte[]
        {
            0x02, 0x00,
            0x01, 0x00,
            0x01, 0xD0, 0x07, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00,
            0x00, 0x00,
        }, payload);
    }

    [Fact]
    public void AnAlreadyLoadedTabIsTheTabAndOne()
    {
        Assert.Equal(new byte[] { 0x02, 0x01 }, WarehouseFrames.AlreadyLoaded(2));
    }

    [Fact]
    public void AnItemRequestIsTabUidItemAndCount()
    {
        var payload = new byte[] { 0x15, 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xD0, 0x07, 0x00, 0x00, 0x02, 0x00 };

        Assert.True(WarehouseFrames.TryReadItemRequest(payload, out var tab, out var uid, out var itemId, out var count));
        Assert.Equal(((byte)0x15, 0x0000_0001_0000_0005UL, 2000, 2), (tab, uid, itemId, count));
    }

    [Fact]
    public void AMoveRequestIsFromToUidItemAndCount()
    {
        var payload = new byte[] { 0x02, 0x15, 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xD0, 0x07, 0x00, 0x00, 0x04, 0x00 };

        Assert.True(WarehouseFrames.TryReadMoveRequest(payload, out var from, out var to, out var uid, out var count));
        Assert.Equal(((byte)0x02, (byte)0x15, 0x0000_0001_0000_0005UL, 4), (from, to, uid, count));
    }

    [Fact]
    public void ShortRequestsAreRejected()
    {
        Assert.False(WarehouseFrames.TryReadItemRequest(new byte[14], out _, out _, out _, out _));
        Assert.False(WarehouseFrames.TryReadMoveRequest(new byte[15], out _, out _, out _, out _));
    }

    [Fact]
    public void AMovedAnswerIsTheTabThenTheBagAndStorageRecords()
    {
        var bag = new BagOperation(7, 1, 2000, 0x0000_0001_0000_0005UL, 0);
        var stored = new BagOperation(8, 1, 2000, 0x0000_0001_0000_0005UL, 3);

        var payload = WarehouseFrames.Moved(2, bag, stored);

        Assert.Equal(0x02, payload[0]);
        Assert.Equal(BankFrames.Moved(bag, stored), payload[1..]);
    }

    [Fact]
    public void AnExchangeCarriesEachTabBeforeItsRecord()
    {
        var from = new BagOperation(7, 1, 2000, 0x0000_0001_0000_0005UL, 0);
        var to = new BagOperation(8, 1, 2000, 0x0000_0001_0000_0005UL, 3);

        var payload = WarehouseFrames.Exchanged(2, from, 21, to);
        var record = QuestResultFrames.ItemOperation(from);

        Assert.Equal(0x02, payload[0]);
        Assert.Equal(0x15, payload[1 + record.Length]);
        Assert.Equal(2 + record.Length + QuestResultFrames.ItemOperation(to).Length, payload.Length);
    }

    [Fact]
    public void ARefusalIsTheTab()
    {
        Assert.Equal(new byte[] { 0x16 }, WarehouseFrames.Refused(0x16));
    }

    [Fact]
    public void EightySlotsAreTabTwo()
    {
        Assert.Equal(new[] { new WarehouseTab(2, 80) }, WarehouseService.Tabs(80));
    }

    [Fact]
    public void SlotsBeyondEightyOpenTheNextTab()
    {
        Assert.Equal(new[] { new WarehouseTab(2, 80), new WarehouseTab(21, 8) }, WarehouseService.Tabs(88));
    }

    [Fact]
    public void EightHundredSlotsFillAllTenTabs()
    {
        var tabs = WarehouseService.Tabs(800);

        Assert.Equal(10, tabs.Count);
        Assert.Equal(29, tabs[^1].Id);
        Assert.All(tabs, tab => Assert.Equal(80, tab.Slots));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(801)]
    public void SlotCountsOutsideOneToEightHundredGiveNoTabs(int slots)
    {
        Assert.Empty(WarehouseService.Tabs(slots));
    }
}

public sealed class StoredItemOptionTests
{
    [Fact]
    public void EquipmentEntriesCarryTheirFullPropertyList()
    {
        var sword = new tPoseidon.Core.Data.Item { ItemUid = 6, ItemIndex = 3030, ItemClass = 2, Stack = 1, CharacterId = 1, SlotKind = 2, SlotPosition = 0 };
        var option = new tPoseidon.Core.Data.ItemProperty(tPoseidon.Core.Data.ItemPropertyTypes.Stat, 10000, 100, 0);

        var payload = tPoseidon.Core.Net.World.WarehouseFrames.Contents(2, new[] { (sword, (byte)2) }, _ => new[] { option });

        Assert.Equal(new byte[] { 0x06, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x0A, 0x00, 0x00, 0x00, 0x10, 0x27, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00 }, payload[^21..]);
    }
}
