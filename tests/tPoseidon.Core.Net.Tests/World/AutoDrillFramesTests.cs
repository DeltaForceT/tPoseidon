using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class AutoDrillFramesTests
{
    private static readonly byte[] PlayerBytes = [0x34, 0x12];
    private static readonly byte[] ErrorBytes = [10, 0];

    [Fact]
    public void PlayerAndErrorAreOneWord()
    {
        Assert.Equal(PlayerBytes, AutoDrillFrames.Player(0x1234));
        Assert.Equal(ErrorBytes, AutoDrillFrames.Refused(10));
    }
}
