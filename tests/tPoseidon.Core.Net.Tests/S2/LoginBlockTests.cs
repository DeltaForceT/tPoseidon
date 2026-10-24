using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class LoginBlockTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    public void ChatTradeAndMoveBlocksGoInTheirSlot(byte type, int slot)
    {
        var minutes = GmBlockRules.LoginBlockMinutes(new GmRunningBlock(type, 90));

        Assert.Equal(GmBlockRules.LoginBlockSlots, minutes.Length);
        Assert.Equal((ushort)90, minutes[slot]);
        Assert.Equal(1, minutes.Count(value => value != 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void OtherBlockTypesAreNotSentAtLogin(byte type)
    {
        Assert.All(GmBlockRules.LoginBlockMinutes(new GmRunningBlock(type, 90)), value => Assert.Equal(0, value));
    }

    [Fact]
    public void NoRunningBlockSendsZeros()
    {
        Assert.All(GmBlockRules.LoginBlockMinutes(null), value => Assert.Equal(0, value));
    }

    [Fact]
    public void TheMinutesAreCutToSixteenBits()
    {
        Assert.Equal(unchecked((ushort)70000), GmBlockRules.LoginBlockMinutes(new GmRunningBlock(2, 70000))[0]);
    }

    [Fact]
    public void TheHandoffReplyCarriesTheBlockMinutesAfterTheU32Port()
    {
        var minutes = GmBlockRules.LoginBlockMinutes(new GmRunningBlock(5, 0x0102));
        var packet = new ChannelHandoffReplyPacket
        {
            BaseBytes = ChannelHandoffReplyPacket.SnapshotBytes,
            SelectedCharUid = 0x10000009,
            IpAddress = "127.0.0.1",
            GamePort = 22006,
            BlockMinutes = minutes,
        };

        var bytes = packet.EncodePayload();

        Assert.Equal(8 + 6 + 10 + 4 + 14, bytes.Length);
        Assert.Equal(new byte[] { 0xF6, 0x55, 0x00, 0x00 }, bytes[24..28]);
        Assert.Equal(0x02, bytes[28 + 6]);
        Assert.Equal(0x01, bytes[28 + 7]);
        Assert.Equal(minutes, ChannelHandoffReplyPacket.DecodePayload(bytes).BlockMinutes);
        Assert.Equal((ushort)22006, ChannelHandoffReplyPacket.DecodePayload(bytes).GamePort);
    }

    [Fact]
    public void AFourteenCharacterAddressGivesTheCapturedFortySevenBytes()
    {
        var packet = new ChannelHandoffReplyPacket
        {
            BaseBytes = ChannelHandoffReplyPacket.SnapshotBytes,
            IpAddress = "192.168.56.102",
            GamePort = 23006,
            BlockMinutes = GmBlockRules.LoginBlockMinutes(new GmRunningBlock(2, 30)),
        };

        var bytes = packet.EncodePayload();

        Assert.Equal(47, bytes.Length);
        Assert.Equal(33, ChannelHandoffReplyPacket.BlockMinutesOffset(14));
        Assert.Equal(new byte[] { 0xDE, 0x59, 0x00, 0x00 }, bytes[29..33]);
        Assert.Equal(30, bytes[33]);
    }

    [Fact]
    public void WithoutBlocksTheReplyStaysTheSnapshotBytes()
    {
        var packet = ChannelHandoffReplyPacket.SnapshotDefault;

        Assert.Equal(ChannelHandoffReplyPacket.SnapshotBytes, packet.EncodePayload());
    }
}
