using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class NpcStatePacketTests
{
    [Theory]
    [InlineData(new byte[] { 0x7F, 0x00, 0xA0 }, 0x007F, 0xA0)]
    [InlineData(new byte[] { 0x72, 0x00, 0x5F }, 0x0072, 0x5F)]
    [InlineData(new byte[] { 0x05, 0x00, 0x5F }, 0x0005, 0x5F)]
    public void Decode_RealCapturedPayloads(byte[] wire, ushort expectedEntityId, byte expectedState)
    {
        var dec = NpcStatePacket.DecodePayload(wire);
        Assert.Equal(expectedEntityId, dec.EntityId);
        Assert.Equal(expectedState, dec.State);
    }

    [Theory]
    [InlineData(new byte[] { 0x7F, 0x00, 0xA0 })]
    [InlineData(new byte[] { 0x72, 0x00, 0x5F })]
    public void EncodeAfterDecode_IsByteIdentical(byte[] captured)
    {
        var dec = NpcStatePacket.DecodePayload(captured);
        var reencoded = dec.EncodePayload();
        Assert.Equal(captured, reencoded);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            NpcStatePacket.DecodePayload(new byte[] { 0x00, 0x00 }));
        Assert.Throws<InvalidPacketException>(() =>
            NpcStatePacket.DecodePayload(new byte[] { 0x00, 0x00, 0x00, 0x00 }));
    }

    [Fact]
    public void DefaultState_IsCommonObservedValue()
    {
        var pkt = new NpcStatePacket { EntityId = 1 };
        Assert.Equal(0xA0, pkt.State);
    }
}
