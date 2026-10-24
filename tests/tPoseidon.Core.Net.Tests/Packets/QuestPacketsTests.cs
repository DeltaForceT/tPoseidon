using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class QuestPacketsTests
{
    [Fact]
    public void DialogStateAck_Default_Emits7F00()
    {
        var pkt = new DialogStateAckPacket();
        var wire = pkt.EncodePayload();
        Assert.Equal(new byte[] { 0x7F, 0x00 }, wire);
    }

    [Fact]
    public void DialogStateAck_RoundTrip()
    {
        var src = new DialogStateAckPacket { Marker = DialogStateAckPacket.StandardMarker };
        var dec = DialogStateAckPacket.DecodePayload(src.EncodePayload());
        Assert.Equal(src.Marker, dec.Marker);
    }

    [Fact]
    public void DialogStateAck_Decode_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() =>
            DialogStateAckPacket.DecodePayload(new byte[1]));
        Assert.Throws<InvalidPacketException>(() =>
            DialogStateAckPacket.DecodePayload(new byte[3]));
    }
}
