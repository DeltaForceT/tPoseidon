using tPoseidon.Core.Data;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class BankFramesTests
{
    private static Item Stored(ulong uid, int itemId, ushort stack, byte itemClass) => new()
    {
        ItemUid = uid,
        ItemIndex = itemId,
        ItemClass = itemClass,
        Stack = stack,
        CharacterId = 0,
        SlotKind = SlotKind.Warehouse,
        SlotPosition = 0,
    };

    [Fact]
    public void ContentsListMoneyEntriesAndTheRareItemOptionBlocks()
    {
        var payload = BankFrames.Contents(1234, new[] { (Stored(5, 2000, 3, 1), (byte)1), (Stored(6, 3030, 1, 2), (byte)2) });

        Assert.Equal(new byte[]
        {
            0x00,
            0xD2, 0x04, 0x00, 0x00,
            0x02, 0x00,
            0x01, 0xD0, 0x07, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00,
            0x02, 0xD6, 0x0B, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x06, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
        }, payload);
    }

    [Fact]
    public void AnAlreadyLoadedBankIsOneByte()
    {
        Assert.Equal(new byte[] { 0x01 }, BankFrames.AlreadyLoaded());
    }

    [Fact]
    public void AnItemRequestIsUidItemAndCount()
    {
        var payload = new byte[] { 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xD0, 0x07, 0x00, 0x00, 0x02, 0x00 };

        Assert.True(BankFrames.TryReadItemRequest(payload, out var uid, out var itemId, out var count));
        Assert.Equal((0x0000_0001_0000_0005UL, 2000, 2), (uid, itemId, count));
    }

    [Fact]
    public void TheMoneyAnswerCarriesBankThenCarried()
    {
        Assert.Equal(new byte[] { 0x64, 0x00, 0x00, 0x00, 0xC8, 0x00, 0x00, 0x00 }, BankFrames.Money(100, 200));
    }

    [Fact]
    public void AMovePairsTheBagAndBankRecords()
    {
        var bag = new BagOperation(7, 1, 2000, 0x0000_0001_0000_0005UL, 0);
        var bank = new BagOperation(8, 1, 2000, 0x0000_0001_0000_0005UL, 3);

        var payload = BankFrames.Moved(bag, bank);

        Assert.Equal(QuestResultFrames.ItemOperation(bag).Concat(QuestResultFrames.ItemOperation(bank)), payload);
        Assert.Equal(7, payload[0]);
        Assert.Equal(8, payload[16]);
    }
}
