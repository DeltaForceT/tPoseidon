using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class EquipItemPacketTests
{
    [Fact]
    public void Encode_MatchesReferenceCapture_WeaponSample()
    {
        var pkt = new EquipItemPacket
        {
            ItemUid = 0x05C8FE12u,
            ClassTag = 1,
            SlotPosition = 0x01,
        };

        var bytes = pkt.EncodePayload();

        Assert.Equal(EquipItemPacket.PayloadSize, bytes.Length);
        Assert.Equal(new byte[] { 0x12, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 0x01 }, bytes);
    }

    [Fact]
    public void Encode_MatchesReferenceCapture_HatSample()
    {
        var pkt = new EquipItemPacket { ItemUid = 0x05C8FE13u, ClassTag = 1, SlotPosition = 0x00 };
        Assert.Equal(new byte[] { 0x13, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 0x00 }, pkt.EncodePayload());
    }

    [Fact]
    public void Encode_MatchesReferenceCapture_ShieldSample()
    {
        var pkt = new EquipItemPacket { ItemUid = 0x05C8FE14u, ClassTag = 1, SlotPosition = 0x02 };
        Assert.Equal(new byte[] { 0x14, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 0x02 }, pkt.EncodePayload());
    }

    [Fact]
    public void Decode_RoundTrips_FullU32WithClassTag()
    {
        var input = new EquipItemPacket
        {
            ItemUid = 0xCAFEBABEu,
            ClassTag = 0xDEADBEEFu,
            SlotPosition = 0x14,
        };

        var bytes = input.EncodePayload();
        var output = EquipItemPacket.DecodePayload(bytes);

        Assert.Equal(input.ItemUid, output.ItemUid);
        Assert.Equal(input.ClassTag, output.ClassTag);
        Assert.Equal(input.SlotPosition, output.SlotPosition);
    }

    [Fact]
    public void Decode_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() => EquipItemPacket.DecodePayload(new byte[8]));
        Assert.Throws<InvalidPacketException>(() => EquipItemPacket.DecodePayload(new byte[10]));
    }

    [Fact]
    public void OpcodeEnum_HasMatchingEntry()
    {
        Assert.Equal(0x0051, (int)Opcode.CMD_EQUIP_ITEM);
        Assert.Equal(0x0052, (int)Opcode.S2C_EQUIP_ITEM_ACK);
    }
}
