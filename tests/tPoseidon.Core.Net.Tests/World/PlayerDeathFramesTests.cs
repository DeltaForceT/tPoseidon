using tPoseidon.Core.Common;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PlayerDeathFramesTests
{
    private const ushort PlayerId = 0x0301;

    [Fact]
    public void Victim_GetsTheExpValueThenTheHaltAndTheDeath()
    {
        var frames = PlayerDeathFrames.ForVictim(PlayerId, 360, 264, moveType: 2, wasAttacking: false, exp: 980);

        Assert.Equal(
            new[] { Opcode.S2C_EXP_VALUE, Opcode.S2C_OP_0019_TBD, Opcode.S2C_OP_002B_TBD },
            frames.Select(f => f.Opcode).ToArray());
        Assert.Equal(new byte[] { 0xD4, 0x03, 0x00, 0x00 }, frames[0].Payload);
        Assert.Equal(new byte[] { 0x01, 0x03, 0x68, 0x01, 0x08, 0x01, 0x02 }, frames[1].Payload);
        Assert.Equal(new byte[] { 0x01, 0x03, 0x00 }, frames[2].Payload);
    }

    [Fact]
    public void Others_DoNotGetTheExpValue()
    {
        var frames = PlayerDeathFrames.ForOthers(PlayerId, 360, 264, moveType: 1, wasAttacking: false);

        Assert.Equal(
            new[] { Opcode.S2C_OP_0019_TBD, Opcode.S2C_OP_002B_TBD },
            frames.Select(f => f.Opcode).ToArray());
        Assert.Equal(0x01, frames[0].Payload[6]);
    }

    [Fact]
    public void PlayerWhoWasAttackingAlsoLeavesTheAttackState()
    {
        var frames = PlayerDeathFrames.ForOthers(PlayerId, 360, 264, moveType: 2, wasAttacking: true);

        Assert.Equal(
            new[] { Opcode.S2C_OP_0019_TBD, Opcode.S2C_OP_002B_TBD, Opcode.S2C_OP_0020_TBD },
            frames.Select(f => f.Opcode).ToArray());
        Assert.Equal(new byte[] { 0x01, 0x03 }, frames[2].Payload);
    }
}
