using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class EntityIdAnnouncePacketTests
{
    [Fact]
    public void Encode_3D1Variant_10PadBytes()
    {
        var pkt = new EntityIdAnnouncePacket { EntityId = 0xFBC9, PadBytes = 10 };
        var wire = pkt.EncodePayload();
        Assert.Equal(12, wire.Length);
        Assert.Equal(0xC9, wire[0]);
        Assert.Equal(0xFB, wire[1]);
        for (var i = 2; i < 12; i++) Assert.Equal((byte)0, wire[i]);
    }

    [Fact]
    public void Encode_3E7Variant_11PadBytes()
    {
        var pkt = new EntityIdAnnouncePacket { EntityId = 0xFBC9, PadBytes = 11 };
        var wire = pkt.EncodePayload();
        Assert.Equal(13, wire.Length);
        Assert.Equal(0xC9, wire[0]);
        Assert.Equal(0xFB, wire[1]);
        for (var i = 2; i < 13; i++) Assert.Equal((byte)0, wire[i]);
    }

    [Theory]
    [InlineData(new byte[] { 0xC9, 0xFB, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0xFBC9, 10)]
    [InlineData(new byte[] { 0x44, 0xFC, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0xFC44, 10)]
    [InlineData(new byte[] { 0x33, 0xFA, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0xFA33, 11)]
    [InlineData(new byte[] { 0x6D, 0xFE, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0xFE6D, 11)]
    public void Decode_CapturedSamples(byte[] wire, ushort expectedId, int expectedPad)
    {
        var dec = EntityIdAnnouncePacket.DecodePayload(wire);
        Assert.Equal(expectedId, dec.EntityId);
        Assert.Equal(expectedPad, dec.PadBytes);
    }

    [Fact]
    public void EncodeAfterDecode_IsByteIdentical()
    {
        var wire = new byte[] { 0x67, 0xA1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var dec = EntityIdAnnouncePacket.DecodePayload(wire);
        var reencoded = dec.EncodePayload();
        Assert.Equal(wire, reencoded);
    }

    [Fact]
    public void Decode_RejectsPayloadTooShort()
    {
        Assert.Throws<InvalidPacketException>(() =>
            EntityIdAnnouncePacket.DecodePayload(new byte[1]));
    }

    [Fact]
    public void Encode_NegativePadBytes_Throws()
    {
        var pkt = new EntityIdAnnouncePacket { EntityId = 0, PadBytes = -1 };
        Assert.Throws<ArgumentOutOfRangeException>(() => pkt.EncodePayload());
    }
}
