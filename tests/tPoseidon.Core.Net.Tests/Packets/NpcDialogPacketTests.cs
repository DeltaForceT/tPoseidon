using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class NpcDialogPacketTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_PreservesAllFields()
    {
        var src = new NpcDialogPacket
        {
            EntityId = 0x0153,
            PortraitType = 0x051F,
            NpcSType = 0x0A,
            Text = "Welcome.\\Okay\\Exit\\@\\@ref,n4",
        };
        var wire = src.EncodePayload();
        Assert.Equal(5 + src.Text.Length + NpcDialogPacket.TrailingPadBytes, wire.Length);

        var dec = NpcDialogPacket.DecodePayload(wire);
        Assert.Equal(src.EntityId, dec.EntityId);
        Assert.Equal(src.PortraitType, dec.PortraitType);
        Assert.Equal(src.NpcSType, dec.NpcSType);
        Assert.Equal(src.Text, dec.Text);
    }

    [Fact]
    public void Encode_HeaderBytesAreLittleEndian()
    {
        var pkt = new NpcDialogPacket
        {
            EntityId = 0x0153,
            PortraitType = 0x051F,
            NpcSType = 0x0A,
            Text = "",
        };
        var wire = pkt.EncodePayload();
        Assert.Equal(5 + NpcDialogPacket.TrailingPadBytes, wire.Length);
        Assert.Equal(0x53, wire[0]);
        Assert.Equal(0x01, wire[1]);
        Assert.Equal(0x1F, wire[2]);
        Assert.Equal(0x05, wire[3]);
        Assert.Equal(0x0A, wire[4]);
        for (int i = 5; i < wire.Length; i++) Assert.Equal(0, wire[i]);
    }

    [Fact]
    public void Decode_RejectsTooShortPayload()
    {
        Assert.Throws<InvalidPacketException>(() =>
            NpcDialogPacket.DecodePayload(new byte[4]));
    }

    [Fact]
    public void Decode_EmptyTextSection_ReturnsEmptyString()
    {
        var wire = new byte[] { 0x53, 0x01, 0x1F, 0x05, 0x0A };
        var dec = NpcDialogPacket.DecodePayload(wire);
        Assert.Equal(string.Empty, dec.Text);
    }
}
