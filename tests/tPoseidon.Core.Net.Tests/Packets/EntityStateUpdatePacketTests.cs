using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class EntityStateUpdatePacketTests
{
    [Theory]
    [InlineData(new byte[] { 0x83, 0x00, 0x01, 0x03, 0x00, 0x00 }, 0x0083, 0x01, 0x03, 0x00)]
    [InlineData(new byte[] { 0x7E, 0x00, 0x03, 0x05, 0x00, 0x01 }, 0x007E, 0x03, 0x05, 0x01)]
    [InlineData(new byte[] { 0x84, 0x00, 0x02, 0x02, 0x00, 0x01 }, 0x0084, 0x02, 0x02, 0x01)]
    public void Decode_RealCapturedPayloads(byte[] wire, ushort id, byte a, byte b, byte flag)
    {
        var dec = EntityStateUpdatePacket.DecodePayload(wire);
        Assert.Equal(id, dec.EntityId);
        Assert.Equal(a, dec.StateA);
        Assert.Equal(b, dec.StateB);
        Assert.Equal(0x00, dec.Reserved4);
        Assert.Equal(flag, dec.Flag);
    }

    [Theory]
    [InlineData(new byte[] { 0x83, 0x00, 0x01, 0x03, 0x00, 0x00 })]
    [InlineData(new byte[] { 0x7E, 0x00, 0x03, 0x05, 0x00, 0x01 })]
    [InlineData(new byte[] { 0x78, 0x00, 0x02, 0x05, 0x00, 0x00 })]
    [InlineData(new byte[] { 0x75, 0x00, 0x03, 0x02, 0x00, 0x00 })]
    [InlineData(new byte[] { 0x84, 0x00, 0x02, 0x02, 0x00, 0x01 })]
    [InlineData(new byte[] { 0x70, 0x00, 0x02, 0x05, 0x00, 0x01 })]
    public void EncodeAfterDecode_IsByteIdentical(byte[] captured)
    {
        var dec = EntityStateUpdatePacket.DecodePayload(captured);
        var reencoded = dec.EncodePayload();
        Assert.Equal(captured, reencoded);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            EntityStateUpdatePacket.DecodePayload(new byte[5]));
        Assert.Throws<InvalidPacketException>(() =>
            EntityStateUpdatePacket.DecodePayload(new byte[7]));
    }
}
