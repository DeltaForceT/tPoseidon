using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class SelfBuffTests
{
    private static readonly int[] Bases = { 400, 50, 12, 30, 20, 10, 5, 8, 4, 900, 120, 60 };

    private static SkillEffect Effect(int actionId, float amount) => new()
    {
        ObjectId = 0x500, Kind = SkillEffectKinds.Find(actionId)!, SkillId = 1, EsClass = 0, CasterEntityId = 1, OwnerEntityId = 1,
        Amount = amount, DurationMs = 20000, Mode = SkillEffectMode.Applied,
    };

    [Fact]
    public void BerserkRaisesApByBaseTimesRatioMinusOne()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(Effect(SkillEffectKinds.Berserk, 1.2f));

        Assert.Equal((int)(400 * (double)1.2f - 400), book.Deltas(Bases, _ => null)[SkillEffectKinds.StatAp]);
        Assert.Equal(SkillEffectTail.Empty, SkillEffectKinds.Find(SkillEffectKinds.Berserk)!.Tail);
        Assert.False(SkillEffectKinds.Find(SkillEffectKinds.Berserk)!.CarriesMode);
    }

    [Fact]
    public void DeadAliveOnlyAddsApWhileActiveAndShrinksWithLife()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        var deadAlive = Effect(SkillEffectKinds.DeadAlive, 0.94f);
        book.Register(deadAlive);

        Assert.Equal(0, book.Deltas(Bases, _ => null)[SkillEffectKinds.StatAp]);

        deadAlive.Active = true;
        deadAlive.LifeRatio = 0.1;

        Assert.Equal((int)(float)(400 * ((double)0.94f - 2.5 * 0.1)), book.Deltas(Bases, _ => null)[SkillEffectKinds.StatAp]);
        Assert.True(SkillEffectKinds.Find(SkillEffectKinds.DeadAlive)!.SilentStart);
    }

    [Fact]
    public void SaintOathSpreadsSaintPromiseToSixMembersInItsRadius()
    {
        var oath = SkillEffectKinds.Find(SkillEffectKinds.SaintOath)!;

        Assert.Equal(SkillEffectKinds.SaintOathPromise, oath.SpreadsAs);
        Assert.Equal("Radius", oath.SpreadRadiusColumn);
        Assert.Equal(6, oath.SpreadLimit);
        Assert.Equal(SkillPeriodicHit.HealOverTime, SkillEffectKinds.Find(SkillEffectKinds.SaintOathPromise)!.Periodic);
    }

    [Fact]
    public void AnEffectAskedToEndEndsAtItsNextTick()
    {
        var effect = Effect(SkillEffectKinds.Berserk, 1.2f);
        effect.EndNow();

        Assert.Equal(SkillEffectTick.Ended, effect.Tick(100));
    }

    [Fact]
    public void BloodyOathRaisesDarkMagicDamageOnly()
    {
        var attacker = new SkillAttacker(0, 0, 0, 0, 1, DarkBonus: 0.15f);

        Assert.Equal(1000 + (int)(1000 * (double)0.15f), SkillDamage.WithDarkBonus(1000, attacker, SkillActions.DarkElement));
        Assert.Equal(1000, SkillDamage.WithDarkBonus(1000, attacker, SkillActions.FireElement));
    }

    [Fact]
    public void EnergySwordAddsItsRangeToReachApproachAndChase()
    {
        var reach = new CharacterCombatInfo
        {
            Id = 1, WalkSpeed = 100f, RunSpeed = 180f, AttackMs = 500, AttackReadyMs = 100, Attack2Ms = 600,
            MinAttackRange = 20, MaxAttackRange = 80, ChaseRange = 200,
        };

        var longer = reach.WithRangeBonus(140);

        Assert.Equal(220, longer.MaxAttackRange);
        Assert.Equal(340, longer.ChaseRange);
        Assert.Equal(20, longer.MinAttackRange);
        Assert.Equal(reach.ApproachRadius + 140, longer.ApproachRadius);
    }
}
