using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PortalClickFramesTests
{
    [Fact]
    public void ARequestIsFourIntegersAPointAndAMoveType()
    {
        var payload = new byte[]
        {
            0x94, 0, 0, 0,
            0x95, 0, 0, 0,
            0x03, 0, 0, 0,
            0x32, 0, 0, 0,
            0x10, 0x01,
            0x20, 0x02,
            0x04,
        };

        Assert.True(PortalClickFrames.TryRead(payload, out var request));
        Assert.Equal(148, request.Click.CurrentMap);
        Assert.Equal(149, request.Click.DestinationMap);
        Assert.Equal(3, request.Click.Region);
        Assert.Equal(50, request.Click.Level);
        Assert.Equal((ushort)0x0110, request.X);
        Assert.Equal((ushort)0x0220, request.Y);
        Assert.Equal((byte)4, request.MoveType);
    }

    [Fact]
    public void AShortRequestIsRejected()
    {
        Assert.False(PortalClickFrames.TryRead(new byte[PortalClickFrames.RequestSize - 1], out _));
    }

    [Fact]
    public void TheResultIsTheCodeTheRegionAndAZero()
    {
        Assert.Equal(new byte[] { 5, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0 }, PortalClickFrames.Result(5, 3));
    }
}
