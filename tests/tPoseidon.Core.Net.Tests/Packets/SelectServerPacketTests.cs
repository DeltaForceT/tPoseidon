using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class SelectServerPacketTests
{
    [Fact]
    public void EncodeDecode_RoundTrip()
    {
        var src = new SelectServerPacket { ServerIndex = 3, ChannelIndex = 7 };
        var wire = src.EncodePayload();
        Assert.Equal(SelectServerPacket.PayloadSize, wire.Length);

        var dec = SelectServerPacket.DecodePayload(wire);
        Assert.Equal(3, dec.ServerIndex);
        Assert.Equal(7, dec.ChannelIndex);
    }

    [Theory]
    [InlineData(new byte[] { 0x01, 0x00, 0x01, 0x00 }, 1, 1)]
    [InlineData(new byte[] { 0xFF, 0xFF, 0x00, 0x00 }, 0xFFFF, 0)]
    [InlineData(new byte[] { 0x00, 0x00, 0xFF, 0xFF }, 0, 0xFFFF)]
    public void Decode_ParsesLittleEndian(byte[] wire, ushort expectedServer, ushort expectedChannel)
    {
        var dec = SelectServerPacket.DecodePayload(wire);
        Assert.Equal(expectedServer, dec.ServerIndex);
        Assert.Equal(expectedChannel, dec.ChannelIndex);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            SelectServerPacket.DecodePayload(new byte[3]));
        Assert.Throws<InvalidPacketException>(() =>
            SelectServerPacket.DecodePayload(new byte[5]));
    }
}
