using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class CrazyHammerFramesTests
{
    [Fact]
    public void TheGaugeIsTheCrazyHammerEffectWithCurrentThenMax()
    {
        var frame = CrazyHammerFrames.Gauge(0x1234, 0x0567, 0, 21, 195);

        Assert.Equal(SkillFrames.EffectHeaderSize + CrazyHammerFrames.GaugeTailSize, frame.Length);
        Assert.Equal(0x1234, BitConverter.ToUInt16(frame, 0));
        Assert.Equal(0x1234, BitConverter.ToUInt16(frame, 2));
        Assert.Equal(0x0567, BitConverter.ToUInt16(frame, 4));
        Assert.Equal(1532, BitConverter.ToUInt16(frame, 6));
        Assert.Equal(4801, BitConverter.ToUInt16(frame, 10));
        Assert.Equal(21, BitConverter.ToInt32(frame, 14));
        Assert.Equal(195, BitConverter.ToInt32(frame, 18));
    }

    [Fact]
    public void TheGaugeUpdateNamesTheEffectAndTheNewValue()
    {
        var frame = CrazyHammerFrames.GaugeUpdate(0x0567, 24);

        Assert.Equal(new byte[] { 0x67, 0x05, 24, 0, 0, 0 }, frame);
    }
}
