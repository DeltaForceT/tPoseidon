using tPoseidon.Core.Game.Combat;

namespace tPoseidon.Core.Game.Tests.Combat;

public sealed class MeleeResolutionTests
{
    private const int Hits = 0;
    private const int Fails = 999;

    private static MeleeOutcome Resolve(byte armed, int targetHp, params int[] draws)
        => MeleeResolution.PlayerAgainstMonster(
            attackerAp: 40, attackerAc: 60, attackerLk: 10, armed,
            targetDp: 8, targetHv: 10, targetLk: 10, targetHp, new ScriptedRandom(draws));

    [Fact]
    public void DamageFormulasHaveNoRandomSpread()
    {
        Assert.Equal(34, MeleeResolution.Normal(40, 8));
        Assert.Equal(54, MeleeResolution.Critical(40, 8));
        Assert.Equal(70, MeleeResolution.StrongCritical(40, 8));
        Assert.Equal(0, MeleeResolution.Normal(3, 100));
        Assert.Equal(1, MeleeResolution.Critical(3, 100));
    }

    [Fact]
    public void PlainHit()
    {
        var outcome = Resolve(MeleeResolution.NotArmed, targetHp: 100, Hits, Fails, Fails, 99);

        Assert.Equal(new MeleeOutcome(34, MeleeResolution.ResultHit, MeleeResolution.NotArmed), outcome);
    }

    [Fact]
    public void HitThatEmptiesTheTargetCarriesTheFinishingFlag()
    {
        var outcome = Resolve(MeleeResolution.NotArmed, targetHp: 34, Hits, Fails, Fails, 99);

        Assert.Equal((byte)(MeleeResolution.ResultHit | MeleeResolution.ResultFinishingFlag), outcome.Result);
    }

    [Fact]
    public void MissCanArmACriticalForTheNextSwing()
    {
        var outcome = Resolve(MeleeResolution.NotArmed, targetHp: 100, Fails, Fails, Fails, 0);

        Assert.Equal(new MeleeOutcome(0, MeleeResolution.ResultMiss, MeleeResolution.ResultCritical), outcome);
    }

    [Fact]
    public void ArmedCriticalLandsWithoutNeedingTheHitRoll()
    {
        var outcome = Resolve(MeleeResolution.ResultCritical, targetHp: 100, Fails, Fails, Fails);

        Assert.Equal(new MeleeOutcome(54, MeleeResolution.ResultCritical, MeleeResolution.NotArmed), outcome);
    }

    [Fact]
    public void EvadeAndBlockDealNoDamage()
    {
        var evaded = Resolve(MeleeResolution.NotArmed, targetHp: 100, Hits, Hits, Fails);
        var blocked = Resolve(MeleeResolution.NotArmed, targetHp: 100, Hits, Fails, Hits);

        Assert.Equal(new MeleeOutcome(0, MeleeResolution.ResultMiss, MeleeResolution.NotArmed), evaded);
        Assert.Equal(new MeleeOutcome(0, MeleeResolution.ResultBlock, MeleeResolution.NotArmed), blocked);
    }

    [Fact]
    public void CriticalAndStrongCriticalRolls()
    {
        var critical = Resolve(MeleeResolution.NotArmed, targetHp: 100, Hits, Fails, Fails, 0, 99);
        var strong = Resolve(MeleeResolution.NotArmed, targetHp: 100, Hits, Fails, Fails, 0, 0);

        Assert.Equal(new MeleeOutcome(54, MeleeResolution.ResultCritical, MeleeResolution.NotArmed), critical);
        Assert.Equal(new MeleeOutcome(70, MeleeResolution.ResultStrongCritical, MeleeResolution.NotArmed), strong);
    }

    private static readonly int[] HitThenNoEvadeNoBlock = { 0, 99, 99 };

    private static MeleeOutcome MonsterResolve(bool levelScaled, int attackerAc, int targetLevel, params int[] draws)
        => MeleeResolution.MonsterAgainstPlayer(
            attackerAp: 10, attackerAc, attackerLk: 0, attackerLevel: 10, levelScaled,
            targetDp: 8, targetHv: 20, targetLk: 10, targetLevel, targetHp: 100, targetProtected: false,
            new ScriptedRandom(draws));

    [Fact]
    public void MonsterPlainHitUsesPerCentRolls()
    {
        var outcome = MonsterResolve(levelScaled: false, attackerAc: 50, targetLevel: 10, 49, 20, 10, 30);

        Assert.Equal(new MeleeOutcome(4, MeleeResolution.ResultHit, MeleeResolution.NotArmed), outcome);
    }

    [Fact]
    public void MonsterMissNeverArmsACritical()
    {
        var outcome = MonsterResolve(levelScaled: false, attackerAc: 50, targetLevel: 10, 50, 99, 99);

        Assert.Equal(new MeleeOutcome(0, MeleeResolution.ResultMiss, MeleeResolution.NotArmed), outcome);
    }

    [Fact]
    public void MonsterEvadeBlockAndCritical()
    {
        var evaded = MonsterResolve(levelScaled: false, attackerAc: 50, targetLevel: 10, 0, 19, 99);
        var blocked = MonsterResolve(levelScaled: false, attackerAc: 50, targetLevel: 10, 0, 20, 9);
        var critical = MonsterResolve(levelScaled: false, attackerAc: 50, targetLevel: 10, 0, 20, 10, 29, 0);

        Assert.Equal(new MeleeOutcome(0, MeleeResolution.ResultMiss, MeleeResolution.NotArmed), evaded);
        Assert.Equal(new MeleeOutcome(0, MeleeResolution.ResultBlock, MeleeResolution.NotArmed), blocked);
        Assert.Equal(new MeleeOutcome(9, MeleeResolution.ResultCritical, MeleeResolution.NotArmed), critical);
    }

    [Fact]
    public void LevelScaledAccuracyRollsOnlyTheHitWithLevelsAndEvasionTakenOff()
    {
        var lands = MonsterResolve(levelScaled: true, attackerAc: 60, targetLevel: 30, 11, 99);
        var misses = MonsterResolve(levelScaled: true, attackerAc: 60, targetLevel: 30, 12);

        Assert.Equal(new MeleeOutcome(4, MeleeResolution.ResultHit, MeleeResolution.NotArmed), lands);
        Assert.Equal(new MeleeOutcome(0, MeleeResolution.ResultMiss, MeleeResolution.NotArmed), misses);
    }

    [Fact]
    public void ProtectedTargetBlocksEveryBlowAfterTheRolls()
    {
        var outcome = MeleeResolution.MonsterAgainstPlayer(
            attackerAp: 10, attackerAc: 50, attackerLk: 0, attackerLevel: 10, levelScaledAccuracy: false,
            targetDp: 8, targetHv: 20, targetLk: 10, targetLevel: 10, targetHp: 100, targetProtected: true,
            new ScriptedRandom(HitThenNoEvadeNoBlock));

        Assert.Equal(new MeleeOutcome(0, MeleeResolution.ResultBlock, MeleeResolution.NotArmed), outcome);
    }

    [Fact]
    public void LevelScaledAccuracyNeverDropsBelowTenPerCent()
    {
        var lands = MonsterResolve(levelScaled: true, attackerAc: 20, targetLevel: 30, 9, 99);
        var misses = MonsterResolve(levelScaled: true, attackerAc: 20, targetLevel: 30, 10);

        Assert.Equal(MeleeResolution.ResultHit, lands.Result);
        Assert.Equal(MeleeResolution.ResultMiss, misses.Result);
    }

    private sealed class ScriptedRandom : Random
    {
        private readonly Queue<int> _draws;

        public ScriptedRandom(int[] draws)
        {
            _draws = new Queue<int>(draws);
        }

        public override int Next(int maxValue) => _draws.Dequeue();
    }
}
