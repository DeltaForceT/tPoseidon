using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class DrillFramesTests
{
    private static readonly DrillSpot Spot = new(0x0100, 0x0200, 20, 10, 1);

    [Fact]
    public void TheSpotCarriesDepthsInFivesTheTokenWithLuckAndTheGauge()
    {
        Assert.Equal(new byte[] { 0x34, 0x12, 0x00, 0x01, 0x00, 0x02, 0x00, 0x04, 0x02, 0x01, 0x35, 0x12, 0x00 },
            DrillFrames.Spot(0x1234, Spot, 0x1234, true, 0));
    }

    [Fact]
    public void OthersSeeTheSpotAndTheInvincibleFlag()
    {
        Assert.Equal(new byte[] { 0x34, 0x12, 0x00, 0x01, 0x00, 0x02, 0x00, 0x04, 0x02, 0x01, 0x01 },
            DrillFrames.SeenStart(0x1234, Spot, true));
    }

    [Fact]
    public void TheResultIsCodeLifeAndItem()
    {
        Assert.Equal(new byte[] { 0x03, 0x2C, 0x01, 0xD1, 0x07, 0x00, 0x00 }, DrillFrames.Result(3, 300, 2001));
        Assert.Equal(new byte[] { 0x34, 0x12, 0x03 }, DrillFrames.SeenResult(0x1234, 3));
    }

    [Fact]
    public void TheEndRequestIsPressesUnusedDepthAndCheck()
    {
        var payload = new byte[] { 0x05, 0x00, 0x99, 0x00, 0x14, 0x00, 0x34, 0x12 };

        Assert.True(DrillFrames.TryReadEnd(payload, out var presses, out var depth, out var check));
        Assert.Equal((5, 20, (ushort)0x1234), (presses, depth, check));
        Assert.False(DrillFrames.TryReadEnd(payload.AsSpan(0, 7), out _, out _, out _));
        Assert.Equal(0, DrillFrames.CancelPresses(payload.AsSpan(0, 3)));
    }
}
