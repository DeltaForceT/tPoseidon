using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class UtilityFramesTests
{
    private static readonly ushort[] Ids = { 0x0102 };
    private static readonly byte[] Mask = { 0x34, 0x12, 0x01, 0x00, 0x00, 0x00 };
    private static readonly byte[] Reveal = { 0x01, 0x01, 0x00, 0x02, 0x01 };
    private static readonly byte[] Found = { 0x01, 0x00, 0x00 };
    private static readonly byte[] NotFound = { 0x00 };
    private static readonly byte[] Resisted = { 0x34, 0x12, 0x03, 0x00, 0x00, 0x00 };

    [Fact]
    public void VisibilityRevealAndDetectorFramesMatchTheServerLayouts()
    {
        Assert.Equal(Mask, SkillFrames.VisibilityMask(0x1234, 1));
        Assert.Equal(Reveal, SkillFrames.Revealed(true, Ids));
        Assert.Equal(Found, SkillFrames.DetectorTail(true));
        Assert.Equal(NotFound, SkillFrames.DetectorTail(false));
        Assert.Equal(Resisted, SkillFrames.DebuffResisted(0x1234, 0b11));
    }
}
