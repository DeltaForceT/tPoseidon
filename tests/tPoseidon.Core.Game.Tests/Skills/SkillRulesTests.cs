using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class SkillRulesTests
{
    private const int PunchSkill = 1012;
    private const int MagicSkill = 2014;

    private static SkillTableRow Row(string table, int level, params (string Column, string Value)[] columns)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["Level"] = level.ToString() };
        foreach (var (column, value) in columns) values[column] = value;
        return new SkillTableRow { Table = table, Level = level, Columns = values };
    }

    private static SkillParam2Entry Skill(int id, int action, string name, int maxLevel = 1, int options = 0x202) => new()
    {
        Id = id,
        Type = 2,
        SkillOption = options,
        MaxSkillLevel = maxLevel,
        EsAction = action,
        EsaiiTable = "ESAII_" + name,
        EsaTable = "ESAction_" + name,
        InvocationCondition = SkillDefinition.ChaseCondition,
        TargetInput = SkillDefinition.ObjectTargetInput,
        EsClass = -1,
        SkillExp = 1,
        StateType = 1,
    };

    private static SkillBook Book() => new(
        new[]
        {
            Skill(PunchSkill, SkillActions.Punch, "Punch"),
            Skill(MagicSkill, SkillActions.FairyOfMagic, "FairyOfMagic", options: 0x206),
            Skill(1001, 1078, "SteelPunch", maxLevel: 4),
        },
        new[]
        {
            Row("ESAII_Punch", 1, ("MpCon", "24"), ("Delay", "1.000000"), ("Range", "80"), ("CoolTime", "3.000000")),
            Row("ESAII_FairyOfMagic", 1, ("MpCon", "10"), ("Delay", "0.500000"), ("Range", "250"), ("CoolTime", "1.000000")),
            Row("ESAII_SteelPunch", 1, ("MpCon", "32")),
            Row("ESAII_SteelPunch", 3, ("MpCon", "38")),
        },
        new[]
        {
            Row("ESAction_Punch", 1, ("Ratio1", "2"), ("Ratio2", "140"), ("ApplyRatio", "0.800000")),
            Row("ESAction_FairyOfMagic", 1, ("Ratio1", "10"), ("Ratio2", "220"), ("ApplyRatio", "0.800000")),
        });

    [Fact]
    public void ASkillFindsItsRowsByTableNameAndLevel()
    {
        var punch = Book().Find(PunchSkill)!;

        Assert.Equal(24, punch.ConditionsAt(1)!.Amount("MpCon"));
        Assert.Equal(140, punch.ActionAt(1)!.Amount("Ratio2"));
        Assert.True(punch.AsksForTarget);
        Assert.True(punch.Has(SkillDefinition.NotInTownOption));
        Assert.False(punch.Has(SkillDefinition.CancelledByDamageOption));
        Assert.True(Book().Find(MagicSkill)!.Has(SkillDefinition.CancelledByDamageOption));
        Assert.Null(Book().Find(9999));
    }

    [Fact]
    public void ALevelWithoutARowUsesThePreviousLevelsRow()
    {
        var steel = Book().Find(1001)!;

        Assert.Null(steel.ConditionsAt(0));
        Assert.Equal(32, steel.ConditionsAt(2)!.Amount("MpCon"));
        Assert.Equal(38, steel.ConditionsAt(4)!.Amount("MpCon"));
        Assert.True(steel.HasLevel(4));
        Assert.False(steel.HasLevel(5));
        Assert.Null(steel.ConditionsAt(5));
    }

    [Theory]
    [InlineData(24, null)]
    [InlineData(23, (int)SkillFailure.NotEnoughMp)]
    public void ASkillNeedsItsMpCost(int mp, int? failure)
    {
        var conditions = Book().Find(PunchSkill)!.ConditionsAt(1);

        Assert.Equal((ushort?)failure, SkillChecks.Conditions(conditions, 1, 100, mp, new Random(1)));
    }

    [Fact]
    public void HpCostAndLevelLimitsAreCheckedBeforeMp()
    {
        var row = Row("ESAII_X", 1, ("MinLv", "5"), ("MaxLv", "9"), ("HpCon", "50"), ("MpCon", "10"));

        Assert.Equal(SkillFailure.BelowMinLevel, SkillChecks.Conditions(row, 4, 100, 100, new Random(1)));
        Assert.Equal(SkillFailure.AboveMaxLevel, SkillChecks.Conditions(row, 10, 100, 100, new Random(1)));
        Assert.Equal(SkillFailure.NotEnoughHp, SkillChecks.Conditions(row, 5, 49, 0, new Random(1)));
        Assert.Equal(SkillFailure.NotEnoughMp, SkillChecks.Conditions(row, 5, 50, 9, new Random(1)));
        Assert.Null(SkillChecks.Conditions(row, 5, 50, 10, new Random(1)));
    }

    [Fact]
    public void AProbabilityColumnLetsTheSkillThroughOnlyUpToItsValue()
    {
        var row = Row("ESAII_X", 1, ("Probability", "30"));

        Assert.Null(SkillChecks.Conditions(row, 1, 1, 1, new Scripted(30)));
        Assert.Equal(SkillFailure.General, SkillChecks.Conditions(row, 1, 1, 1, new Scripted(31)));
    }

    [Fact]
    public void RangeDelayAndCostsComeFromTheRow()
    {
        var conditions = Book().Find(PunchSkill)!.ConditionsAt(1);

        Assert.Equal(80, SkillChecks.RangeOf(conditions));
        Assert.Equal(SkillChecks.DefaultRange, SkillChecks.RangeOf(Row("ESAII_X", 1)));
        Assert.Equal(1000, SkillChecks.DelayMsOf(conditions));
        Assert.Equal((0, 24), SkillChecks.Costs(conditions, 100, 24));
        Assert.Equal((0, 0), SkillChecks.Costs(conditions, 100, 23));
    }

    [Theory]
    [InlineData(100, 100, 179, 100, true)]
    [InlineData(100, 100, 180, 100, false)]
    [InlineData(100, 100, 148, 164, false)]
    [InlineData(100, 100, 147, 164, true)]
    public void ATargetIsInRangeWhenCloserThanTheRange(int fromX, int fromY, int toX, int toY, bool within)
    {
        Assert.Equal(within, SkillChecks.IsWithinRange(80, fromX, fromY, toX, toY));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void TownsAreTheMapTypesWhereBattleSkillsAreRefused(int mapType, bool town)
    {
        Assert.Equal(town, SkillChecks.IsTownMap(mapType));
    }

    [Fact]
    public void ACooldownStartsHalfASecondShortAndEndsOnTheTickAfterItsTime()
    {
        var cooldowns = new SkillCooldowns();

        cooldowns.Start(PunchSkill, 3.0f);
        cooldowns.Tick(2400);
        Assert.True(cooldowns.IsRunning(PunchSkill));
        cooldowns.Tick(100);
        Assert.True(cooldowns.IsRunning(PunchSkill));
        cooldowns.Tick(100);
        Assert.False(cooldowns.IsRunning(PunchSkill));
    }

    [Fact]
    public void ACooldownOfHalfASecondOrLessNeverStarts()
    {
        var cooldowns = new SkillCooldowns();

        cooldowns.Start(PunchSkill, 0.5f);

        Assert.False(cooldowns.IsRunning(PunchSkill));
    }

    private static readonly SkillAttacker Caster = new(Ap: 40, Ac: 50, Ma: 60, Lk: 10, Level: 3);
    private static readonly SkillTarget Monster = new(Dp: 20, Hv: 4, Md: 10, Lk: 2, Hp: 500, Level: 3, Resist: 0);

    [Fact]
    public void APhysicalSkillHitsByAccuracyAgainstEvasion()
    {
        Assert.Equal(SkillDamage.KindMiss, SkillDamage.Physical(Caster, 200, Monster, 0.8f, 0, new Scripted(76, 99)).Kind);
        Assert.Equal(SkillDamage.KindHit, SkillDamage.Physical(Caster, 200, Monster, 0.8f, 0, new Scripted(75, 99)).Kind);
    }

    [Fact]
    public void APhysicalSkillDealsPowerLessThreeQuartersOfDefence()
    {
        var hit = SkillDamage.Physical(Caster, 200, Monster, 0.8f, 0, new Scripted(0, 99));

        Assert.Equal(new SkillHit(185, SkillDamage.PhysicalType, SkillDamage.KindHit, 0, 0), hit);
    }

    [Fact]
    public void ACriticalPhysicalSkillUsesOneAndAHalfTimesThePower()
    {
        var hit = SkillDamage.Physical(Caster, 200, Monster, 0.8f, 0, new Scripted(0, 13));

        Assert.Equal(new SkillHit(285, SkillDamage.PhysicalType, SkillDamage.KindCritical, 0, 0), hit);
        Assert.Equal(SkillDamage.KindHit, SkillDamage.Physical(Caster, 200, Monster, 0.8f, 0, new Scripted(0, 14)).Kind);
    }

    [Fact]
    public void AHitThatReachesTheTargetsHpCarriesTheFinishingFlag()
    {
        var weak = Monster with { Hp = 185 };

        Assert.Equal(SkillDamage.KindFinishingFlag, SkillDamage.Physical(Caster, 200, weak, 0.8f, 0, new Scripted(0, 99)).Kind);
        Assert.Equal(
            SkillDamage.KindCritical | SkillDamage.KindFinishingFlag,
            SkillDamage.Physical(Caster, 200, weak, 0.8f, 0, new Scripted(0, 0)).Kind);
    }

    [Fact]
    public void AMagicSkillHitsByLuckAndScalesWithMagicAttack()
    {
        Assert.Equal(SkillDamage.KindMiss, SkillDamage.Magic(Caster, 260, Monster, 0.8f, 0, 0x80, new Scripted(94, 99)).Kind);

        var hit = SkillDamage.Magic(Caster, 260, Monster, 0.8f, 0, 0x80, new Scripted(93, 99));

        Assert.Equal(new SkillHit(147, SkillDamage.MagicType, SkillDamage.KindHit, 0x80, 0), hit);
        Assert.Equal(162, SkillDamage.Magic(Caster, 260, Monster, 0.8f, 0, 0x80, new Scripted(0, 0)).Damage);
    }

    [Theory]
    [InlineData(20, 148, 0)]
    [InlineData(100, 0, 0)]
    [InlineData(130, 0, 55)]
    public void ResistTakesItsPercentAndAboveAHundredTurnsTheRestIntoHealing(int resist, int damage, int healed)
    {
        var hit = SkillDamage.Physical(Caster, 200, Monster with { Resist = resist }, 0.8f, 0, new Scripted(0, 99));

        Assert.Equal((damage, healed), (hit.Damage, hit.Healed));
    }

    [Fact]
    public void ThePunchAddsTwiceTheLevelUpToThirtyAndTheMagicSkillDoesNot()
    {
        var book = Book();
        var punch = SkillActions.Resolve(
            SkillActions.Punch, book.Find(PunchSkill)!.ActionAt(1)!, Caster, Monster with { Dp = 0 }, new Scripted(0, 99));
        var capped = SkillActions.Resolve(
            SkillActions.Punch, book.Find(PunchSkill)!.ActionAt(1)!, Caster with { Level = 80 }, Monster with { Dp = 0 }, new Scripted(0, 99));
        var magic = SkillActions.Resolve(
            SkillActions.FairyOfMagic, book.Find(MagicSkill)!.ActionAt(1)!, Caster, Monster with { Md = 0 }, new Scripted(0, 99));

        Assert.Equal(40 + 3 * 2 + 140, punch.Damage);
        Assert.Equal(40 + 30 * 2 + 140, capped.Damage);
        Assert.Equal((int)((float)((40 + 220) * 0.6)), magic.Damage);
        Assert.Equal(SkillActions.NoPropertyElement, magic.Element);
    }

    [Fact]
    public void ElementsReadTheirOwnResistColumn()
    {
        var monster = new MonsterParamEntry { Id = 1, Name = "m", PhysicalR = 11, NoPropR = 22, GunR = 33, FireR = 44, ShadowR = 55 };

        Assert.Equal(11, SkillActions.ResistOf(monster, SkillActions.NoElement));
        Assert.Equal(22, SkillActions.ResistOf(monster, SkillActions.NoPropertyElement));
        Assert.Equal(33, SkillActions.ResistOf(monster, SkillActions.GunElement));
        Assert.Equal(44, SkillActions.ResistOf(monster, SkillActions.FireElement));
        Assert.Equal(55, SkillActions.ResistOf(monster, SkillActions.ShadowElement));
        Assert.Equal(0, SkillActions.ResistOf(monster, 0x200));
    }

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(10, 20, 20, 30)]
    [InlineData(10, 20, 30, 27)]
    [InlineData(10, 20, 80, 15)]
    public void UsingASkillGivesTmExpByLevelAndLevelDifference(int skillExp, int casterLevel, int targetLevel, int tmExp)
    {
        Assert.Equal(tmExp, SkillActions.TmExpForUse(skillExp, casterLevel, targetLevel));
    }

    [Fact]
    public void StarterActionsAreBuiltAndTheMagicOneDoesNotStartAnAttack()
    {
        Assert.True(SkillActions.IsBuilt(SkillActions.Punch));
        Assert.True(SkillActions.IsBuilt(SkillActions.WarmingUp));
        Assert.False(SkillActions.IsBuilt(1510));
        Assert.False(SkillActions.IsBuilt(1521));
        Assert.True(SkillActions.StartsAttackAfterwards(SkillActions.Punch, holdsGun: true));
        Assert.False(SkillActions.StartsAttackAfterwards(SkillActions.FairyOfMagic, holdsGun: false));
        Assert.True(SkillActions.StartsAttackAfterwards(SkillActions.StoneThrowing, holdsGun: false));
        Assert.False(SkillActions.StartsAttackAfterwards(SkillActions.StoneThrowing, holdsGun: true));
    }

    [Fact]
    public void ACastWaitsForItsTargetThenChasesThenCountsItsDelay()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var cast = new SkillCast(Book().Find(PunchSkill)!, 1);

        Assert.Equal(SkillCastPhase.AwaitingTarget, cast.Phase);
        Assert.False(cast.IsUnderWay);

        cast.Chase(0x0201, 300, 300, start.AddSeconds(1), start);
        Assert.Equal(SkillCastPhase.Chasing, cast.Phase);
        Assert.False(cast.ChaseTimedOut(cast.Elapse(start.AddMilliseconds(5000))));
        Assert.True(cast.ChaseTimedOut(cast.Elapse(start.AddMilliseconds(5001))));

        cast.StartCasting(0x0201, start);
        Assert.Equal(SkillCastPhase.Casting, cast.Phase);
        Assert.False(cast.DelayIsOver(cast.Elapse(start.AddMilliseconds(999))));
        Assert.True(cast.DelayIsOver(cast.Elapse(start.AddMilliseconds(1000))));
    }

    private sealed class Scripted : Random
    {
        private readonly Queue<int> _draws;

        public Scripted(params int[] draws)
        {
            _draws = new Queue<int>(draws);
        }

        public override int Next(int maxValue) => _draws.Dequeue();
    }

    [Theory]
    [InlineData(SkillPowerRule.AttackTimesRatio, 76)]
    [InlineData(SkillPowerRule.DigTimesRatio, 285)]
    [InlineData(SkillPowerRule.MissingHpTimesRatio, 115)]
    public void JobHitPowerFollowsItsRule(SkillPowerRule rule, int expected)
    {
        var attacker = new SkillAttacker(Ap: 59, Ac: 50, Ma: 60, Lk: 10, Level: 3, Da: 10, Hp: 100, MaxHp: 400);

        Assert.Equal(expected, SkillHitShapes.Power(rule, attacker, 1.3f));
    }

    [Fact]
    public void TitaniumWristAddsItsRatioAndReplacesTheApplyRatio()
    {
        var strike = Row("Strike", 1, ("Ratio", "1.200000"), ("ApplyRatio", "0.650000"));
        var wrist = Row("ESAction_HandIntensification", 1, ("Ratio", "0.200000"), ("ApplyRatio", "0.750000"));

        Assert.Equal((1.2f, 0.65f), SkillHitShapes.WithEnhancer(strike, null));
        Assert.Equal((1.2f + 0.2f, 0.75f), SkillHitShapes.WithEnhancer(strike, wrist));
    }

    [Fact]
    public void TheSixJobHitsAreBuiltAndStoneStrikeKeepsTheGunRule()
    {
        foreach (var action in new[] { 1078, 1080, 1090, 1094, 1100, 1123 }) Assert.True(SkillActions.IsBuilt(action));
        Assert.False(SkillActions.StartsAttackAfterwards(SkillHitShapes.StoneStrike, holdsGun: true));
        Assert.True(SkillActions.StartsAttackAfterwards(SkillHitShapes.PowerBlow, holdsGun: true));
    }

    [Fact]
    public void ManaArrowShootsOneArrowWithoutArrowRushAndItsCountWithIt()
    {
        var rush = Row("ESAction_ChainArrow", 1, ("Count", "3"), ("Ratio", "0.610000"), ("ApplyRatio", "0.700000"));

        Assert.True(SkillActions.IsBuilt(SkillActions.ManaArrow));
        Assert.Equal(SkillActions.ArrowRush, SkillActions.EnhancerOf(SkillActions.ManaArrow));
        Assert.Equal(1, SkillActions.HitsOf(SkillActions.ManaArrow, null));
        Assert.Equal(3, SkillActions.HitsOf(SkillActions.ManaArrow, rush));
        Assert.Equal(1, SkillActions.HitsOf(SkillHitShapes.Shockwave, rush));
        Assert.False(SkillActions.StartsAttackAfterwards(SkillActions.ManaArrow, holdsGun: false));
        Assert.Equal(SkillActions.NoPropertyElement, SkillActions.ElementOf(SkillActions.ManaArrow));
    }

    [Fact]
    public void ManaArrowHitsWithMagicLessFortyNineAndTheArrowRushRatio()
    {
        var arrow = Row("Magic_Arrow", 1, ("AP", "2300"), ("ApplyRatio", "0.720000"));
        var rush = Row("ESAction_ChainArrow", 1, ("Count", "2"), ("Ratio", "0.500000"), ("ApplyRatio", "1.000000"));
        var caster = new SkillAttacker(Ap: 40, Ac: 500, Ma: 149, Lk: 0, Level: 3);
        var target = new SkillTarget(Dp: 0, Hv: 0, Md: 0, Lk: 0, Hp: 5000, Level: 3, Resist: 0);

        var plain = SkillActions.ResolveArrow(arrow, null, caster, target, new Random(1));
        var rushed = SkillActions.ResolveArrow(arrow, rush, caster, target, new Random(1));

        Assert.Equal(SkillDamage.MagicType, plain.Type);
        if (plain.Kind == SkillDamage.KindHit) Assert.Equal(SkillDamage.MagicDamage(2300, 100, 0), plain.Damage);
        if (rushed.Kind == SkillDamage.KindHit) Assert.Equal(SkillDamage.MagicDamage(1150, 100, 0), rushed.Damage);
    }

    [Fact]
    public void ManaRingTakesTheBoosterPowerAndFallsOffWithDistance()
    {
        var ring = Row("Magic_Ring", 1, ("AP", "400"), ("Radius", "120"), ("ApplyRatio", "0.630000"));
        var booster = Row("SealOfMagic", 1, ("AP", "100"), ("ApplyRatio", "0.880000"));

        Assert.True(SkillActions.IsBuilt(SkillAreaRules.ManaRing));
        Assert.Equal(SkillAreaRules.ManaRingBooster, SkillActions.EnhancerOf(SkillAreaRules.ManaRing));
        Assert.Equal((400, 0.63f, 120f), SkillAreaRules.Ring(ring, null));
        Assert.Equal((500, 0.88f, 120f), SkillAreaRules.Ring(ring, booster));
        Assert.Equal(50f, SkillAreaRules.Distance(0, 0, 30, 40));
        Assert.True(SkillAreaRules.Reaches(120f, 120f));
        Assert.False(SkillAreaRules.Reaches(120.5f, 120f));
        Assert.Equal(75, SkillAreaRules.WithFalloff(100, 30f, 120f));
        Assert.Equal(0, SkillAreaRules.WithFalloff(100, 120f, 120f));
    }

    [Fact]
    public void TornadoBombSplitsThreeHitsIntoThirds()
    {
        var tornado = Row("TornadoAttack", 1, ("AP", "700"), ("ApplyRatio", "100.000000"));
        var strong = new SkillAttacker(Ap: 200, Ac: 5000, Ma: 0, Lk: 0, Level: 3);
        var soft = new SkillTarget(Dp: 0, Hv: 0, Md: 0, Lk: 0, Hp: 50000, Level: 3, Resist: 0);

        var outcome = SkillActions.ResolveTornado(tornado, strong, soft, new Random(3));

        Assert.Equal(3, outcome.Slots.Count);
        Assert.Equal(outcome.Slots.Sum(slot => slot.Damage), outcome.Sum);
        Assert.Equal(outcome.Slots.Count(slot => slot.Damage > 0), outcome.Landed);
        Assert.All(outcome.Slots.Where(slot => slot.Kind == SkillDamage.KindHit), slot => Assert.Equal(900 / 3, slot.Damage));
        Assert.True(SkillActions.StartsAttackAfterwards(SkillActions.TornadoBomb, holdsGun: true));
    }

    [Fact]
    public void CureRaisesHpByItsShareOfMaxHpUpToTheMax()
    {
        Assert.Equal((129u, true), SkillActions.Heal(100, 500, 0.06f));
        Assert.Equal((500u, true), SkillActions.Heal(490, 500, 0.30f));
        Assert.Equal((500u, false), SkillActions.Heal(500, 500, 0.30f));
        Assert.True(SkillActions.IsBuilt(SkillActions.Cure));
    }

    [Fact]
    public void UpperSmashRestartsAfterTwoSwingIntervalsAndDropsTheEarlierAttack()
    {
        Assert.Equal(2 * (600 + 400), SkillHitShapes.RestartDelayMs(SkillHitShapes.UpperSmash, 600, 400, 500));
        Assert.Equal(500, SkillHitShapes.RestartDelayMs(SkillHitShapes.Shockwave, 600, 400, 500));
        Assert.False(SkillHitShapes.KeepsEarlierAttack(SkillHitShapes.UpperSmash));
        Assert.True(SkillHitShapes.KeepsEarlierAttack(SkillHitShapes.PowerBlow));
    }

    private static readonly SkillHit[] ThreeHits =
    {
        new(30, SkillDamage.PhysicalType, SkillDamage.KindHit, 0, 0),
        new(0, SkillDamage.PhysicalType, SkillDamage.KindMiss, 0, 0),
        new(40, SkillDamage.PhysicalType, SkillDamage.KindCritical, 0, 0),
    };

    [Fact]
    public void TornadoBombWithoutShieldsKeepsEveryLandedHit()
    {
        var outcome = SkillActions.RecordTornadoHits(ThreeHits, default);

        Assert.Equal(70, outcome.Sum);
        Assert.Equal(2, outcome.Landed);
        Assert.Equal(2, outcome.ResultCount);
        Assert.Equal(new (int, int, ushort)[] { (30, 0, 0xFFFF), (0, 1, 0xFFFF), (40, 3, 0xFFFF) }, outcome.Slots);
    }

    [Fact]
    public void ATornadoShieldGaugeAbsorbsOnTheWireButNotInTheTotal()
    {
        var outcome = SkillActions.RecordTornadoHits(ThreeHits, new TornadoShields(Gauge: 50, Bunker: 0, GuardCount: 0));

        Assert.Equal(70, outcome.Sum);
        Assert.Equal(2, outcome.ShieldHits);
        Assert.Equal(2, outcome.ResultCount);
        Assert.Equal((0, SkillActions.ShieldAbsorbedKind, (ushort)0xFFFF), outcome.Slots[0]);
        Assert.Equal((20, SkillDamage.KindCritical, (ushort)0xFFFF), outcome.Slots[2]);
    }

    [Fact]
    public void AGuardCountRemovesTheHitFromTheTotal()
    {
        var outcome = SkillActions.RecordTornadoHits(ThreeHits, new TornadoShields(Gauge: 0, Bunker: 0, GuardCount: 1));

        Assert.Equal(40, outcome.Sum);
        Assert.Equal((0, SkillActions.GuardAbsorbedKind, (ushort)0), outcome.Slots[0]);
        Assert.Equal((40, SkillDamage.KindCritical, (ushort)0xFFFF), outcome.Slots[2]);
        Assert.Equal(1, outcome.ResultCount);
    }

    [Fact]
    public void ResistIsTheProtectRatioCappedAtOneHundred()
    {
        var protections = new Dictionary<int, int> { [1005] = 20, [4006] = 140 };

        Assert.Equal(20, tPoseidon.Core.Game.Skills.Effects.SkillResist.Of(protections, 1005));
        Assert.Equal(100, tPoseidon.Core.Game.Skills.Effects.SkillResist.Of(protections, 4006));
        Assert.Equal(0, tPoseidon.Core.Game.Skills.Effects.SkillResist.Of(protections, 1006));
        Assert.Equal(0, tPoseidon.Core.Game.Skills.Effects.SkillResist.Of(null, 1005));
    }

    [Fact]
    public void InvincibleCastingIsAPassiveWithAbilityThree()
    {
        var invincible = Skill(2002, 1000, "InvincibleCasting") with { Type = SkillDefinition.PassiveType, PSAbility = SkillBook.InvincibleCastingAbility };
        var book = new SkillBook(new[] { invincible, Skill(PunchSkill, SkillActions.Punch, "Punch") }, Array.Empty<SkillTableRow>(), Array.Empty<SkillTableRow>());

        Assert.True(book.GrantsAbility(new Dictionary<int, int> { [2002] = 1 }, SkillBook.InvincibleCastingAbility));
        Assert.False(book.GrantsAbility(new Dictionary<int, int> { [PunchSkill] = 1 }, SkillBook.InvincibleCastingAbility));
    }

    [Theory]
    [InlineData(SkillPowerRule.StockTimesRatio, 10)]
    [InlineData(SkillPowerRule.AttackAndAvoidThreeQuarters, 135)]
    [InlineData(SkillPowerRule.LuckAndTargetLife, 168)]
    [InlineData(SkillPowerRule.AvoidSixteenfold, 103)]
    [InlineData(SkillPowerRule.AttackAndAvoidHalf, 90)]
    [InlineData(SkillPowerRule.AttackAndLifeEighth, 92)]
    [InlineData(SkillPowerRule.AttackDigAndLuck, 492)]
    [InlineData(SkillPowerRule.AttackAndDig, 284)]
    public void SecondTierPowerRulesFollowTheServerFormulas(SkillPowerRule rule, int expected)
    {
        var attacker = new SkillAttacker(Ap: 59, Ac: 40, Ma: 60, Lk: 10, Level: 3, Da: 10, Hp: 100, MaxHp: 400, Hv: 5, Dx: 3);
        var target = new SkillTarget(Dp: 20, Hv: 4, Md: 10, Lk: 2, Hp: 500, Level: 3, Resist: 0, MaxHp: 500, BaseDp: 20);

        Assert.Equal(expected, SkillHitShapes.Power(rule, attacker, target, 1.3f, null, null));
    }

    private static readonly int[] ThunderPowers = { 500, 1000 };

    [Fact]
    public void ShadowAndMagicPowerReadTheirColumns()
    {
        var shadow = Row("OnesOtherSelf", 6, ("Ratio", "1.450000"), ("Count", "3"));
        var lance = Row("DarkSpear", 1, ("AP", "2500"), ("ApplyRatio", "0.700000"));
        var thunder = Row("CallLightning", 1, ("M_AP", "500"), ("MaxCount", "2"), ("ApplyRT", "0.800000"));
        var attacker = new SkillAttacker(Ap: 100, Ac: 40, Ma: 60, Lk: 10, Level: 3, Dx: 3);

        Assert.Equal(1270, SkillHitShapes.Power(SkillPowerRule.ShadowCopies, attacker, default, 1.45f, shadow, null));
        Assert.Equal(2500, SkillHitShapes.Power(SkillPowerRule.MagicPower, attacker, default, 0f, lance, null));
        Assert.Contains(SkillHitShapes.Power(SkillPowerRule.MagicPowerRandomTimes, attacker, default, 0f, thunder, new Random(5)), ThunderPowers);
    }

    [Fact]
    public void SecondTierShapesCarryTheirElementTailAndRules()
    {
        Assert.Equal(SkillHitShapes.DarkElement, SkillActions.ElementOf(SkillHitShapes.DarkLance));
        Assert.Equal(SkillTail.None, SkillHitShapes.Find(SkillHitShapes.Scythe)!.Tail);
        Assert.True(SkillHitShapes.Find(SkillHitShapes.Shadow)!.TargetInHeader);
        Assert.True(SkillHitShapes.Find(SkillHitShapes.CrackAttack)!.FlatTm);
        Assert.False(SkillActions.StartsAttackAfterwards(SkillHitShapes.PiercingWave, holdsGun: false));
        Assert.False(SkillActions.StartsAttackAfterwards(SkillHitShapes.LuckyFist, holdsGun: true));
        Assert.True(SkillActions.StartsAttackAfterwards(SkillHitShapes.LuckyFist, holdsGun: false));
        Assert.False(SkillHitShapes.KeepsEarlierAttack(SkillHitShapes.LuckyFist));
    }
}
