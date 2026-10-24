using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class GunFramesTests
{
    private const ushort BlockId = 9;

    private static PlayerStrike Strike() => new(
        AttackerEntityId: 0x0301,
        TargetEntityId: 0x0201,
        AttackerX: 328,
        AttackerY: 232,
        TargetX: 344,
        TargetY: 248,
        Damage: 32,
        MobHpAfter: 68,
        HitFlags: 0,
        HitKind: 0,
        Killed: false,
        NextSwingAtUtc: default,
        MonsterParamId: 2100,
        MobMaxHp: 100);

    [Fact]
    public void ShotRequest_ReadsTargetCheckWordBulletsAndMoveType()
    {
        Assert.True(GunFrames.TryReadShot(new byte[] { 0x01, 0x02, 0x35, 0x12, 0x03, 0x00, 0x02 }, out var shot));

        Assert.Equal(0x0201, shot.TargetEntityId);
        Assert.Equal(0x1235, shot.CheckWord);
        Assert.Equal(3, shot.BulletsPerShot);
        Assert.Equal(2, shot.MoveType);
        Assert.True(shot.Charged);
    }

    [Fact]
    public void ShotRequest_WithBitZeroClearIsNotCharged()
    {
        Assert.True(GunFrames.TryReadShot(new byte[] { 0x01, 0x02, 0x34, 0x12, 0x01, 0x00, 0x00 }, out var shot));

        Assert.False(shot.Charged);
    }

    [Fact]
    public void ShortShotRequest_IsRejected()
    {
        Assert.False(GunFrames.TryReadShot(new byte[GunFrames.ShotRequestSize - 1], out _));
    }

    [Fact]
    public void Key_KeepsBitZeroFreeForTheChargeFlag()
    {
        Assert.Equal(new byte[] { 0x34, 0x12 }, GunFrames.Key(0x1235));
    }

    [Fact]
    public void Refusal_IsFourZeroBytes()
    {
        Assert.Equal(new byte[4], GunFrames.Refused());
    }

    [Fact]
    public void GunShot_IsTheMeleeChainWithTheGunAttackFrame()
    {
        var melee = MeleeSwingFrames.Encode(Strike(), BlockId);
        var gun = MeleeSwingFrames.EncodeGunShot(Strike(), BlockId);

        Assert.Equal(
            new[]
            {
                (Opcode.S2C_OP_001B_TBD, MeleeSwingFrames.NoBlock),
                (Opcode.S2C_GUN_SHOT, BlockId),
                (Opcode.S2C_OP_002A_TBD, BlockId),
                (Opcode.S2C_OP_011E_TBD, BlockId),
                (Opcode.S2C_COMMAND_BLOCK_END, BlockId),
            },
            gun.Select(f => (f.Opcode, f.BlockId)).ToArray());
        Assert.Equal(MeleeSwingFrames.AttackSize, gun[1].Payload.Length);
        Assert.Equal(MeleeSwingFrames.GunAttackKind, BitConverter.ToUInt16(gun[1].Payload, 2));
        Assert.Equal(MeleeSwingFrames.MeleeAttackKind, BitConverter.ToUInt16(melee[1].Payload, 2));
        Assert.Equal(melee[1].Payload[..2], gun[1].Payload[..2]);
        Assert.Equal(melee[1].Payload[4..], gun[1].Payload[4..]);
        for (var i = 2; i < melee.Count; i++)
        {
            Assert.Equal(melee[i].Payload, gun[i].Payload);
        }
    }
}
