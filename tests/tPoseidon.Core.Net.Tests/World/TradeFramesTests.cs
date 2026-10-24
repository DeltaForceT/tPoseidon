using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class TradeFramesTests
{
    [Fact]
    public void TheOfferReadsFlagMoneyAndAtMostSixSlots()
    {
        var w = new PacketWriter(128);
        w.WriteByte(1);
        w.WriteUInt32(5000);
        w.WriteUInt16(7);
        for (var index = 0; index < 7; index++)
        {
            w.WriteUInt64((ulong)(index + 1));
            w.WriteUInt32((uint)(100 + index));
            w.WriteUInt32((uint)(index + 1));
        }

        Assert.True(TradeFrames.TryReadUpdate(w.ToArray(), out var ready, out var money, out var slots));
        Assert.True(ready);
        Assert.Equal(5000u, money);
        Assert.Equal(TradeFrames.SlotsPerSide, slots.Count);
        Assert.Equal(new TradeOfferSlot(6, 105, 6), slots[5]);
        Assert.False(TradeFrames.TryReadUpdate(new byte[3], out _, out _, out _));
    }

    [Fact]
    public void TheUpdateWritesBothRecordsWithEmptySlotsAsZero()
    {
        var common = new TradeItemView(1, 900, 0x10, 3, 0, 0, 0, 0);
        var rare = new TradeItemView(2, 20001, 0x11, 1, 1, 7, 2, 3);
        var own = new TradeRecordView(true, 10, new TradeItemView?[] { common, null, null, null, null, null });
        var opposite = new TradeRecordView(false, 0, new TradeItemView?[] { null, rare });

        var bytes = TradeFrames.Update(own, 2, opposite, 0, TradeFrames.CodeMoneyInvalid);

        var commonRecord = 1 + 4 + 8 + 2;
        var ownSize = 1 + 4 + 2 + commonRecord + 5;
        var oppositeSize = 1 + 4 + 2 + 1 + (commonRecord + 4) + 4;
        Assert.Equal(ownSize + 2 + oppositeSize + 2 + 2 + 2, bytes.Length);
        Assert.Equal(1, bytes[0]);
        Assert.Equal(1, bytes[7]);
        Assert.Equal(2, BitConverter.ToUInt16(bytes, ownSize));
        Assert.Equal(TradeFrames.CodeMoneyInvalid, BitConverter.ToUInt16(bytes, bytes.Length - 2));
        var rareAt = ownSize + 2 + 1 + 4 + 2 + 1;
        Assert.Equal(2, bytes[rareAt]);
        Assert.Equal(new byte[] { 1, 7, 2, 3 }, bytes[(rareAt + commonRecord)..(rareAt + commonRecord + 4)]);
    }

    [Fact]
    public void TheInviteAndSettledFramesCarryTheirFields()
    {
        var invite = TradeFrames.Invite(0x1234, "Tata", Encoding.ASCII);
        Assert.Equal(new byte[] { 0x34, 0x12, (byte)'T', (byte)'a', (byte)'t', (byte)'a', 0 }, invite);

        var settled = TradeFrames.Settled(777, new[] { new BagOperation(QuestResultFrames.Removed, 1, 900, 0x10, 3) });
        Assert.Equal(777u, BitConverter.ToUInt32(settled, 0));
        Assert.Equal(1, BitConverter.ToUInt16(settled, 4));
        Assert.Equal(QuestResultFrames.Removed, settled[6]);
    }

    [Fact]
    public void TheMasterMessagesRoundTrip()
    {
        var first = new TradeOffer(1, 100, new[] { new TradeSlot(11, 900, 3) });
        var second = new TradeOffer(2, 0, Array.Empty<TradeSlot>());
        var (readFirst, readSecond, capacity) = TradeMessages.DecodeTrade(TradeMessages.EncodeTrade(first, second, 50));
        Assert.Equal(first.CharacterId, readFirst.CharacterId);
        Assert.Equal(first.Slots, readFirst.Slots);
        Assert.Empty(readSecond.Slots);
        Assert.Equal(50, capacity);

        var mine = new TradeSideResult(1, 0, new[] { new TradeItemChange(TradeChangeKind.Removed, 1, 900, 11, 3, 0) });
        var theirs = new TradeSideResult(2, 100, new[] { new TradeItemChange(TradeChangeKind.Created, 1, 900, 12, 3, 0) });
        var result = TradeMessages.DecodeResult(TradeMessages.EncodeResult(mine, theirs));
        Assert.True(result.Succeeded);
        Assert.Equal(mine.Changes, result.First!.Changes);
        Assert.Equal(100u, result.Second!.Money);
        Assert.Equal(TradeRules.BagFull, TradeMessages.DecodeError(TradeMessages.EncodeError(TradeRules.BagFull)));
    }

    [Fact]
    public void SettledMoneyMustStayInRange()
    {
        Assert.True(TradeRules.MoneySettles(100, 100, 0));
        Assert.False(TradeRules.MoneySettles(100, 101, 0));
        Assert.False(TradeRules.MoneySettles(TradeRules.MaxSettledMoney, 0, 1));
    }
}
