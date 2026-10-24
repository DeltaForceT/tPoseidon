using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class StatusEffectTests
{
    private static readonly int[] Bases = { 400, 50, 12, 30, 20, 10, 5, 8, 40, 900, 120, 60 };

    [Fact]
    public void AdhesiveLowersAvoidLuckAndDefenceByTheCastersLuck()
    {
        var kind = SkillEffectKinds.Find(SkillEffectKinds.AdhesiveOnMonster)!;
        var book = new SkillEffectBook(SkillEffectBook.MonsterCapacities);
        book.Register(new SkillEffect
        {
            ObjectId = 1, Kind = kind, SkillId = 3106, EsClass = 1, CasterEntityId = 7, OwnerEntityId = 2,
            Amount = 0.34f, SecondAmount = 0.34f, ThirdAmount = 0.34f, DurationMs = 20000, Mode = SkillEffectMode.Applied,
        });

        var deltas = book.Deltas(Bases, _ => new SkillEffectCaster(0, 0, Lk: 200), ownerIsPlayer: false);
        var loss = (double)(float)(200 * (double)0.34f);

        Assert.Equal((int)(float)(-loss * 0.4), deltas[SkillEffectKinds.StatHv]);
        Assert.Equal((int)(float)-loss, deltas[SkillEffectKinds.StatLk]);
        Assert.Equal((int)(float)-loss, deltas[SkillEffectKinds.StatDp]);
        Assert.Equal(new[] { SkillEffectKinds.StatHv, SkillEffectKinds.StatLk, SkillEffectKinds.StatDp }, kind.Stats);
    }

    [Fact]
    public void SkunkBagStunsAfterOnePointOneSeconds()
    {
        var kind = SkillEffectKinds.Find(SkillEffectKinds.SkunkBag)!;

        Assert.True(kind.Holds);
        Assert.Equal(1100, kind.HoldDelayMs);
        Assert.Equal(SkillPeriodicHit.DelayedHold, kind.Periodic);
    }

    [Fact]
    public void BindSpreadsAStunToSevenMonstersAndEndsWhenTheCasterIsHit()
    {
        var bind = SkillEffectKinds.Find(SkillEffectKinds.Bind)!;
        var effect = new SkillEffect
        {
            ObjectId = 1, Kind = bind, SkillId = 2201, EsClass = 1, CasterEntityId = 0x10, OwnerEntityId = 0x10,
            Amount = 0f, DurationMs = 20000, Mode = SkillEffectMode.Applied,
        };

        IncomingHits.Intercept(new IncomingHit(100, IncomingHits.BasicAttackType, 0, 0, 0), new[] { effect }, new HitDefender(0x10, 0, 0, 0, 0), _ => 0);

        Assert.Equal(SkillEffectKinds.BindOnMonster, bind.MonsterSpread);
        Assert.Equal(7, bind.MonsterSpreadLimit);
        Assert.True(SkillEffectKinds.Find(SkillEffectKinds.BindOnMonster)!.Holds);
        Assert.True(effect.EndRequested);
    }

    [Fact]
    public void ChillingPrisonNeedsItsWaterHitToConnect()
    {
        var kind = SkillEffectKinds.Find(SkillEffectKinds.ChillingPrison)!;

        Assert.True(kind.StrikesOnStart);
        Assert.Equal(SkillEffectTail.ModeThenTarget, kind.Tail);
        Assert.Equal("ApplyRT", kind.ChanceColumn);
        Assert.Equal(0x31, kind.StrikeMagicLoss);
    }

    [Fact]
    public void StunCancelIsAnInstantCleanse()
    {
        var kind = SkillEffectKinds.Find(SkillEffectKinds.StunCancel)!;

        Assert.Equal(SkillInstant.Cleanse, kind.Instant);
        Assert.Contains(SkillEffectKinds.SkunkBag, kind.CleansesActions);
        Assert.Contains(SkillEffectKinds.ChillingPrison, kind.CleansesActions);
        Assert.Equal(10, kind.CleansesActions.Count);
    }
}
