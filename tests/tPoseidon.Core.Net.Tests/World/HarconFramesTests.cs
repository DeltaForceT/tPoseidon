using tPoseidon.Core.Data;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class HarconFramesTests
{
    [Fact]
    public void TotalRecordPutsTheCountersAndStagesAtTheirOffsets()
    {
        var frame = HarconFrames.TotalRecord(new HarconDefTotals(7, 5, 2, [(3, 4), (11, 1)]));

        Assert.Equal(HarconFrames.TotalRecordSize, frame.Length);
        Assert.Equal(7u, BitConverter.ToUInt32(frame, 0x00));
        Assert.Equal((ushort)5, BitConverter.ToUInt16(frame, 0x04));
        Assert.Equal((ushort)2, BitConverter.ToUInt16(frame, 0x06));
        Assert.Equal((ushort)3, BitConverter.ToUInt16(frame, 0x12));
        Assert.Equal((ushort)4, BitConverter.ToUInt16(frame, 0x14));
        Assert.Equal((ushort)11, BitConverter.ToUInt16(frame, 0x16));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(frame, 0x18));
        Assert.Equal((ushort)0, BitConverter.ToUInt16(frame, 0x1A));
        Assert.All(frame[0x1E..], value => Assert.Equal(0, value));
    }

    [Fact]
    public void StageRecordPutsEachHarconRecordInfoValueAtTheOffsetTheClientReads()
    {
        var frame = HarconFrames.StageRecord(new HarconStageRecord(5, 4, 3, 1, 900, 200f, 400, 2, 62.5f, 3, 120, 6));
        var record = frame.AsSpan(2);

        Assert.Equal(2 + HarconFrames.StageRecordSize, frame.Length);
        Assert.Equal((ushort)5, BitConverter.ToUInt16(frame, 0));
        Assert.Equal((ushort)4, BitConverter.ToUInt16(record[0x00..]));
        Assert.Equal((ushort)3, BitConverter.ToUInt16(record[0x02..]));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(record[0x04..]));
        Assert.Equal(900u, BitConverter.ToUInt32(record[0x0C..]));
        Assert.Equal(50f, BitConverter.ToSingle(record[0x10..]));
        Assert.Equal(2, record[0x18]);
        Assert.Equal(62.5f, BitConverter.ToSingle(record[0x19..]));
        Assert.Equal((ushort)3, BitConverter.ToUInt16(record[0x1D..]));
        Assert.Equal(120u, BitConverter.ToUInt32(record[0x2F..]));
        Assert.Equal((ushort)6, BitConverter.ToUInt16(record[0x33..]));
        Assert.All(record[0x1F..0x2F].ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void RoomKeyStageAndNameRequestsAreRead()
    {
        Assert.True(HarconFrames.TryReadRoomKey(new byte[] { 0x2A, 0, 0, 0 }, out var key));
        Assert.Equal(42u, key);
        Assert.False(HarconFrames.TryReadRoomKey(new byte[] { 1, 0 }, out _));
        Assert.True(HarconFrames.TryReadStage(new byte[] { 7, 0 }, out var stage));
        Assert.Equal((ushort)7, stage);
        Assert.True(HarconFrames.TryReadName("Bob\0"u8, System.Text.Encoding.ASCII, out var name));
        Assert.Equal("Bob", name);
        Assert.False(HarconFrames.TryReadName("\0"u8, System.Text.Encoding.ASCII, out _));
    }

    [Fact]
    public void EmptyTotalRecordIsAllZero()
    {
        Assert.All(HarconFrames.TotalRecord(HarconDefTotals.Empty), value => Assert.Equal(0, value));
    }

    [Fact]
    public void GaugesAreEntityIdAndValuePairsCappedAtSix()
    {
        var frame = HarconFrames.Gauges([(0x1234, 5000), (0x0042, 0)]);

        Assert.Equal(new byte[] { 2, 0x34, 0x12, 0x88, 0x13, 0, 0, 0x42, 0, 0, 0, 0, 0 }, frame);
        var many = Enumerable.Range(1, 8).Select(id => ((ushort)id, 100)).ToArray();
        Assert.Equal(HarconFrames.MaxGauges, HarconFrames.Gauges(many)[0]);
    }

    [Fact]
    public void GameStartCarriesTheGaugeListBeforeTheItemChange()
    {
        var members = new[] { new tPoseidon.Core.Game.HarconDefence.HarconMember(1, 0x0101, "A") };
        var frame = HarconFrames.GameStart(members, 0, [(0x0201, 9000)], [7, 7], System.Text.Encoding.ASCII);

        Assert.Equal(new byte[] { 0, 1, 0x01, 0x01, (byte)'A', 0, 1, 0x01, 0x02, 0x28, 0x23, 0, 0, 7, 7 }, frame);
    }

    [Fact]
    public void TheCoinShopOpensWithTheNpcTypeAndTheBuyCarriesTheNpcTypeAndTheItem()
    {
        Assert.Equal(new byte[] { 0x73, 0x05 }, HarconFrames.ShopOpen(1395));

        Assert.True(HarconFrames.TryReadShop(new byte[] { 0x83, 0x05, 0x10, 0xFD, 0, 0 }, out var request));
        Assert.Equal((ushort)1411, request.NpcType);
        Assert.Equal(64784u, request.ItemId);
        Assert.False(HarconFrames.TryReadShop(new byte[] { 0x83, 0x05, 0x10, 0xFD, 0 }, out _));
    }
}
