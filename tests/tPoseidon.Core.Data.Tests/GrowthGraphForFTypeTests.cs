using tPoseidon.Core.Data;

namespace tPoseidon.Core.DataTests;

public class GrowthGraphForFTypeTests
{
    [Fact]
    public void Power_LocksPower_OthersBalanced()
    {
        var (p, m, s, c) = SpeciesDefaults.GrowthGraphForFType(FType.Power);
        Assert.Equal(SpeciesDefaults.LockedPts, p);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, m);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, s);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, c);
    }

    [Fact]
    public void Magic_LocksMagic_OthersBalanced()
    {
        var (p, m, s, c) = SpeciesDefaults.GrowthGraphForFType(FType.Magic);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, p);
        Assert.Equal(SpeciesDefaults.LockedPts, m);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, s);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, c);
    }

    [Fact]
    public void Sense_LocksSense_OthersBalanced()
    {
        var (p, m, s, c) = SpeciesDefaults.GrowthGraphForFType(FType.Sense);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, p);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, m);
        Assert.Equal(SpeciesDefaults.LockedPts, s);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, c);
    }

    [Fact]
    public void Charm_LocksCharm_OthersBalanced()
    {
        var (p, m, s, c) = SpeciesDefaults.GrowthGraphForFType(FType.Charm);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, p);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, m);
        Assert.Equal(SpeciesDefaults.BalancedSecondary, s);
        Assert.Equal(SpeciesDefaults.LockedPts, c);
    }

    [Fact]
    public void Defaults_SumToFixedBudget()
    {
        foreach (var ftype in new[] { FType.Power, FType.Magic, FType.Sense, FType.Charm })
        {
            var (p, m, s, c) = SpeciesDefaults.GrowthGraphForFType(ftype);
            Assert.Equal(SpeciesDefaults.TotalBudget, p + m + s + c);
        }
    }

    [Fact]
    public void Defaults_AreAlwaysValid()
    {
        foreach (var ftype in new[] { FType.Power, FType.Magic, FType.Sense, FType.Charm })
        {
            var (p, m, s, c) = SpeciesDefaults.GrowthGraphForFType(ftype);
            Assert.True(SpeciesDefaults.IsValidGrowthGraph(ftype, p, m, s, c),
                $"Default for {ftype} ({p}/{m}/{s}/{c}) failed validation");
        }
    }

    [Theory]
    [InlineData(FType.Power, 4, 1, 1, 4, true)]
    [InlineData(FType.Power, 4, 4, 1, 1, true)]
    [InlineData(FType.Power, 4, 2, 2, 2, true)]
    [InlineData(FType.Power, 4, 1, 2, 3, true)]
    [InlineData(FType.Power, 4, 3, 2, 1, true)]
    [InlineData(FType.Power, 3, 1, 2, 4, false)]
    [InlineData(FType.Power, 4, 0, 2, 4, false)]
    [InlineData(FType.Power, 4, 5, 1, 0, false)]
    [InlineData(FType.Power, 4, 1, 1, 1, false)]
    [InlineData(FType.Power, 4, 4, 4, 4, false)]
    [InlineData(FType.Magic, 1, 4, 4, 1, true)]
    [InlineData(FType.Magic, 1, 4, 3, 2, true)]
    [InlineData(FType.Magic, 4, 4, 1, 1, true)]
    [InlineData(FType.Magic, 1, 3, 4, 2, false)]
    [InlineData(FType.Sense, 2, 1, 4, 3, true)]
    [InlineData(FType.Sense, 4, 1, 4, 1, true)]
    [InlineData(FType.Charm, 1, 1, 4, 4, true)]
    [InlineData(FType.Charm, 2, 2, 2, 4, true)]
    [InlineData(FType.Charm, 1, 1, 1, 4, false)]
    [InlineData(FType.Charm, 1, 1, 1, 5, false)]
    public void IsValidGrowthGraph_EnforcesLockRangeAndBudget(FType ftype, byte p, byte m, byte s, byte c, bool expected)
    {
        Assert.Equal(expected, SpeciesDefaults.IsValidGrowthGraph(ftype, p, m, s, c));
    }
}
