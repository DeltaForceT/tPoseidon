using tPoseidon.Core.Game.Characters;

namespace tPoseidon.Core.Game.Tests.Characters;

public sealed class RunRulesTests
{
    [Theory]
    [InlineData(0, 1000, true)]
    [InlineData(899, 1000, true)]
    [InlineData(900, 1000, false)]
    [InlineData(1000, 1000, false)]
    [InlineData(5, 0, false)]
    [InlineData(0, 0, false)]
    [InlineData(-1000, 1000, false)]
    public void RunningIsAllowedBelowNinetyPercentOfTheMaximumWeight(long carried, long max, bool allowed)
    {
        Assert.Equal(allowed, RunRules.CanRun(carried, max));
    }

    [Fact]
    public void ARunRequestBecomesAWalkWhenTheBagIsHeavy()
    {
        Assert.Equal(1, RunRules.Allowed(2, 900, 1000));
    }

    [Fact]
    public void ARunRequestStaysARunWhenTheBagIsLight()
    {
        Assert.Equal(2, RunRules.Allowed(2, 899, 1000));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public void OtherMoveTypesAreNeverChanged(byte requested)
    {
        Assert.Equal(requested, RunRules.Allowed(requested, 1000, 1000));
    }

    [Fact]
    public void TheWholePercentIsTruncatedBeforeTheComparison()
    {
        Assert.True(RunRules.CanRun(8999, 10000));
        Assert.False(RunRules.CanRun(9000, 10000));
    }
}
