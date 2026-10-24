using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class CombatPacketTests
{
    [Fact]
    public void DamageTickPacket_PlayerNormalHit_MatchesReferencePcap_F113()
    {
        var pkt = new DamageTickPacket
        {
            VictimEntityId = 0x228D,
            Damage = 30,
            HitFlags = DamageHitFlags.MobHit,
            HitKind = DamageHitKind.PlayerToMob,
        };
        var bytes = pkt.EncodePayload();
        Assert.Equal(DamageTickPacket.PayloadSize, bytes.Length);
        Assert.Equal(new byte[]
        {
            0x8D, 0x22,
            0x1E, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x03,
            0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
        }, bytes);
    }

    [Fact]
    public void DamageTickPacket_MobHitOnPlayer_MatchesReferencePcap_F151()
    {
        var pkt = new DamageTickPacket
        {
            VictimEntityId = 0xA435,
            Damage = 0,
            HitFlags = DamageHitFlags.Miss,
            HitKind = DamageHitKind.MobToPlayer,
        };
        var bytes = pkt.EncodePayload();
        Assert.Equal(new byte[]
        {
            0x35, 0xA4,
            0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01,
            0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x03, 0x00, 0x00, 0x00,
        }, bytes);
    }

    [Fact]
    public void DamageTickPacket_PlayerNormalKill_MatchesReferencePcap_F8()
    {
        var pkt = new DamageTickPacket
        {
            VictimEntityId = 0x228D,
            Damage = 20,
            HitFlags = DamageHitFlags.Kill,
            HitKind = DamageHitKind.PlayerToMob,
        };
        var bytes = pkt.EncodePayload();
        Assert.Equal(new byte[]
        {
            0x8D, 0x22,
            0x14, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x80,
            0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
        }, bytes);
    }

    [Fact]
    public void DamageTickPacket_PlayerCritKill_MatchesReferencePcap_F15()
    {
        var pkt = new DamageTickPacket
        {
            VictimEntityId = 0x200D,
            Damage = 30,
            HitFlags = DamageHitFlags.CritKill,
            HitKind = DamageHitKind.PlayerToMob,
        };
        var bytes = pkt.EncodePayload();
        Assert.Equal(new byte[]
        {
            0x0D, 0x20,
            0x1E, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x83,
            0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
        }, bytes);
    }

    [Fact]
    public void DamageTickPacket_DamageClampedToUshort()
    {
        var pkt = new DamageTickPacket
        {
            VictimEntityId = 0x21CA,
            Damage = ushort.MaxValue,
            HitFlags = DamageHitFlags.Crit,
            HitKind = DamageHitKind.PlayerToMob,
        };
        var bytes = pkt.EncodePayload();
        var decoded = DamageTickPacket.DecodePayload(bytes);
        Assert.Equal((ushort)ushort.MaxValue, decoded.Damage);
    }

    [Fact]
    public void DamageTickPacket_Decode_RoundTrips()
    {
        var src = new DamageTickPacket
        {
            VictimEntityId = 0xCAFE,
            Damage = 9999,
            HitFlags = DamageHitFlags.CritKill,
            HitKind = DamageHitKind.PlayerToMob,
        };
        var bytes = src.EncodePayload();
        var got = DamageTickPacket.DecodePayload(bytes);
        Assert.Equal(src.VictimEntityId, got.VictimEntityId);
        Assert.Equal(src.Damage, got.Damage);
        Assert.Equal(src.HitFlags, got.HitFlags);
        Assert.Equal(src.HitKind, got.HitKind);
    }

    [Fact]
    public void DamageHitFlags_HasAllRevdValues()
    {
        Assert.Equal((byte)0x00, DamageHitFlags.Normal);
        Assert.Equal((byte)0x01, DamageHitFlags.Miss);
        Assert.Equal((byte)0x03, DamageHitFlags.MobHit);
        Assert.Equal((byte)0x80, DamageHitFlags.Kill);
        Assert.Equal((byte)0x80, DamageHitFlags.Crit);
        Assert.Equal((byte)0x83, DamageHitFlags.CritKill);
    }

    [Fact]
    public void DamageHitKind_HasAllRevdValues()
    {
        Assert.Equal(0x00000001u, DamageHitKind.PlayerToMob);
        Assert.Equal(0x00000003u, DamageHitKind.MobToPlayer);
    }

    [Fact]
    public void MobHpUpdatePacket_Encode_MatchesReferenceCapture()
    {
        var pkt = new MobHpUpdatePacket { EntityId = 0x1F4B, RemainingHp = 0x44 };
        var bytes = pkt.EncodePayload();

        Assert.Equal(MobHpUpdatePacket.PayloadSize, bytes.Length);
        Assert.Equal(new byte[] { 0x4B, 0x1F, 0x44, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, bytes);
    }

    [Fact]
    public void MobHpUpdatePacket_HpZero_SignalsDeath()
    {
        var pkt = new MobHpUpdatePacket { EntityId = 0x1F4B, RemainingHp = 0 };
        var bytes = pkt.EncodePayload();
        Assert.Equal(0x00, bytes[2]);
        Assert.Equal(0x00, bytes[3]);
        Assert.Equal(0x00, bytes[4]);
        Assert.Equal(0x00, bytes[5]);
    }

    [Fact]
    public void MobHpUpdatePacket_BossHp_DoesNotWrapAt65535()
    {
        var pkt = new MobHpUpdatePacket { EntityId = 0x1F4B, RemainingHp = 1_000_000 };
        var bytes = pkt.EncodePayload();
        var decoded = MobHpUpdatePacket.DecodePayload(bytes);
        Assert.Equal(1_000_000u, decoded.RemainingHp);
    }

    [Fact]
    public void MobHpUpdatePacket_SecondLifeCount_RoundTrips()
    {
        var pkt = new MobHpUpdatePacket { EntityId = 0x1F4B, RemainingHp = 100, SecondLifeCount = 3 };
        var bytes = pkt.EncodePayload();
        var decoded = MobHpUpdatePacket.DecodePayload(bytes);
        Assert.Equal((ushort)3, decoded.SecondLifeCount);
        Assert.Equal(100u, decoded.RemainingHp);
    }

    [Fact]
    public void DeathConfirmPacket_Encode_MatchesReferenceCapture()
    {
        var pkt = new DeathConfirmPacket { EntityId = 0x1F4B };
        var bytes = pkt.EncodePayload();

        Assert.Equal(DeathConfirmPacket.PayloadSize, bytes.Length);
        Assert.Equal(new byte[] { 0x4B, 0x1F, 0x00 }, bytes);
    }

    [Fact]
    public void AggroNotifyPacket_RoundTrips()
    {
        var pkt = new AggroNotifyPacket { MobEntityId = 0x1F4B, AttackerEntityId = 0x3574 };
        var bytes = pkt.EncodePayload();
        Assert.Equal(AggroNotifyPacket.PayloadSize, bytes.Length);
        Assert.Equal(new byte[] { 0x4B, 0x1F, 0x74, 0x35 }, bytes);
        var got = AggroNotifyPacket.DecodePayload(bytes);
        Assert.Equal((ushort)0x1F4B, got.MobEntityId);
        Assert.Equal((ushort)0x3574, got.AttackerEntityId);
    }

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
    public void DamageTickPacket_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() => DamageTickPacket.DecodePayload(new byte[20]));
    }

    [Fact]
    public void MobHpUpdatePacket_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() => MobHpUpdatePacket.DecodePayload(new byte[8]));
    }

    [Fact]
    public void DeathConfirmPacket_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() => DeathConfirmPacket.DecodePayload(new byte[2]));
    }

    [Fact]
    public void MobTargetInfoPacket_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() => MobTargetInfoPacket.DecodePayload(new byte[3]));
    }

    [Fact]
    public void Packet01D8Tbd_MatchesReferenceCapture_ByteValue0x2E()
    {
        var pkt = new Packet01D8Tbd
        {
            EntityId = 0x35A4,
            FieldAt02Tbd = 0x2E,
        };
        var bytes = pkt.EncodePayload();
        Assert.Equal(Packet01D8Tbd.PayloadSize, bytes.Length);
        Assert.Equal(new byte[] { 0xA4, 0x35, 0x2E }, bytes);
        var got = Packet01D8Tbd.DecodePayload(bytes);
        Assert.Equal((ushort)0x35A4, got.EntityId);
        Assert.Equal((byte)0x2E, got.FieldAt02Tbd);
    }

    [Fact]
    public void Packet01D8Tbd_MatchesReferenceCapture_ByteValue0x1D()
    {
        var pkt = new Packet01D8Tbd
        {
            EntityId = 0x228D,
            FieldAt02Tbd = 0x1D,
        };
        var bytes = pkt.EncodePayload();
        Assert.Equal(new byte[] { 0x8D, 0x22, 0x1D }, bytes);
    }

    [Fact]
    public void Packet01D8Tbd_WrongLength_Throws()
    {
        Assert.Throws<InvalidPacketException>(() => Packet01D8Tbd.DecodePayload(new byte[2]));
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
