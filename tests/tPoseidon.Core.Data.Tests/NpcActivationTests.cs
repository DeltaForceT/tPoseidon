using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Tests;

public sealed class NpcActivationTests
{
    [Fact]
    public void AnNpcIsActiveUntilItIsDeactivated()
    {
        var activation = new NpcActivation();
        Assert.True(activation.IsActive(0x1000));

        Assert.True(activation.Set(0x1000, false));
        Assert.False(activation.Set(0x1000, false));
        Assert.False(activation.IsActive(0x1000));
        Assert.Equal(new ushort[] { 0x1000 }, activation.Inactive());

        Assert.True(activation.Set(0x1000, true));
        Assert.False(activation.Set(0x1000, true));
        Assert.True(activation.IsActive(0x1000));
        Assert.Empty(activation.Inactive());
    }
}
