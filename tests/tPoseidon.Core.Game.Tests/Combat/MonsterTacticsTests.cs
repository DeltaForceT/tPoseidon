using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Combat;

public sealed class MonsterTacticsTests
{
    private static MonsterTacticEntry Tactic(int id, int factorType, int factorValue, int ratio = 100, int weight = 10, int option = 0, int time = 5) => new()
    {
        Id = id, TacticsName = "Mon_Test", FactorType = factorType, FactorValue = factorValue, ActionType = MonsterTactics.CastActionType,
        SkillId = 5100 + id, SkillLevel = 1, TimeSeconds = time, ActionRatio = ratio, RandomWeight = weight, Option = option,
    };

    [Fact]
    public void FactorsTestTheTargetOrTheMonsterItself()
    {
        var target = new TacticTarget(Level: 50, Job: 2, Hp: 300, MaxHp: 1000);

        Assert.True(MonsterTactics.Factor(Tactic(1, 9, 500), null, 400));
        Assert.False(MonsterTactics.Factor(Tactic(1, 9, 500), null, 600));
        Assert.True(MonsterTactics.Factor(Tactic(1, 8, 500), null, 600));
        Assert.True(MonsterTactics.Factor(Tactic(1, 1, 40), target, 0));
        Assert.True(MonsterTactics.Factor(Tactic(1, 3, 3), target, 0));
        Assert.True(MonsterTactics.Factor(Tactic(1, 7, 31), target, 0));
        Assert.False(MonsterTactics.Factor(Tactic(1, 6, 31), target, 0));
        Assert.False(MonsterTactics.Factor(Tactic(1, 1, 40), null, 0));
    }

    [Fact]
    public void ATacticArmsOnTheSharedRollAndIsPickedByWeightOnce()
    {
        var tactics = new MonsterTactics(new[] { Tactic(1, 9, 1000, ratio: 50, weight: 10), Tactic(2, 9, 1000, ratio: 20, weight: 30) });

        tactics.Rearm(40, null, 100);
        Assert.True(tactics.Usable);
        var picked = tactics.Pick(sum => sum - 1);

        Assert.Equal(1, picked!.Id);
        Assert.Null(tactics.Pick(sum => 0));
    }

    [Fact]
    public void APickedTacticCoolsDownForItsTime()
    {
        var tactics = new MonsterTactics(new[] { Tactic(1, 9, 1000, time: 2) });
        tactics.Rearm(0, null, 100);
        Assert.NotNull(tactics.Pick(_ => 0));

        tactics.Rearm(0, null, 100);
        Assert.Null(tactics.Pick(_ => 0));
        tactics.Advance(2001);
        tactics.Rearm(0, null, 100);
        Assert.NotNull(tactics.Pick(_ => 0));
    }

    [Fact]
    public void TheForcedOptionWinsAndRearmingRunsOncePerSecond()
    {
        var tactics = new MonsterTactics(new[] { Tactic(1, 9, 1000, weight: 100), Tactic(2, 9, 1000, option: MonsterTactics.ForcedPickOption, weight: 1) });
        tactics.Rearm(0, null, 100);

        Assert.Equal(2, tactics.Pick(_ => 0)!.Id);
        Assert.False(tactics.Advance(600));
        Assert.True(tactics.Advance(500));
    }
}
