using tPoseidon.Core.Game.Warp;

namespace tPoseidon.Core.Game.Tests.Warp;

public sealed class MemoryPortRulesTests
{
    [Fact]
    public void MapOptionBitsRefuseSavingAndWarping()
    {
        Assert.True(MemoryPortRules.RefusesSave(0x10 | 0x2));
        Assert.False(MemoryPortRules.RefusesSave(0x80));
        Assert.True(MemoryPortRules.RefusesWarp(0x80));
        Assert.False(MemoryPortRules.RefusesWarp(0x10));
    }

    [Fact]
    public void OnlyEffectThirtyFourWithTheCustomDataOptionIsAMemoryPort()
    {
        Assert.True(MemoryPortRules.IsMemoryPort(34, 2 | 2048 | 4096 | 32768 | 262144));
        Assert.False(MemoryPortRules.IsMemoryPort(34, 2));
        Assert.False(MemoryPortRules.IsMemoryPort(14, 32768));
    }
}
