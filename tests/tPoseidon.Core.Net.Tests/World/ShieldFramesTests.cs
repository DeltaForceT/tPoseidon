using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ShieldFramesTests
{
    [Fact]
    public void TheShieldGaugeNamesTheObjectThenCurrentAndMax()
    {
        Assert.Equal(new byte[] { 0x34, 0x12, 0x2C, 0x01, 0, 0, 0xF4, 0x01, 0, 0 }, SkillEffectFrames.ShieldGauge(0x1234, 300, 500));
    }

    [Fact]
    public void AResistChangeIsACountedListOfKindAndValue()
    {
        Assert.Equal(new byte[] { 1, 0x0C, 34, 0 }, SkillEffectFrames.ResistChanged(new[] { ((byte)0x0C, (short)34) }));
    }

    [Fact]
    public void SeveralStatChangesShareOneRecordList()
    {
        var frame = SkillEffectFrames.StatsChanged(new[] { (5, 40), (10, 40) });

        Assert.Equal(new byte[] { 2, 5, 40, 0, 0, 0, 10, 40, 0, 0, 0 }, frame);
        Assert.Equal(SkillEffectFrames.StatChanged(5, 40), SkillEffectFrames.StatsChanged(new[] { (5, 40) }));
    }
}
