using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class CombatPacketTests
{
    [Fact]
    public void MobTargetInfoPacket_MatchesReferenceCapture()
    {
        var pkt = new MobTargetInfoPacket
        {
            EntityId = 0x12DF,
            FieldAt02Tbd = 100,
            FieldAt06Tbd = 40,
        };
        var bytes = pkt.EncodePayload();
        Assert.Equal(MobTargetInfoPacket.PayloadSize, bytes.Length);
        Assert.Equal(new byte[] { 0xDF, 0x12, 0x64, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, bytes);
        var got = MobTargetInfoPacket.DecodePayload(bytes);
        Assert.Equal((ushort)0x12DF, got.EntityId);
        Assert.Equal(100u, got.FieldAt02Tbd);
        Assert.Equal(40u, got.FieldAt06Tbd);
        Assert.Equal((byte)0, got.FieldAt0CTbd);
        Assert.Equal((ushort)0, got.FieldAt0ATbd);
    }

    [Fact]
    public void MobTargetInfoPacket_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() => MobTargetInfoPacket.DecodePayload(new byte[3]));
    }

    [Fact]
    public void OpcodeEnum_HasCombatEntries()
    {
        Assert.Equal(0x002A, (int)Opcode.S2C_OP_002A_TBD);
        Assert.Equal(0x002B, (int)Opcode.S2C_OP_002B_TBD);
        Assert.Equal(0x011E, (int)Opcode.S2C_OP_011E_TBD);
        Assert.Equal(0x016D, (int)Opcode.S2C_OP_016D_TBD);
        Assert.Equal(0x0121, (int)Opcode.S2C_MOB_TARGET_INFO);
    }
}
