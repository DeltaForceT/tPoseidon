using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class SkillEffectBookTests
{
    private static readonly int[] Bases = { 100, 40, 10, 0, 60, 0, 0, 20, 30, 0, 50, 25 };
    private static readonly int[] DebuffActions = { 1081, 1098, 1099, 1102, 1117 };

    private static SkillEffect Effect(int actionId, float amount, sbyte esClass = 0, short seconds = 60, ushort caster = 1,
        SkillEffectMode mode = SkillEffectMode.Applied, int skillId = 1003) => new()
    {
        ObjectId = 0x500,
        Kind = SkillEffectKinds.Find(actionId)!,
        SkillId = skillId,
        EsClass = esClass,
        CasterEntityId = caster,
        OwnerEntityId = 1,
        Amount = amount,
        DurationMs = seconds * SkillEffect.MillisecondsPerSecond,
        Mode = mode,
    };

    private static SkillEffectCaster? Caster(ushort entityId) =>
        entityId == 7 ? new SkillEffectCaster(Ap: 100, Hv: 3, Ma: 81, Da: 40, Ac: 50) : null;

    [Fact]
    public void ARatioBuffAddsTheTruncatedShareOfTheBase()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(Effect(1079, 0.45f));

        var deltas = book.Deltas(Bases, Caster);

        Assert.Equal(18, deltas[SkillEffectKinds.StatAc]);
        Assert.Equal(0, deltas[SkillEffectKinds.StatAp]);
    }

    [Fact]
    public void HyperBeatAddsItsFlatNegativeDx()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(Effect(1101, -2f));

        Assert.Equal(-2, book.Deltas(Bases, Caster)[SkillEffectKinds.StatDx]);
    }

    [Fact]
    public void DebuffsTakeTheCastersLiveAttackOrAvoid()
    {
        var book = new SkillEffectBook(SkillEffectBook.MonsterCapacities);
        book.Register(Effect(1081, 0.14f, esClass: 1, caster: 7));
        book.Register(Effect(1098, 0.15f, esClass: 1, caster: 7));
        book.Register(Effect(1099, 0.15f, esClass: 1, caster: 7));
        book.Register(Effect(1117, 0.21f, esClass: 1, caster: 7));

        var deltas = book.Deltas(new int[SkillEffectKinds.StatCount], Caster);

        Assert.Equal(-14, deltas[SkillEffectKinds.StatDp]);
        Assert.Equal(-49, deltas[SkillEffectKinds.StatAc]);
        Assert.Equal(0, deltas[SkillEffectKinds.StatDa]);
        Assert.Equal(-10, deltas[SkillEffectKinds.StatMd]);
    }

    [Fact]
    public void ADebuffWhoseCasterIsGoneChangesNothing()
    {
        var book = new SkillEffectBook(SkillEffectBook.MonsterCapacities);
        book.Register(Effect(1081, 0.14f, esClass: 1, caster: 8));

        Assert.Equal(0, book.Deltas(new int[SkillEffectKinds.StatCount], Caster)[SkillEffectKinds.StatDp]);
    }

    [Fact]
    public void AFailedEffectChangesNothing()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(Effect(1079, 0.45f, mode: SkillEffectMode.Failed));

        Assert.Equal(0, book.Deltas(Bases, Caster)[SkillEffectKinds.StatAc]);
    }

    [Fact]
    public void RecastingTheSameActionReplacesItSilently()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        var first = Effect(1079, 0.10f);
        book.Register(first);

        var registration = book.Register(Effect(1079, 0.45f));

        Assert.NotNull(registration);
        Assert.Equal(new[] { (first, SkillEffectRemoval.Replaced) }, registration.Removed);
        Assert.Single(book.Snapshot());
    }

    [Fact]
    public void AFullClassEvictsItsOldestEffect()
    {
        var book = new SkillEffectBook(SkillEffectBook.MonsterCapacities);
        var debuffs = DebuffActions.Select(action => Effect(action, 0.1f, esClass: 1, caster: 7)).ToArray();
        foreach (var debuff in debuffs) book.Register(debuff);

        var registration = book.Register(Effect(1101, -1f, esClass: 1));

        Assert.NotNull(registration);
        Assert.Equal(new[] { (debuffs[0], SkillEffectRemoval.Evicted) }, registration.Removed);
    }

    [Fact]
    public void AClassWithoutRoomRefuses()
    {
        var book = new SkillEffectBook(SkillEffectBook.MonsterCapacities);

        Assert.Null(book.Register(Effect(1079, 0.1f, esClass: 3)));
    }

    [Fact]
    public void AnEffectWarnsOnceTenSecondsBeforeItEnds()
    {
        var effect = Effect(1079, 0.1f, seconds: 12);

        Assert.Equal(SkillEffectTick.None, effect.Tick(1000));
        Assert.Equal(SkillEffectTick.Warning, effect.Tick(1000));
        Assert.Equal(SkillEffectTick.None, effect.Tick(5000));
        Assert.Equal(SkillEffectTick.Ended, effect.Tick(5000));
    }

    [Fact]
    public void AFailedEffectEndsOnItsFirstTickAndClassThreeNeverWarns()
    {
        Assert.Equal(SkillEffectTick.Ended, Effect(1079, 0.1f, mode: SkillEffectMode.Failed).Tick(100));
        Assert.Equal(SkillEffectTick.None, Effect(1079, 0.1f, esClass: 3, seconds: 5).Tick(100));
    }

    [Fact]
    public void TheModeRollUsesTheApplyRatioAndTheResist()
    {
        Assert.Equal(SkillEffectMode.Applied, SkillEffectBook.RollMode(0f, 100, new Random(1)));
        Assert.Equal(SkillEffectMode.Applied, SkillEffectBook.RollMode(1f, 0, new Random(1)));
        Assert.Equal(SkillEffectMode.Resisted, SkillEffectBook.RollMode(1f, 100, new Random(1)));
        Assert.Equal(SkillEffectMode.Failed, SkillEffectBook.RollMode(0.001f, 0, new Random(1)));
    }

    [Fact]
    public void SecondBatchBuffsUseTheirOwnRules()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(Effect(1115, 400f));
        book.Register(Effect(1122, 0.45f));
        book.Register(Effect(1274, 0.26f));

        var deltas = book.Deltas(Bases, Caster);

        Assert.Equal(20, deltas[SkillEffectKinds.StatAc]);
        Assert.Equal(0, deltas[SkillEffectKinds.StatMd]);
        Assert.Equal(26, deltas[SkillEffectKinds.StatAp]);
    }

    [Fact]
    public void SecondBatchDebuffsFollowTheCasterAndTheOwnerKind()
    {
        var book = new SkillEffectBook(SkillEffectBook.MonsterCapacities);
        book.Register(Effect(1096, 0.30f, esClass: 1, caster: 7));
        book.Register(Effect(1106, 3.30f, esClass: 1, caster: 7));
        book.Register(Effect(1107, 0.15f, esClass: 1, caster: 7));
        book.Register(Effect(1110, 0.20f, esClass: 1, caster: 7));
        book.Register(Effect(1363, 0.11f, esClass: 1, caster: 7));
        var monsterBases = new int[SkillEffectKinds.StatCount];
        monsterBases[SkillEffectKinds.StatDp] = 50;

        var asMonster = book.Deltas(monsterBases, Caster, ownerIsPlayer: false);
        var asPlayer = book.Deltas(monsterBases, Caster, ownerIsPlayer: true);

        Assert.Equal(0, asMonster[SkillEffectKinds.StatMa]);
        Assert.Equal(2, asMonster[SkillEffectKinds.StatDx]);
        Assert.Equal(-76, asMonster[SkillEffectKinds.StatAp]);
        Assert.Equal(-10, asMonster[SkillEffectKinds.StatDp]);
        Assert.Equal(-2, asMonster[SkillEffectKinds.StatHv]);
        Assert.Equal(-5, asPlayer[SkillEffectKinds.StatHv]);
    }

    [Fact]
    public void KindsWithoutAStatRuleChangeNoStat()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(Effect(1526, 0.55f));
        book.Register(Effect(SkillEffectKinds.PartyGuardOnMember, 3f));

        Assert.All(book.Deltas(Bases, Caster), delta => Assert.Equal(0, delta));
        Assert.Equal(2, book.Snapshot().Count);
    }

    [Fact]
    public void MentalBreakTakesItsDurationInMilliseconds()
    {
        var kind = SkillEffectKinds.Find(1526)!;

        Assert.Equal(SkillEffectKinds.MillisecondsColumn, kind.DurationColumn);
        Assert.Equal(SkillEffectKinds.MillisecondsPerMillisecond, kind.MillisecondsPerUnit);
        Assert.False(kind.CarriesMode);
        Assert.True(SkillEffectKinds.Find(1079)!.CarriesMode);
    }
}
