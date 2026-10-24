using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ObjectHitFramesTests
{
    private static readonly ushort[] Victims = { 0x1234, 0x0056 };
    private static readonly byte[] Expected = { 0x21, 0x00, 0x02, 0x34, 0x12, 0x56, 0x00 };

    [Fact]
    public void ObjectVictimsListsTheObjectThenAByteCountThenIds()
    {
        Assert.Equal(Expected, SkillFrames.ObjectVictims(0x0021, Victims));
    }
}
