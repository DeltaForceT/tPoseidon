using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class C2SMovementPacketTests
{
    [Theory]
    [InlineData(new byte[] { 0xA8, 0x03, 0x98, 0x02, 0x30, 0x03, 0xEE, 0x01, 0x02 },
                0x03A8, 0x0298, 0x0330, 0x01EE, 0x02)]
    [InlineData(new byte[] { 0x99, 0x03, 0x89, 0x02, 0x20, 0x03, 0xC4, 0x01, 0x02 },
                0x0399, 0x0289, 0x0320, 0x01C4, 0x02)]
    [InlineData(new byte[] { 0x36, 0x03, 0x26, 0x02, 0x15, 0x03, 0x5B, 0x01, 0x02 },
                0x0336, 0x0226, 0x0315, 0x015B, 0x02)]
    public void Decode_RealCapturedSamples(byte[] wire,
        ushort fromX, ushort fromY, ushort toX, ushort toY, byte flag)
    {
        var dec = C2SMovementPacket.DecodePayload(wire);
        Assert.Equal(fromX, dec.FromX);
        Assert.Equal(fromY, dec.FromY);
        Assert.Equal(toX, dec.ToX);
        Assert.Equal(toY, dec.ToY);
        Assert.Equal(flag, dec.Flag);

        var dx = (int)dec.ToX - dec.FromX;
        var dy = (int)dec.ToY - dec.FromY;
        Assert.InRange(dx, -2000, 2000);
        Assert.InRange(dy, -2000, 2000);
    }

    [Theory]
    [InlineData(new byte[] { 0xA8, 0x03, 0x98, 0x02, 0x30, 0x03, 0xEE, 0x01, 0x02 })]
    [InlineData(new byte[] { 0x99, 0x03, 0x89, 0x02, 0x20, 0x03, 0xC4, 0x01, 0x02 })]
    [InlineData(new byte[] { 0x18, 0x03, 0xA9, 0x01, 0x20, 0x03, 0xB9, 0x01, 0x02 })]
    [InlineData(new byte[] { 0x28, 0x03, 0x83, 0x01, 0xE4, 0x03, 0x11, 0x02, 0x02 })]
    public void EncodeAfterDecode_IsByteIdentical(byte[] captured)
    {
        var dec = C2SMovementPacket.DecodePayload(captured);
        var reencoded = dec.EncodePayload();
        Assert.Equal(captured, reencoded);
    }

    [Fact]
    public void DefaultFlag_IsObservedConstant()
    {
        var pkt = new C2SMovementPacket();
        Assert.Equal(0x02, pkt.Flag);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            C2SMovementPacket.DecodePayload(new byte[8]));
        Assert.Throws<InvalidPacketException>(() =>
            C2SMovementPacket.DecodePayload(new byte[10]));
    }
}
