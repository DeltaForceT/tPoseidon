using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class CashBoxFramesTests
{
    private static readonly ChargedItem Potion = new(0x10, 1, 1001, 5, 0, 0);
    private static readonly ChargedItem Drill = new(0x11, 3, 4500, 600, 72, 1);

    [Fact]
    public void TheHeaderPageCarriesTheEntryTotal()
    {
        Assert.Equal(new byte[] { 0, 0, 0, 3, 0 }, CashBoxFrames.Header(3));
    }

    [Fact]
    public void AnEmptyBoxSendsOneLastPage()
    {
        Assert.Equal(new byte[] { 2, 0, 0 }, Assert.Single(CashBoxFrames.Pages(Array.Empty<ChargedItem>())));
    }

    [Fact]
    public void EntriesCarryTheItemThenHoursThenFlag()
    {
        var page = Assert.Single(CashBoxFrames.Pages(new[] { Potion, Drill }));

        Assert.Equal(new byte[] { 2, 2, 0 }, page[..3]);
        Assert.Equal(3 + 18 + 22, page.Length);
        Assert.Equal(1, page[3]);
        Assert.Equal(1001u, BitConverter.ToUInt32(page, 4));
        Assert.Equal(BagItemIds.OnWire(0x10), BitConverter.ToUInt64(page, 8));
        Assert.Equal(5, BitConverter.ToUInt16(page, 16));
        Assert.Equal(0, BitConverter.ToUInt16(page, 18));
        Assert.Equal(2, page[21]);
        Assert.Equal(600, BitConverter.ToUInt16(page, 34));
        Assert.Equal(72, BitConverter.ToUInt16(page, 40));
        Assert.Equal(1, page[42]);
    }

    [Fact]
    public void PagesStopWhileLessThanTheReserveIsFree()
    {
        var items = Enumerable.Range(0, 60).Select(i => Potion with { ItemUid = (ulong)i + 1 }).ToList();

        var pages = CashBoxFrames.Pages(items);

        Assert.Equal(2, pages.Count);
        Assert.Equal(1, pages[0][0]);
        Assert.Equal(55, BitConverter.ToUInt16(pages[0], 1));
        Assert.Equal(2, pages[1][0]);
        Assert.Equal(5, BitConverter.ToUInt16(pages[1], 1));
    }

    [Fact]
    public void ATimedTakeEndsWithTheTimerRecord()
    {
        var bagSide = new BagOperation(QuestResultFrames.Created, 2, 4500, BagItemIds.OnWire(0x11), 600);
        var boxSide = new BagOperation(QuestResultFrames.Removed, 2, 4500, BagItemIds.OnWire(0x11), 600);
        var until = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

        var body = CashBoxFrames.Taken(bagSide, boxSide, until);
        var record = body[^CashBoxFrames.TimedRecordSize..];

        Assert.Equal(QuestResultFrames.ItemOperation(bagSide).Length + QuestResultFrames.ItemOperation(boxSide).Length + CashBoxFrames.TimedRecordSize, body.Length);
        Assert.Equal(BagItemIds.OnWire(0x11), BitConverter.ToUInt64(record, 0));
        Assert.Equal(4500u, BitConverter.ToUInt32(record, 8));
        Assert.Equal(ClientClock.FileTime(until), BitConverter.ToUInt64(record, 12));
        Assert.Equal(0, record[20]);
    }

    [Fact]
    public void AnUntimedTakeSendsAnEmptyTimerRecord()
    {
        var bagSide = new BagOperation(QuestResultFrames.StackChanged, 1, 1001, BagItemIds.OnWire(0x20), 15);
        var boxSide = new BagOperation(QuestResultFrames.Removed, 1, 1001, BagItemIds.OnWire(0x10), 5);

        var body = CashBoxFrames.Taken(bagSide, boxSide, null);

        Assert.All(body[^CashBoxFrames.TimedRecordSize..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void TheTakeRequestReadsUidItemAndCount()
    {
        var body = new byte[14];
        BitConverter.TryWriteBytes(body.AsSpan(0), BagItemIds.OnWire(0x11));
        BitConverter.TryWriteBytes(body.AsSpan(8), 4500u);
        BitConverter.TryWriteBytes(body.AsSpan(12), (ushort)600);

        Assert.True(CashBoxFrames.TryReadTake(body, out var take));
        Assert.Equal(new CashBoxTake(BagItemIds.OnWire(0x11), 4500, 600), take);
    }

    [Fact]
    public void MasterMessagesRoundTrip()
    {
        Assert.Equal(new[] { Potion, Drill }, CashBoxMessages.DecodeRows(CashBoxMessages.EncodeRows(new[] { Potion, Drill })));
        var move = new ChargedItemMove(7, 0x11, 9, true);
        Assert.Equal(move, CashBoxMessages.DecodeMove(9, CashBoxMessages.EncodeMove(move)));
        var moved = new ChargedItemMoved(0x11, false, 2, 4500, 600, 600, new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(moved, CashBoxMessages.DecodeMoved(CashBoxMessages.EncodeMoved(moved)));
    }
}
