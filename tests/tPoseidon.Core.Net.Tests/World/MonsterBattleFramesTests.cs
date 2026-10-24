using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class MonsterBattleFramesTests
{
    private const ushort MonsterId = 0x0201;
    private const ushort PlayerId = 0x0301;
    private const ushort BlockId = 9;

    [Fact]
    public void Halt_IsOneUnblockedStopFrameAtTheMonsterPosition()
    {
        var frames = MonsterBattleFrames.Encode(new MonsterBattleHalted(MonsterId, 344, 248, MeleeApproach.WalkMoveType), BlockId);

        var frame = Assert.Single(frames);
        Assert.Equal((Opcode.S2C_OP_0019_TBD, MeleeSwingFrames.NoBlock), (frame.Opcode, frame.BlockId));
        Assert.Equal(new byte[] { 0x01, 0x02, 0x58, 0x01, 0xF8, 0x00, 0x01 }, frame.Payload);
    }

    [Fact]
    public void Move_CarriesItsMoveType()
    {
        var frames = MonsterBattleFrames.Encode(
            new MonsterBattleMoved(MonsterId, 344, 248, 296, 264, MeleeApproach.ChaseMoveType), BlockId);

        var frame = Assert.Single(frames);
        Assert.Equal((Opcode.ENTITY_MOVE, MeleeSwingFrames.NoBlock), (frame.Opcode, frame.BlockId));
        Assert.Equal(
            new byte[] { 0x01, 0x02, 0x58, 0x01, 0xF8, 0x00, 0x28, 0x01, 0x08, 0x01, 0x03 },
            frame.Payload);
    }

    [Fact]
    public void Swing_IsTheMotionFrameThenOneClosedBlockWithoutAMonsterHpFrame()
    {
        var frames = MonsterBattleFrames.Encode(
            new MonsterBattleSwung(MonsterId, 296, 264, PlayerId, 360, 264, 6, MeleeResolution.ResultCritical, Fatal: false),
            BlockId);

        Assert.Equal(
            new[]
            {
                (Opcode.S2C_OP_001B_TBD, MeleeSwingFrames.NoBlock),
                (Opcode.S2C_OP_001F_TBD, BlockId),
                (Opcode.S2C_OP_002A_TBD, BlockId),
                (Opcode.S2C_COMMAND_BLOCK_END, BlockId),
            },
            frames.Select(f => (f.Opcode, f.BlockId)).ToArray());
        Assert.Equal(new byte[] { 0x01, 0x02, 0x28, 0x01, 0x08, 0x01, 0x02 }, frames[0].Payload);
        Assert.Equal(
            new byte[]
            {
                0x01, 0x02, 0x01, 0x00, 0x01, 0x03,
                0x28, 0x01, 0x08, 0x01, 0x68, 0x01, 0x08, 0x01,
                0x06, 0x00, 0x03,
            },
            frames[1].Payload);
        Assert.Equal(MeleeSwingFrames.HitResultSize, frames[2].Payload.Length);
        Assert.Equal(
            new byte[] { 0x01, 0x03, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00 },
            frames[2].Payload[..14]);
        Assert.All(frames[2].Payload[14..], b => Assert.Equal(0, b));
        Assert.Empty(frames[3].Payload);
    }

    [Fact]
    public void Swing_CarriesExtraFramesInsideItsBlockBeforeTheClose()
    {
        var inside = new[] { (Opcode.S2C_OP_002B_TBD, new byte[] { 0x01, 0x03, 0x00 }) };

        var frames = MonsterBattleFrames.Encode(
            new MonsterBattleSwung(MonsterId, 296, 264, PlayerId, 360, 264, 6, 0x80, Fatal: true),
            BlockId,
            inside);

        Assert.Equal(
            new[]
            {
                (Opcode.S2C_OP_001B_TBD, MeleeSwingFrames.NoBlock),
                (Opcode.S2C_OP_001F_TBD, BlockId),
                (Opcode.S2C_OP_002A_TBD, BlockId),
                (Opcode.S2C_OP_002B_TBD, BlockId),
                (Opcode.S2C_COMMAND_BLOCK_END, BlockId),
            },
            frames.Select(f => (f.Opcode, f.BlockId)).ToArray());
        Assert.Equal(0x80, frames[1].Payload[16]);
        Assert.Equal(0x80, frames[2].Payload[10]);
    }

    [Fact]
    public void Swing_NeedsABlockId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MonsterBattleFrames.Encode(
            new MonsterBattleSwung(MonsterId, 296, 264, PlayerId, 360, 264, 6, MeleeResolution.ResultHit, Fatal: false),
            MeleeSwingFrames.NoBlock));
    }

    [Fact]
    public void Leaving_SendsTheLeaveAttackFrameForTheMonster()
    {
        var frames = MonsterBattleFrames.Encode(
            new MonsterBattleLeft(MonsterId, MonsterBattleLeaveReason.OutOfChaseRange), MeleeSwingFrames.NoBlock);

        var frame = Assert.Single(frames);
        Assert.Equal((Opcode.S2C_OP_0020_TBD, MeleeSwingFrames.NoBlock), (frame.Opcode, frame.BlockId));
        Assert.Equal(new byte[] { 0x01, 0x02 }, frame.Payload);
    }
}
