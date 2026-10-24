using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class ReleaseItemPacketTests
{
    [Fact]
    public void Encode_MatchesEquipShape_9Bytes()
    {
        var pkt = new ReleaseItemPacket { ItemUid = 0x05C8FE12u, ClassTag = 1, SlotPosition = 0x01 };
        var bytes = pkt.EncodePayload();
        Assert.Equal(ReleaseItemPacket.PayloadSize, bytes.Length);
        Assert.Equal(new byte[] { 0x12, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 0x01 }, bytes);
    }

    [Fact]
    public void Decode_RoundTrips()
    {
        var input = new ReleaseItemPacket { ItemUid = 0x9ABCDEF0u, ClassTag = 0x12345678u, SlotPosition = 0x05 };
        var output = ReleaseItemPacket.DecodePayload(input.EncodePayload());
        Assert.Equal(input.ItemUid, output.ItemUid);
        Assert.Equal(input.ClassTag, output.ClassTag);
        Assert.Equal(input.SlotPosition, output.SlotPosition);
    }

    [Fact]
    public void Decode_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() => ReleaseItemPacket.DecodePayload(new byte[7]));
    }

    [Fact]
    public void OpcodeEnum_HasMatchingEntry()
    {
        Assert.Equal(0x0054, (int)Opcode.CMD_RELEASE_ITEM);
    }
}
