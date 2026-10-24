using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PolarBearSkillFramesTests
{
    [Fact]
    public void DoubleHammerNamesTheTargetThenTwoTenByteHits()
    {
        var tail = PolarBearSkillFrames.HitsTail(0x8563, new[] { new PolarBearHit(139, 0), new PolarBearHit(0, 1) });

        Assert.Equal(
            new byte[]
            {
                0x63, 0x85,
                0x8B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
            },
            tail);
    }

    [Fact]
    public void OnlyDoubleHammerAndOverSwingCarryHitEntries()
    {
        Assert.Equal(2, PolarBearSkillFrames.HitsOf(PolarBearSkillFrames.DoubleHammer));
        Assert.Equal(3, PolarBearSkillFrames.HitsOf(PolarBearSkillFrames.OverSwing));
        Assert.Equal(0, PolarBearSkillFrames.HitsOf(PolarBearSkillFrames.HammerAp));
    }

    [Fact]
    public void TheGaugeStartsWithCurrentAndMaximumAndUpdatesByObject()
    {
        Assert.Equal(new byte[] { 0x0A, 0x00, 0x00, 0x00, 0xC3, 0x00, 0x00, 0x00 }, PolarBearSkillFrames.GaugeTail(10, 195));
        Assert.Equal(new byte[] { 0x10, 0x05, 0x14, 0x00, 0x00, 0x00 }, PolarBearSkillFrames.GaugeChanged(0x0510, 20));
        Assert.Equal(new byte[] { 0x01, 0x02 }, PolarBearSkillFrames.TargetTail(0x0201));
    }
}
