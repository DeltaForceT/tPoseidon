using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Combat;

public sealed class MeleeApproachTests
{
    private static readonly CharacterCombatInfo Reach = new()
    {
        Id = 1,
        MinAttackRange = 50,
        MaxAttackRange = 70,
        ChaseRange = 300,
        WalkSpeed = 0,
        RunSpeed = 0,
        AttackMs = 600,
        Attack2Ms = 1050,
        AttackReadyMs = 240,
    };

    private static readonly Random Rng = new(5);

    [Theory]
    [InlineData(1060)]
    [InlineData(1050)]
    [InlineData(1070)]
    public void BetweenMinimumAndMaximumRangeIsInRange(int attackerX)
    {
        var decision = MeleeApproach.Decide(Reach, attackerX, 1000, 1000, 1000, afterFirstSwing: false, Rng);

        Assert.Equal(MeleeApproachKind.InRange, decision.Kind);
    }

    [Fact]
    public void TooFarWalksToTheApproachRadiusOnTheLineToTheAttacker()
    {
        var decision = MeleeApproach.Decide(Reach, 1200, 1000, 1000, 1000, afterFirstSwing: false, Rng);

        Assert.Equal(new MeleeApproachDecision(MeleeApproachKind.Move, 1060, 1000), decision);
    }

    [Fact]
    public void TooCloseBacksOffToTheApproachRadius()
    {
        var decision = MeleeApproach.Decide(Reach, 1000, 1010, 1000, 1000, afterFirstSwing: false, Rng);

        Assert.Equal(new MeleeApproachDecision(MeleeApproachKind.BackOff, 1000, 1060), decision);
    }

    [Fact]
    public void StandingOnTheTargetPicksAPointAtTheApproachRadius()
    {
        var decision = MeleeApproach.Decide(Reach, 1000, 1000, 1000, 1000, afterFirstSwing: false, Rng);

        Assert.Equal(MeleeApproachKind.BackOff, decision.Kind);
        var distance = Math.Sqrt(Math.Pow(decision.X - 1000, 2) + Math.Pow(decision.Y - 1000, 2));
        Assert.InRange(distance, Reach.ApproachRadius - Math.Sqrt(2), Reach.ApproachRadius);
    }

    [Fact]
    public void BeyondTheChaseRangeIsOnlyGivenUpAfterTheFirstSwing()
    {
        var first = MeleeApproach.Decide(Reach, 1400, 1000, 1000, 1000, afterFirstSwing: false, Rng);
        var later = MeleeApproach.Decide(Reach, 1400, 1000, 1000, 1000, afterFirstSwing: true, Rng);

        Assert.Equal(MeleeApproachKind.Move, first.Kind);
        Assert.Equal(MeleeApproachKind.GiveUp, later.Kind);
    }
}
