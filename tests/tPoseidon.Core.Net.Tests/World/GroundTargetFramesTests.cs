using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class GroundTargetFramesTests
{
    [Fact]
    public void ARegionTargetCarriesItsPoint()
    {
        var request = new SkillTargetRequest(2, 2009, 3, SkillFrames.RegionTargetType, new byte[] { 0x10, 0x01, 0x20, 0x02 });

        Assert.Equal(((ushort)0x110, (ushort)0x220), request.TargetPoint);
        Assert.Null(new SkillTargetRequest(2, 2009, 3, SkillFrames.ObjectTargetType, new byte[] { 0x10, 0x01, 0x20, 0x02 }).TargetPoint);
    }

    [Fact]
    public void ACastStartedAtAPointUsesTargetTypeTwo()
    {
        var frame = SkillFrames.CastStartedAt(0x1234, 2009, 3, (0x110, 0x220));

        Assert.Equal(new byte[] { 0x34, 0x12, 2, 0xD9, 0x07, 3, 2, 0x10, 0x01, 0x20, 0x02 }, frame);
    }
}
