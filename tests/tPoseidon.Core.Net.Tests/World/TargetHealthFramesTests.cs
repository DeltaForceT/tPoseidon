using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class TargetHealthFramesTests
{
    [Fact]
    public void RequestIsOneEntityId()
    {
        Assert.True(TargetHealthFrames.TryReadRequest([0x34, 0x12], out var entityId));
        Assert.Equal(0x1234, entityId);
        Assert.False(TargetHealthFrames.TryReadRequest([0x34], out _));
    }

    [Fact]
    public void HealthIsIdThenMaxHpThenHp()
    {
        Assert.Equal(
            new byte[] { 0x34, 0x12, 0x10, 0x27, 0, 0, 0xE8, 3, 0, 0 },
            TargetHealthFrames.Health(0x1234, 10000, 1000));
        Assert.Equal(TargetHealthFrames.HealthSize, TargetHealthFrames.Health(1, 1, 1).Length);
    }

    [Fact]
    public void HealthNeverGoesNegativeOrPastASignedDword()
    {
        Assert.Equal(
            new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            TargetHealthFrames.Health(1, -5, -1));
        Assert.Equal(
            new byte[] { 1, 0, 0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0x7F },
            TargetHealthFrames.Health(1, long.MaxValue, 0x80000000L));
    }
}
