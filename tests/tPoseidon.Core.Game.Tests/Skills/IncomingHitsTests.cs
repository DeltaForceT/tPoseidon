using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class IncomingHitsTests
{
    private static readonly int[] LightBases = { 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 120, 0 };

    private static readonly HitDefender Owner = new(0x10, Ma: 300, Ac: 120, Lk: 60, Hv: 80);

    private static SkillEffect Effect(int actionId, float amount, int charges = 0, int magnitude = 0, ushort caster = 0x10, SkillTableRow? action = null) => new()
    {
        ObjectId = 0x500, Kind = SkillEffectKinds.Find(actionId)!, SkillId = 1, EsClass = 0, CasterEntityId = caster, OwnerEntityId = 0x10,
        Amount = amount, Charges = charges, Magnitude = magnitude, DurationMs = 60000, Mode = SkillEffectMode.Applied, Action = action,
    };

    private static IncomingHit Melee(int damage) => new(damage, IncomingHits.BasicAttackType, 0, 0, 0);

    [Fact]
    public void GuardBlocksBasicAttacksUntilItsChargesRunOut()
    {
        var guard = Effect(SkillEffectKinds.Guard, 2f, charges: 2);

        var first = IncomingHits.Intercept(Melee(300), new[] { guard }, Owner, _ => 0).Hit;
        var second = IncomingHits.Intercept(Melee(300), new[] { guard }, Owner, _ => 0).Hit;

        Assert.Equal((0, IncomingHits.KindBlockedByGuard, (ushort)1), (first.Damage, first.Kind, first.Extra));
        Assert.Equal((0, (ushort)0), (second.Damage, second.Extra));
        Assert.True(guard.EndRequested);
    }

    [Fact]
    public void GuardLetsMagicThrough()
    {
        var guard = Effect(SkillEffectKinds.Guard, 2f, charges: 2);

        var hit = IncomingHits.Intercept(new IncomingHit(300, IncomingHits.MagicType, 0, 0, 0), new[] { guard }, Owner, _ => 0).Hit;

        Assert.Equal(300, hit.Damage);
        Assert.Equal(2, guard.Charges);
    }

    [Fact]
    public void ShieldAbsorbsUntilBrokenAndPassesTheOverflow()
    {
        var shield = Effect(SkillEffectKinds.Shield, 1.2f, charges: 500, magnitude: 500);

        var absorbed = IncomingHits.Intercept(Melee(300), new[] { shield }, Owner, _ => 0);
        var broken = IncomingHits.Intercept(Melee(300), new[] { shield }, Owner, _ => 0);

        Assert.Equal((0, IncomingHits.KindAbsorbed), (absorbed.Hit.Damage, absorbed.Hit.Kind));
        Assert.Single(absorbed.ShieldsTouched);
        Assert.Equal(100, broken.Hit.Damage);
        Assert.Equal(0, shield.Charges);
        Assert.True(shield.EndRequested);
    }

    [Fact]
    public void WhiteKnightHalvesHitsOnMembersButNotOnTheKnight()
    {
        var onMember = Effect(SkillEffectKinds.WhiteKnightOnMember, 0.9f, caster: 0x20);
        var onKnight = Effect(SkillEffectKinds.WhiteKnightOnMember, 0.9f, caster: 0x10);

        Assert.Equal(151, IncomingHits.Intercept(Melee(303), new[] { onMember }, Owner, _ => 0).Hit.Damage);
        Assert.Equal(303, IncomingHits.Intercept(Melee(303), new[] { onKnight }, Owner, _ => 0).Hit.Damage);
    }

    [Fact]
    public void SubcutaneousReducesSkillHitsByTheCastersAvoid()
    {
        var skin = Effect(SkillEffectKinds.Subcutaneous, 0.44f);

        var skill = IncomingHits.Intercept(new IncomingHit(1000, IncomingHits.PhysicalType, 0, 0, 0), new[] { skin }, Owner, _ => 200).Hit;
        var melee = IncomingHits.Intercept(Melee(1000), new[] { skin }, Owner, _ => 200).Hit;

        Assert.Equal(1000 - IncomingHits.SkinReduction(1000, 200, 0.44f), skill.Damage);
        Assert.Equal(1000 - (int)(1000 * (double)(35 / 100f)), skill.Damage);
        Assert.Equal(1000, melee.Damage);
        Assert.Equal(IncomingHits.FlagReduced, melee.Flags);
    }

    [Fact]
    public void MagicReflectSendsBackAtMostEightyPercent()
    {
        Assert.Equal((int)(1000 * Math.Min(0.8, (80 / 100.0 + 0.3) * 0.44f)), IncomingHits.MirrorShare(1000, 80, 0.44f));
        Assert.Equal(800, IncomingHits.MirrorShare(1000, 500, 1f));
    }

    [Fact]
    public void ACounterAnswersEveryHitWithHalfItsPower()
    {
        var row = new SkillTableRow { Table = "ESAction_WaterCounter", Level = 1, Columns = new Dictionary<string, string> { ["AP"] = "900", ["ApplyRatio"] = "1.000000" } };
        var counter = Effect(SkillEffectKinds.WaterCounter, 900f, action: row);

        var interception = IncomingHits.Intercept(Melee(300), new[] { counter }, Owner, _ => 0);

        var strike = Assert.Single(interception.Counters);
        Assert.Equal(450, strike.Power);
        Assert.Equal(SkillActions.WaterElement, strike.Element);
        Assert.Equal(300 - (120 + 60), strike.MagicLoss);
        Assert.Equal(300, interception.Hit.Damage);
        Assert.Equal(SkillEffectKinds.WaterShield, counter.Kind.RequiresEffect);
    }

    [Fact]
    public void LightShieldRaisesMagicDefenceAndDefenceFromMagic()
    {
        var light = Effect(SkillEffectKinds.LightShield, 0.15f);
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(new SkillEffect
        {
            ObjectId = 1, Kind = light.Kind, SkillId = 2213, EsClass = 0, CasterEntityId = 1, OwnerEntityId = 1,
            Amount = 0.15f, SecondAmount = 0.15f, DurationMs = 60000, Mode = SkillEffectMode.Applied,
        });

        var deltas = book.Deltas(LightBases, _ => new SkillEffectCaster(0, 0, 300, 0, 0));
        var expected = (int)(float)((300 - 49) << 4) * 0.15f;

        Assert.Equal((int)(float)(((300 - 49) << 4) * (double)0.15f), deltas[SkillEffectKinds.StatMd]);
        Assert.Equal(deltas[SkillEffectKinds.StatMd], deltas[SkillEffectKinds.StatDp]);
        Assert.True(expected > 0);
        Assert.Equal(new[] { SkillEffectKinds.StatMd, SkillEffectKinds.StatDp }, light.Kind.Stats);
    }
}
