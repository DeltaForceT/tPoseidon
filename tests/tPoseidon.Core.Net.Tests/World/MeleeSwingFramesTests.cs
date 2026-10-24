using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class MeleeSwingFramesTests
{
    private const ushort BlockId = 7;

    private static PlayerStrike Strike(int damage, int hpAfter, bool killed) => new(
        AttackerEntityId: 0x0301,
        TargetEntityId: 0x0201,
        AttackerX: 328,
        AttackerY: 232,
        TargetX: 344,
        TargetY: 248,
        Damage: damage,
        MobHpAfter: hpAfter,
        HitFlags: 0,
        HitKind: killed ? (uint)0x80 : 0,
        Killed: killed,
        NextSwingAtUtc: default,
        MonsterParamId: 2100,
        MobMaxHp: 100);

    [Fact]
    public void Swing_IsTheHaltFrameThenOneClosedBlock()
    {
        var frames = MeleeSwingFrames.Encode(Strike(damage: 32, hpAfter: 68, killed: false), BlockId);

        Assert.Equal(
            new[]
            {
                (Opcode.S2C_OP_001B_TBD, MeleeSwingFrames.NoBlock),
                (Opcode.S2C_OP_001F_TBD, BlockId),
                (Opcode.S2C_OP_002A_TBD, BlockId),
                (Opcode.S2C_OP_011E_TBD, BlockId),
                (Opcode.S2C_COMMAND_BLOCK_END, BlockId),
            },
            frames.Select(f => (f.Opcode, f.BlockId)).ToArray());
        Assert.Empty(frames[4].Payload);
    }

    [Fact]
    public void SwingOnAPlayer_HasNoMonsterHpAndCarriesTheVictimFramesInsideTheBlock()
    {
        var peace = new SwingFrame(Opcode.S2C_PK_PEACE_BROKEN, Array.Empty<byte>(), BlockId, SwingAudience.Owner);
        var death = new SwingFrame(Opcode.S2C_OP_002B_TBD, new byte[3], BlockId, SwingAudience.Everyone);
        var frames = MeleeSwingFrames.EncodeOnPlayer(Strike(damage: 100, hpAfter: 0, killed: true), BlockId, new[] { peace }, new[] { death });

        Assert.Equal(
            new[]
            {
                (Opcode.S2C_OP_001B_TBD, MeleeSwingFrames.NoBlock),
                (Opcode.S2C_OP_001F_TBD, BlockId),
                (Opcode.S2C_PK_PEACE_BROKEN, BlockId),
                (Opcode.S2C_OP_002A_TBD, BlockId),
                (Opcode.S2C_OP_002B_TBD, BlockId),
                (Opcode.S2C_COMMAND_BLOCK_END, BlockId),
                (Opcode.S2C_OP_0020_TBD, MeleeSwingFrames.NoBlock),
            },
            frames.Select(f => (f.Opcode, f.BlockId)).ToArray());
        Assert.Equal(SwingAudience.Owner, frames[2].Audience);
    }

    [Fact]
    public void Payloads_FollowTheWireLayouts()
    {
        var frames = MeleeSwingFrames.Encode(Strike(damage: 32, hpAfter: 68, killed: false), BlockId);

        Assert.Equal(new byte[] { 0x01, 0x03, 0x48, 0x01, 0xE8, 0x00, 0x02 }, frames[0].Payload);
        Assert.Equal(
            new byte[]
            {
                0x01, 0x03, 0x01, 0x00, 0x01, 0x02,
                0x48, 0x01, 0xE8, 0x00, 0x58, 0x01, 0xF8, 0x00,
                0x20, 0x00, 0x00,
            },
            frames[1].Payload);
        Assert.Equal(MeleeSwingFrames.HitResultSize, frames[2].Payload.Length);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x20, 0x00, 0x00, 0x00 }, frames[2].Payload[..6]);
        Assert.All(frames[2].Payload[6..22], b => Assert.Equal(0, b));
        Assert.Equal(MeleeSwingFrames.PlayerHitPresentation, BitConverter.ToInt32(frames[2].Payload, 22));
        Assert.All(frames[2].Payload[26..], b => Assert.Equal(0, b));
        Assert.Equal(new byte[] { 0x01, 0x02, 0x44, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, frames[3].Payload);
    }

    [Fact]
    public void Kill_AddsTheDeathFrameInsideTheBlockAndLeavesTheAttackStateAfterIt()
    {
        var frames = MeleeSwingFrames.Encode(Strike(damage: 68, hpAfter: 0, killed: true), BlockId);

        Assert.Equal(
            new[]
            {
                (Opcode.S2C_OP_001B_TBD, MeleeSwingFrames.NoBlock),
                (Opcode.S2C_OP_001F_TBD, BlockId),
                (Opcode.S2C_OP_002A_TBD, BlockId),
                (Opcode.S2C_OP_002B_TBD, BlockId),
                (Opcode.S2C_OP_011E_TBD, BlockId),
                (Opcode.S2C_COMMAND_BLOCK_END, BlockId),
                (Opcode.S2C_OP_0020_TBD, MeleeSwingFrames.NoBlock),
            },
            frames.Select(f => (f.Opcode, f.BlockId)).ToArray());
        Assert.Equal(0x80, frames[1].Payload[16]);
        Assert.Equal(0x80, frames[2].Payload[10]);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x00 }, frames[3].Payload);
        Assert.Equal(new byte[] { 0x01, 0x03 }, frames[6].Payload);
    }

    [Fact]
    public void MonsterHpGoesOnlyToThePlayersEngagedWithTheMonster()
    {
        var frames = MeleeSwingFrames.Encode(Strike(damage: 32, hpAfter: 68, killed: false), BlockId);

        Assert.Equal(
            new[]
            {
                SwingAudience.Everyone, SwingAudience.Everyone, SwingAudience.Everyone,
                SwingAudience.Engaged, SwingAudience.Everyone,
            },
            frames.Select(f => f.Audience).ToArray());
    }

    [Fact]
    public void Kill_PutsTheOwnerMarkBeforeTheHitTheOwnerRewardsAfterTheDeathAndTheAttackerRewardsLast()
    {
        var ownerTaken = new byte[] { 0x01, 0x02, 0x01, 0x03 };
        var tmTotal = new byte[] { 0x00, 0x10, 0x00, 0x00, 0x00 };
        var expGained = new byte[] { 0x10, 0x00, 0x00, 0x00 };
        var levelSeen = new byte[] { 0x01, 0x03, 0x02, 0x00 };
        var additions = new SwingAdditions(
            ownerTaken,
            new[] { (Opcode.S2C_OP_011D_TBD, tmTotal) },
            new[]
            {
                (Opcode.S2C_OP_0406_TBD, expGained, SwingAudience.Attacker),
                (Opcode.S2C_OP_0028_TBD, levelSeen, SwingAudience.Everyone),
            });

        var frames = MeleeSwingFrames.Encode(Strike(damage: 68, hpAfter: 0, killed: true), BlockId, additions);

        Assert.Equal(
            new[]
            {
                (Opcode.S2C_OP_001B_TBD, MeleeSwingFrames.NoBlock, SwingAudience.Everyone),
                (Opcode.S2C_OP_001F_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_016D_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_002A_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_002B_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_011D_TBD, BlockId, SwingAudience.Owner),
                (Opcode.S2C_OP_011E_TBD, BlockId, SwingAudience.Engaged),
                (Opcode.S2C_OP_0406_TBD, BlockId, SwingAudience.Attacker),
                (Opcode.S2C_OP_0028_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_COMMAND_BLOCK_END, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_0020_TBD, MeleeSwingFrames.NoBlock, SwingAudience.Everyone),
            },
            frames.Select(f => (f.Opcode, f.BlockId, f.Audience)).ToArray());
        Assert.Same(ownerTaken, frames[2].Payload);
    }

    [Fact]
    public void OwnerRewardsAreLeftOutWhenTheMonsterLives()
    {
        var additions = new SwingAdditions(
            null,
            new[] { (Opcode.S2C_OP_011D_TBD, new byte[5]) },
            Array.Empty<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>());

        var frames = MeleeSwingFrames.Encode(Strike(damage: 32, hpAfter: 68, killed: false), BlockId, additions);

        Assert.DoesNotContain(frames, f => f.Opcode == Opcode.S2C_OP_011D_TBD);
    }

    [Fact]
    public void BlockIdsRunFromOneToTheHighestAndStartAgain()
    {
        Assert.Equal((ushort)1, MeleeSwingFrames.NextBlockId(MeleeSwingFrames.NoBlock));
        Assert.Equal((ushort)0x7FFF, MeleeSwingFrames.NextBlockId(0x7FFE));
        Assert.Equal((ushort)1, MeleeSwingFrames.NextBlockId(MeleeSwingFrames.HighestBlockId));
    }

    [Fact]
    public void BlockIdZeroIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MeleeSwingFrames.Encode(Strike(damage: 1, hpAfter: 99, killed: false), MeleeSwingFrames.NoBlock));
    }
}
