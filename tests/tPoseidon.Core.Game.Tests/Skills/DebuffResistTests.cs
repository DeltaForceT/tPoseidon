using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class DebuffResistTests
{
    private static readonly int[] Boss = { 100, 100, 100, 50, 50 };
    private static readonly int[] Open = { 0, 0 };
    private static readonly int[] Mixed = { 100, 0 };

    [Fact]
    public void ImmuneBitsBlockAndVulnerableBitsLetThrough()
    {
        Assert.Equal((true, 0b11u), DebuffResist.Roll(Boss, 0b11, () => 0));
        Assert.Equal((false, 0u), DebuffResist.Roll(Open, 0b1, () => 0));
        Assert.Equal((false, 0b1u), DebuffResist.Roll(Mixed, 0b11, () => 0));
    }

    [Fact]
    public void AHalfValueBlocksOnRollsAtOrAboveIt()
    {
        Assert.Equal((false, 0u), DebuffResist.Roll(Boss, 0b1000, () => 49));
        Assert.Equal((true, 0b1000u), DebuffResist.Roll(Boss, 0b1000, () => 50));
    }

    [Fact]
    public void ASkillWithoutDebuffBitsIsNeverBlockedByTheTable()
    {
        Assert.Equal((true, 0u), DebuffResist.Roll(Boss, 0, () => 0));
    }
}
