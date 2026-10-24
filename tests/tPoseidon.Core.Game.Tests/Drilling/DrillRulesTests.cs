using tPoseidon.Core.Game.Drilling;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Drilling;

public sealed class DrillRulesTests
{
    private static readonly DrillParamEntry Drill = new(8001, 1, 1, 30, 70, 100, 1, 300);

    [Fact]
    public void WearDoublesOnAnotherGroundNature()
    {
        Assert.Equal(290, DrillRules.Wear(300, 10, 1, 1));
        Assert.Equal(280, DrillRules.Wear(300, 10, 1, 2));
        Assert.Equal(290, DrillRules.Wear(300, 10, 0, 4));
        Assert.Equal(0, DrillRules.Wear(5, 10, 1, 1));
    }

    [Fact]
    public void TheCheckUndoesTheClientChecksum()
    {
        const ushort token = 0x1234 & DrillRules.TokenMask;
        var checksum = (2 << 12) ^ (20 << 8) ^ (Drill.ScoopMass << 5) ^ (Drill.BarSpeed << 1) ^ Drill.BarEffArea;
        var reported = (ushort)(token ^ checksum);

        Assert.Equal(token, DrillRules.Check(reported, Drill, 20, 2));
    }

    [Fact]
    public void TheMinimumTimeMatchesTheServerExample()
    {
        Assert.Equal(2226, DrillRules.MinimumMilliseconds(Drill, 20, 20));
    }

    [Theory]
    [InlineData(null, 0, 20, 20, DrillResult.Nothing)]
    [InlineData(20, 0, 20, 20, DrillResult.Nothing)]
    [InlineData(20, 0, 15, 20, DrillResult.TooShallow)]
    [InlineData(20, 2001, 25, 30, DrillResult.Nothing)]
    [InlineData(20, 2001, 15, 30, DrillResult.TooShallow)]
    [InlineData(20, 2001, 20, 30, DrillResult.Found)]
    public void EndResultsFollowTheOutcomeMatrix(int? laid, int rolled, int depth, int mapDepth, byte expected)
    {
        Assert.Equal(expected, DrillRules.EndResult(laid, rolled, depth, mapDepth));
    }

    [Fact]
    public void DiggingPastTheItemGivesNoExp()
    {
        Assert.False(DrillRules.GivesEndExp(20, 2001, 25));
        Assert.True(DrillRules.GivesEndExp(null, 0, 25));
        Assert.True(DrillRules.GivesEndExp(20, 2001, 20));
    }

    [Fact]
    public void ExpFormulasMatchTheServer()
    {
        var rates = new[] { 1f, 6f, 12f };

        Assert.Equal(30, DrillRules.StartExp(new DrillZoneEntry(17, 10, 20, 60), 0));
        Assert.Equal(25, DrillRules.EndExp(rates, 1, 20, 20));
        Assert.Equal(1, DrillRules.EndExp(rates, 0, 1, 1));
        Assert.Equal(320, DrillRules.FindTmExp(20, 20, 1));
        Assert.Equal(12f, DrillRules.GradeRate(rates, 5));
        Assert.Equal(0f, DrillRules.GradeRate(rates, 3));
    }
}
