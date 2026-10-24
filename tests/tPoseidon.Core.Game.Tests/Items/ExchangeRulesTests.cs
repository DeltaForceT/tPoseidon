using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class ExchangeRulesTests
{
    private static readonly ExchangeOffer Bokken = new()
    {
        Id = 1,
        Results = new[] { 600019 },
        Requirements = new[] { new CompoundRequirement(11894, 1) },
    };

    [Fact]
    public void TheRequirementWithTimesOneMatches()
    {
        Assert.True(ExchangeRules.Matches(Bokken, new[] { new CompoundMaterial(5, 11894, 1) }, 1));
    }

    [Fact]
    public void TimesOtherThanOneIsRefused()
    {
        Assert.False(ExchangeRules.Matches(Bokken, new[] { new CompoundMaterial(5, 11894, 2) }, 2));
    }

    [Fact]
    public void AnExtraOrMissingMaterialIsRefused()
    {
        Assert.False(ExchangeRules.Matches(Bokken, new[] { new CompoundMaterial(5, 11894, 1), new CompoundMaterial(6, 1, 1) }, 1));
        Assert.False(ExchangeRules.Matches(Bokken, new[] { new CompoundMaterial(5, 777, 1) }, 1));
        Assert.False(ExchangeRules.Matches(null, new[] { new CompoundMaterial(5, 11894, 1) }, 1));
    }
}
