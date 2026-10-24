using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class DisguiseFramesTests
{
    [Fact]
    public void DisguisedIsEntityThenThirtyTwoBitType()
    {
        Assert.Equal(new byte[] { 0x34, 0x12, 0x1B, 0x08, 0, 0 }, DisguiseFrames.Disguised(0x1234, 2075));
    }

    [Fact]
    public void RemovedIsEntityThenEffectByte()
    {
        Assert.Equal(new byte[] { 0x34, 0x12, 0 }, DisguiseFrames.Removed(0x1234, 0));
    }
}
