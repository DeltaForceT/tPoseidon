using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class MonsterWanderFramesTests
{
    [Fact]
    public void MoveStarted_IsAnEntityMoveFrameWithTheWalkType()
    {
        var frame = MonsterWanderFrames.Encode(new MonsterMoveStarted(0x0201, 328, 328, 408, 232));

        Assert.Equal(Opcode.ENTITY_MOVE, frame.Opcode);
        Assert.Equal(
            new byte[] { 0x01, 0x02, 0x48, 0x01, 0x48, 0x01, 0x98, 0x01, 0xE8, 0x00, 0x01 },
            frame.Payload);
    }

    [Fact]
    public void MoveRefused_IsAHaltFrameAtTheCurrentPosition()
    {
        var frame = MonsterWanderFrames.Encode(new MonsterMoveRefused(0x0201, 328, 232));

        Assert.Equal(Opcode.S2C_OP_0019_TBD, frame.Opcode);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x48, 0x01, 0xE8, 0x00, 0x01 }, frame.Payload);
    }

    [Fact]
    public void Arrived_IsTheThreeByteMoveFinishedFrame()
    {
        var frame = MonsterWanderFrames.Encode(new MonsterArrived(0x0201));

        Assert.Equal(Opcode.ENTITY_EMOTE, frame.Opcode);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x00 }, frame.Payload);
    }
}
