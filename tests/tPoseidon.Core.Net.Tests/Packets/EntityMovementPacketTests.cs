using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class EntityMovementPacketTests
{
    [Fact]
    public void Decode_RealCapturedPayload_ProducesExpectedFields()
    {
        var captured = new byte[] { 0x2B, 0x79, 0x88, 0x02, 0x68, 0x01, 0xD8, 0x03, 0xE8, 0x01, 0x02 };
        var dec = EntityMovementPacket.DecodePayload(captured);

        Assert.Equal(0x792B, dec.EntityId);
        Assert.Equal(0x0288, dec.FromX);
        Assert.Equal(0x0168, dec.FromY);
        Assert.Equal(0x03D8, dec.ToX);
        Assert.Equal(0x01E8, dec.ToY);
        Assert.Equal(0x02, dec.Flag);

        var dx = (int)dec.ToX - dec.FromX;
        var dy = (int)dec.ToY - dec.FromY;
        Assert.InRange(dx, -2000, 2000);
        Assert.InRange(dy, -2000, 2000);
    }

    [Theory]
    [InlineData(new byte[] { 0x2B, 0x79, 0x88, 0x02, 0x68, 0x01, 0xD8, 0x03, 0xE8, 0x01, 0x02 })]
    [InlineData(new byte[] { 0x2C, 0x79, 0x98, 0x02, 0x88, 0x01, 0x38, 0x03, 0xE8, 0x01, 0x02 })]
    [InlineData(new byte[] { 0xF9, 0x0A, 0x58, 0x04, 0xD8, 0x11, 0x48, 0x05, 0x08, 0x11, 0x01 })]
    [InlineData(new byte[] { 0x68, 0x1D, 0x78, 0x03, 0x98, 0x10, 0x48, 0x04, 0x68, 0x11, 0x01 })]
    public void EncodeAfterDecode_IsByteIdentical(byte[] captured)
    {
        var dec = EntityMovementPacket.DecodePayload(captured);
        var reencoded = dec.EncodePayload();
        Assert.Equal(captured, reencoded);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            EntityMovementPacket.DecodePayload(new byte[10]));
        Assert.Throws<InvalidPacketException>(() =>
            EntityMovementPacket.DecodePayload(new byte[12]));
    }
}
