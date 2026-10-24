using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class TargetActionPacketTests
{
    [Theory]
    [InlineData(new byte[] { 0xF8, 0x0F, 0x02 }, 0x0FF8, 0x02)]
    [InlineData(new byte[] { 0x30, 0x1A, 0x02 }, 0x1A30, 0x02)]
    [InlineData(new byte[] { 0xF6, 0x17, 0x02 }, 0x17F6, 0x02)]
    [InlineData(new byte[] { 0x67, 0xA1, 0x02 }, 0xA167, 0x02)]
    public void Decode_CapturedSamples_ParsesLittleEndian(byte[] wire, ushort expectedId, byte expectedFlag)
    {
        var dec = TargetActionPacket.DecodePayload(wire);
        Assert.Equal(expectedId, dec.EntityId);
        Assert.Equal(expectedFlag, dec.ActionFlag);
    }

    [Theory]
    [InlineData(new byte[] { 0xF8, 0x0F, 0x02 })]
    [InlineData(new byte[] { 0x67, 0xA1, 0x02 })]
    [InlineData(new byte[] { 0x21, 0xA0, 0x02 })]
    public void EncodeAfterDecode_IsByteIdentical(byte[] captured)
    {
        var dec = TargetActionPacket.DecodePayload(captured);
        var reencoded = dec.EncodePayload();
        Assert.Equal(captured, reencoded);
    }

    [Fact]
    public void DefaultFlag_Is02()
    {
        var pkt = new TargetActionPacket { EntityId = 0x1234 };
        Assert.Equal(0x02, pkt.ActionFlag);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            TargetActionPacket.DecodePayload(new byte[2]));
        Assert.Throws<InvalidPacketException>(() =>
            TargetActionPacket.DecodePayload(new byte[4]));
    }
}

public class TargetMarkPacketTests
{
    [Theory]
    [InlineData(new byte[] { 0xF8, 0x0F }, 0x0FF8)]
    [InlineData(new byte[] { 0x30, 0x1A }, 0x1A30)]
    [InlineData(new byte[] { 0x21, 0xA0 }, 0xA021)]
    public void Decode_CapturedSamples(byte[] wire, ushort expectedId)
    {
        var dec = TargetMarkPacket.DecodePayload(wire);
        Assert.Equal(expectedId, dec.EntityId);
    }

    [Fact]
    public void EncodeDecode_RoundTrip()
    {
        var src = new TargetMarkPacket { EntityId = 0xBEEF };
        var dec = TargetMarkPacket.DecodePayload(src.EncodePayload());
        Assert.Equal(0xBEEF, dec.EntityId);
    }

    [Fact]
    public void Decode_RejectsWrongLength()
    {
        Assert.Throws<InvalidPacketException>(() =>
            TargetMarkPacket.DecodePayload(new byte[1]));
        Assert.Throws<InvalidPacketException>(() =>
            TargetMarkPacket.DecodePayload(new byte[3]));
    }

    [Fact]
    public void TargetMark_And_TargetAction_CarrySameEntityId()
    {
        var markBytes = new byte[] { 0x67, 0xA1 };
        var actionBytes = new byte[] { 0x67, 0xA1, 0x02 };

        var mark = TargetMarkPacket.DecodePayload(markBytes);
        var action = TargetActionPacket.DecodePayload(actionBytes);

        Assert.Equal(mark.EntityId, action.EntityId);
    }
}
