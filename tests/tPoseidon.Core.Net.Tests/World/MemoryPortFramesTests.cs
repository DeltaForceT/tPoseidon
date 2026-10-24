using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class MemoryPortFramesTests
{
    private static readonly MemoryPort Saved = new()
    {
        CharacterId = 5,
        PortType = 0,
        SlotNum = 2,
        Zone = 12,
        PositionX = 300,
        PositionY = 0x1234,
        Description = "Coral",
    };

    [Fact]
    public void RecordIsSlotZoneXYAndTwentyFourDescriptionBytes()
    {
        var record = MemoryPortFrames.Record(Saved, Encoding.ASCII);
        Assert.Equal(MemoryPortFrames.RecordSize, record.Length);
        Assert.Equal(new byte[] { 2, 0, 12, 0, 0, 0, 0x2C, 0x01, 0x34, 0x12, (byte)'C', (byte)'o', (byte)'r', (byte)'a', (byte)'l', 0 }, record[..16]);
        Assert.All(record[15..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void LongDescriptionsKeepTwentyThreeBytesAndAZero()
    {
        var record = MemoryPortFrames.Record(Saved with { Description = new string('x', 40) }, Encoding.ASCII);
        Assert.All(record[10..33], b => Assert.Equal((byte)'x', b));
        Assert.Equal(0, record[33]);
        Assert.Equal(new string('x', 23), MemoryPortFrames.StoredDescription(new string('x', 40), Encoding.ASCII));
    }

    [Fact]
    public void ListEchoesTheItemUidThenCountAndRecords()
    {
        var list = MemoryPortFrames.List(0x1_0000_0007UL, new[] { Saved, Saved with { SlotNum = 3 } }, Encoding.ASCII);
        Assert.Equal(9 + 2 * MemoryPortFrames.RecordSize, list.Length);
        Assert.Equal(new byte[] { 7, 0, 0, 0, 1, 0, 0, 0, 2 }, list[..9]);
        Assert.Equal(3, list[9 + MemoryPortFrames.RecordSize]);
    }

    [Fact]
    public void RequestsParseAsTheClientSendsThem()
    {
        var save = new byte[] { 7, 0, 0, 0, 1, 0, 0, 0, 4, 0, (byte)'H', (byte)'i', 0 };
        Assert.True(MemoryPortFrames.TryReadSave(save, Encoding.ASCII, out var parsed));
        Assert.Equal(new MemoryPortSave(0x1_0000_0007UL, 4, "Hi"), parsed);
        Assert.False(MemoryPortFrames.TryReadSave(save.AsSpan(0, 9), Encoding.ASCII, out _));
        Assert.False(MemoryPortFrames.TryReadOpen(save.AsSpan(0, 7), out _));

        Assert.True(MemoryPortFrames.TryReadSlot(save.AsSpan(0, 10), out var uid, out var slot));
        Assert.Equal((0x1_0000_0007UL, (ushort)4), (uid, slot));

        var use = new byte[] { 7, 0, 0, 0, 1, 0, 0, 0, 2, 0, 3, 0 };
        Assert.True(MemoryPortFrames.TryReadUseWithData(use, out var withData));
        Assert.Equal(new byte[] { 3, 0 }, withData.Data);
        Assert.False(MemoryPortFrames.TryReadUseWithData(use.AsSpan(0, 11), out _));
    }

    [Fact]
    public void ShortAnswersCarryTheirFields()
    {
        Assert.Equal(new byte[] { 4, 0, 0, 0 }, MemoryPortFrames.WarpRefused(4));
        Assert.Equal(new byte[] { 3, 0 }, MemoryPortFrames.Deleted(3));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, MemoryPortFrames.OpenFailed(0));
    }

    [Fact]
    public void MasterMessagesRoundTrip()
    {
        var slots = MemoryPortMessages.DecodeSlots(5, MemoryPortMessages.EncodeSlots(new[] { Saved }));
        Assert.Equal(Saved, Assert.Single(slots));
        Assert.Equal(Saved, MemoryPortMessages.DecodeSlot(5, MemoryPortMessages.EncodeSlot(Saved)));
        Assert.Equal(new MemoryPortSlotRequest(1, 4), MemoryPortMessages.DecodeSlotRequest(MemoryPortMessages.EncodeSlotRequest(new MemoryPortSlotRequest(1, 4))));
        Assert.Equal(104u, MemoryPortMessages.DecodeCode(MemoryPortMessages.EncodeCode(104)));
    }
}
