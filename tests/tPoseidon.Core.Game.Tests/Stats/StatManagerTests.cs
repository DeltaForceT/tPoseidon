using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Game.Stats;

namespace tPoseidon.Core.GameTests.Stats;

public class StatManagerTests
{
    private static Character MakeChar(
        uint money = 0,
        ulong exp = 0,
        uint hp = 41,
        uint mp = 5,
        ushort level = 1,
        uint tmExp = 0,
        ushort tmLevel = 0,
        ushort tmPoint = 0)
        => new()
        {
            CharacterId = 0x10000001,
            OwnerAccountId = 1,
            Slot = 0,
            Name = "Tester",
            Species = Species.Lion,
            FType = FType.Power,
            Money = money,
            Exp = exp,
            CurrentHp = hp,
            CurrentMp = mp,
            Level = level,
            TmExp = tmExp,
            TmLevel = tmLevel,
            TmPoint = tmPoint,
        };

    [Fact]
    public void GrantMoney_PositiveDelta_FiresEventWithNewTotal()
    {
        var sm = new StatManager(MakeChar(money: 100));
        MoneyChangedEvent? captured = null;
        sm.MoneyChanged += e => captured = e;

        sm.GrantMoney(50);

        Assert.Equal(150u, sm.Money);
        Assert.NotNull(captured);
        Assert.Equal(150u, captured!.NewTotal);
        Assert.Equal(50, captured.Delta);
    }

    [Fact]
    public void GrantMoney_NegativeDelta_DeductsButClampsAtZero()
    {
        var sm = new StatManager(MakeChar(money: 100));
        sm.GrantMoney(-30);
        Assert.Equal(70u, sm.Money);

        sm.GrantMoney(-1000);
        Assert.Equal(0u, sm.Money);
    }

    [Fact]
    public void GrantMoney_ZeroDelta_NoEvent()
    {
        var sm = new StatManager(MakeChar(money: 100));
        var fired = false;
        sm.MoneyChanged += _ => fired = true;
        sm.GrantMoney(0);
        Assert.False(fired);
    }

    [Fact]
    public void GrantExp_FiresEventWithNewTotal()
    {
        var sm = new StatManager(MakeChar(exp: 250));
        ExpChangedEvent? captured = null;
        sm.ExpChanged += e => captured = e;

        sm.GrantExp(100);

        Assert.Equal(350ul, sm.Exp);
        Assert.NotNull(captured);
        Assert.Equal(350ul, captured!.NewTotal);
        Assert.Equal(100, captured.Delta);
    }

    [Fact]
    public void SetHp_DifferentValue_FiresHealthChanged()
    {
        var sm = new StatManager(MakeChar(hp: 100));
        HealthChangedEvent? captured = null;
        sm.HealthChanged += e => captured = e;

        sm.SetHp(75);

        Assert.Equal(75u, sm.CurrentHp);
        Assert.Equal(75u, captured!.NewHp);
    }

    [Fact]
    public void SetHp_SameValue_NoEvent()
    {
        var sm = new StatManager(MakeChar(hp: 100));
        var fired = false;
        sm.HealthChanged += _ => fired = true;
        sm.SetHp(100);
        Assert.False(fired);
    }

    [Fact]
    public void TakeHit_LowersHpWithoutAHealthEventAndStopsAtZero()
    {
        var sm = new StatManager(MakeChar(hp: 100));
        var fired = false;
        sm.HealthChanged += _ => fired = true;

        Assert.Equal(94u, sm.TakeHit(6));
        Assert.Equal(94u, sm.TakeHit(0));
        Assert.Equal(0u, sm.TakeHit(500));
        Assert.Equal(0u, sm.CurrentHp);
        Assert.False(fired);
    }

    [Fact]
    public void Die_EmptiesHpAndTakesTwoPerCentOfExpWithoutAnEvent()
    {
        var sm = new StatManager(MakeChar(exp: 1050, hp: 100));
        var fired = false;
        sm.ExpChanged += _ => fired = true;
        sm.HealthChanged += _ => fired = true;

        Assert.Equal(1029ul, sm.Die());
        Assert.Equal(0u, sm.CurrentHp);
        Assert.Equal(1029ul, sm.Exp);
        Assert.Equal(21ul, sm.DeathExpLoss);
        Assert.False(fired);
    }

    [Fact]
    public void Resurrect_SetsHpRaisesMpToItsFloorAndGivesTheLostExpBack()
    {
        var sm = new StatManager(MakeChar(exp: 1050, hp: 100, mp: 5));
        sm.Die();

        sm.Resurrect(maxHp: 120, maxMp: 80, hpRatio: 1f, mpRatio: 1f, expRatio: 1f);

        Assert.Equal(120u, sm.CurrentHp);
        Assert.Equal(80u, sm.CurrentMp);
        Assert.Equal(1050ul, sm.Exp);
        Assert.Equal(0ul, sm.DeathExpLoss);
    }

    [Fact]
    public void Resurrect_WithPartRatiosTruncatesAndNeverLowersMp()
    {
        var sm = new StatManager(MakeChar(exp: 1050, hp: 100, mp: 60));
        sm.Die();

        sm.Resurrect(maxHp: 125, maxMp: 80, hpRatio: 0.5f, mpRatio: 0.5f, expRatio: 0.5f);

        Assert.Equal(62u, sm.CurrentHp);
        Assert.Equal(60u, sm.CurrentMp);
        Assert.Equal(1039ul, sm.Exp);
    }

    [Fact]
    public void RestartHp_IsThirtyPerCentOfTheMaximumInIntegerMaths()
    {
        Assert.Equal(36u, StatManager.RestartHp(120));
        Assert.Equal(37u, StatManager.RestartHp(125));
        Assert.Equal(0u, StatManager.RestartHp(3));
    }

    [Fact]
    public void RestartInTown_GivesExpBackByRatioThenSetsThirtyPerCentHpAndClearsTheRecord()
    {
        var sm = new StatManager(MakeChar(exp: 1050, hp: 100));
        sm.Die();

        Assert.Equal(1050ul, sm.GiveBackDeathExp(1f));
        Assert.Equal(36u, sm.Restart(120));
        Assert.Equal(36u, sm.CurrentHp);
        Assert.Equal(0ul, sm.DeathExpLoss);
    }

    private static readonly LevelExpChart FourLevels =
        LevelExpChart.Parse("1 50 500 500 2 68 680 1180 3 98 980 2160 4 144 1440 3600");

    private static readonly TmLevelChart ThreeTmLevels =
        new(new[] { (0, 320), (1, 460), (2, 680), (3, 1000) });

    [Fact]
    public void AddExp_BelowTheNextLevelOnlyAdds()
    {
        var sm = new StatManager(MakeChar(exp: 100));

        var gain = sm.AddExp(399, FourLevels);

        Assert.Equal(new ExpGain(ExpGainKind.Added, 0, 1, 499), gain);
        Assert.Equal(499ul, sm.Exp);
        Assert.Equal(1, sm.Level);
    }

    [Fact]
    public void AddExp_CarriesTheRemainderOverSeveralLevels()
    {
        var sm = new StatManager(MakeChar(exp: 100));

        var gain = sm.AddExp(400 + 680 + 5, FourLevels);

        Assert.Equal(new ExpGain(ExpGainKind.LevelUp, 2, 3, 5), gain);
        Assert.Equal(3, sm.Level);
        Assert.Equal(5ul, sm.Exp);
    }

    [Fact]
    public void AddExp_WhenTheChartHasNoNextRowTheLevelStays()
    {
        var sm = new StatManager(MakeChar(exp: 0, level: 4));

        var gain = sm.AddExp(1500, FourLevels);

        Assert.Equal(new ExpGain(ExpGainKind.Withheld, 0, 4, 60), gain);
        Assert.Equal(4, sm.Level);
    }

    [Fact]
    public void AddExp_AtTheLastLevelAddsNothing()
    {
        var sm = new StatManager(MakeChar(exp: 7, level: LevelExpChart.LastLevel));

        var gain = sm.AddExp(1000, FourLevels);

        Assert.Equal(ExpGainKind.Withheld, gain.Kind);
        Assert.Equal(7ul, sm.Exp);
    }

    [Fact]
    public void AddTmExp_WithoutALevelTheTotalIsReported()
    {
        var sm = new StatManager(MakeChar(tmExp: 256));

        var gain = sm.AddTmExp(0, ThreeTmLevels);

        Assert.True(gain.SendsTotal);
        Assert.Empty(gain.LevelUps);
        Assert.Equal(256u, gain.TmExp);
    }

    [Fact]
    public void AddTmExp_EachLevelGivesOnePointAndItsOwnStep()
    {
        var sm = new StatManager(MakeChar(tmExp: 300, tmPoint: 2));

        var gain = sm.AddTmExp(20 + 460 + 9, ThreeTmLevels);

        Assert.False(gain.SendsTotal);
        Assert.Equal(
            new[] { new TmLevelStep(1, 469, 3), new TmLevelStep(2, 9, 4) },
            gain.LevelUps);
        Assert.Equal(2, sm.TmLevel);
        Assert.Equal(9u, sm.TmExp);
        Assert.Equal(4, sm.TmPoint);
    }

    [Fact]
    public void AddTmExp_AtTheLastLevelAddsNothingAndReportsNothing()
    {
        var sm = new StatManager(MakeChar(tmExp: 5, tmLevel: TmLevelChart.LastLevel));

        var gain = sm.AddTmExp(1000, ThreeTmLevels);

        Assert.False(gain.SendsTotal);
        Assert.Empty(gain.LevelUps);
        Assert.Equal(5u, sm.TmExp);
    }

    [Fact]
    public void SetVitals_StoresHpAndMpWithoutAnEvent()
    {
        var sm = new StatManager(MakeChar(hp: 3, mp: 1));

        var raised = 0;
        sm.HealthChanged += _ => raised++;
        sm.ManaChanged += _ => raised++;

        sm.SetVitals(210, 80);

        Assert.Equal(210u, sm.CurrentHp);
        Assert.Equal(80u, sm.CurrentMp);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void ApplyQuestRewards_GrantsAllNonZeroFields()
    {
        var sm = new StatManager(MakeChar(money: 50, exp: 100));
        var moneyEvents = 0;
        var expEvents = 0;
        var tmEvents = 0;
        sm.MoneyChanged += _ => moneyEvents++;
        sm.ExpChanged += _ => expEvents++;
        sm.TmExpChanged += _ => tmEvents++;

        sm.ApplyQuestRewards(gelder: 200, exp: 500, tmxp: 50);

        Assert.Equal(250u, sm.Money);
        Assert.Equal(600ul, sm.Exp);
        Assert.Equal(50u, sm.TmExp);
        Assert.Equal(1, moneyEvents);
        Assert.Equal(1, expEvents);
        Assert.Equal(1, tmEvents);
    }

    [Fact]
    public void ApplyQuestRewards_AllZero_NoEventsFire()
    {
        var sm = new StatManager(MakeChar());
        var anyFired = false;
        sm.MoneyChanged += _ => anyFired = true;
        sm.ExpChanged += _ => anyFired = true;
        sm.TmExpChanged += _ => anyFired = true;

        sm.ApplyQuestRewards(gelder: 0, exp: 0, tmxp: 0);

        Assert.False(anyFired);
    }

    private static LevelExpTable Table(params (int level, ulong exp)[] rows)
    {
        var d = new Dictionary<int, ulong>();
        foreach (var (l, e) in rows) d[l] = e;
        return LevelExpTable.FromInlineThresholds(d);
    }

    [Fact]
    public void GrantExp_NoTable_OnlyAccumulates()
    {
        var sm = new StatManager(MakeChar(exp: 0, level: 1));
        bool levelFired = false;
        sm.LevelUp += _ => levelFired = true;

        sm.GrantExp(10000);

        Assert.Equal(10000ul, sm.Exp);
        Assert.Equal((ushort)1, sm.Level);
        Assert.False(levelFired);
    }

    [Fact]
    public void GrantExp_WithTable_LevelsUpAndCarriesExpOver()
    {
        var table = Table((1, 500ul), (2, 680ul), (3, 980ul));
        var sm = new StatManager(MakeChar(exp: 0, level: 1), expTable: table);
        LevelChangedEvent? captured = null;
        sm.LevelUp += e => captured = e;

        sm.GrantExp(700);

        Assert.Equal((ushort)2, sm.Level);
        Assert.Equal(200ul, sm.Exp);
        Assert.NotNull(captured);
        Assert.Equal((ushort)2, captured!.NewLevel);
    }

    [Fact]
    public void GrantExp_WithTable_OneGrantCanBumpMultipleLevels()
    {
        var table = Table((1, 500ul), (2, 680ul), (3, 980ul), (4, 1440ul));
        var sm = new StatManager(MakeChar(exp: 0, level: 1), expTable: table);

        sm.GrantExp(2400);

        Assert.Equal((ushort)4, sm.Level);
        Assert.Equal(2400ul - 500ul - 680ul - 980ul, sm.Exp);
    }

    [Fact]
    public void GrantExp_RespectsMaxLevelCap()
    {
        var table = Table((1, 100ul), (2, 100ul), (3, 100ul));
        var caps = new ServerRates.CapsSection { MaxLevel = 3, StatPtsPerLevel = 4 };
        var sm = new StatManager(MakeChar(exp: 0, level: 1), expTable: table, capsProvider: () => caps);

        sm.GrantExp(10000);

        Assert.Equal((ushort)3, sm.Level);
    }

    [Fact]
    public void GrantExp_StopsLoopingWhenThresholdMissing()
    {
        var table = Table((1, 100ul));
        var sm = new StatManager(MakeChar(exp: 0, level: 1), expTable: table);

        sm.GrantExp(10000);

        Assert.Equal((ushort)2, sm.Level);
    }

    [Fact]
    public void GrantTmExp_WithTable_BumpsTmLevelAndTmPoint()
    {
        var table = LevelExpTable.FromInlineThresholds(
            new Dictionary<int, ulong>(),
            new Dictionary<int, ulong> { [0] = 320, [1] = 460 });
        var sm = new StatManager(MakeChar(tmExp: 0, tmLevel: 0, tmPoint: 0), expTable: table);
        TmLevelChangedEvent? captured = null;
        sm.TmLevelUp += e => captured = e;

        sm.GrantTmExp(800);

        Assert.Equal((ushort)2, sm.TmLevel);
        Assert.Equal((ushort)2, sm.TmPoint);
        Assert.Equal(800u - 320u - 460u, sm.TmExp);
        Assert.NotNull(captured);
        Assert.Equal((ushort)2, captured!.NewTmLevel);
    }

    [Fact]
    public void RecomputeAccPts_AtLevel1_AllZero()
    {
        var pts = StatManager.RecomputeAccPts(6, 3, 3, 3, level: 1);
        Assert.Equal(0, pts.ApPts);
        Assert.Equal(0, pts.AcPts);
        Assert.Equal(0, pts.DxPts);
        Assert.Equal(0, pts.MaxMpPts);
        Assert.Equal(0, pts.MaxHpPts);
        Assert.Equal(0, pts.AttrPts);
    }

    [Fact]
    public void RecomputeAccPts_PrimaryPower_AtLevel5()
    {
        var pts = StatManager.RecomputeAccPts(6, 3, 3, 3, level: 5);
        Assert.Equal(24, pts.ApPts);
        Assert.Equal(24, pts.AcPts);
        Assert.Equal(24, pts.DxPts);
        Assert.Equal(12, pts.MaxMpPts);
        Assert.Equal(12, pts.MaPts);
        Assert.Equal(12, pts.MdPts);
        Assert.Equal(12, pts.MaxWtPts);
        Assert.Equal(12, pts.DaPts);
        Assert.Equal(12, pts.LkPts);
        Assert.Equal(12, pts.MaxHpPts);
        Assert.Equal(12, pts.DpPts);
        Assert.Equal(12, pts.HvPts);
        Assert.Equal(16, pts.AttrPts);
    }

    [Fact]
    public void RecomputeAccPts_PrimaryCharm_AtLevel10()
    {
        var pts = StatManager.RecomputeAccPts(3, 3, 3, 6, level: 10);
        Assert.Equal(27, pts.ApPts);
        Assert.Equal(54, pts.MaxHpPts);
        Assert.Equal(54, pts.DpPts);
        Assert.Equal(54, pts.HvPts);
        Assert.Equal(36, pts.AttrPts);
    }

    [Fact]
    public void RecomputeAccPts_LargeLevel_DoesNotOverflow()
    {
        var pts = StatManager.RecomputeAccPts(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, level: 1000);
        Assert.Equal(ushort.MaxValue, pts.ApPts);
        Assert.Equal(ushort.MaxValue, pts.MaxHpPts);
    }

    [Fact]
    public void RecomputeAccPts_Level0_NoNegative()
    {
        var pts = StatManager.RecomputeAccPts(6, 3, 3, 3, level: 0);
        Assert.Equal(0, pts.ApPts);
        Assert.Equal(0, pts.AttrPts);
    }

    [Fact]
    public void SpendTmPoints_TakesThePointsAndStopsAtZero()
    {
        var sm = new StatManager(MakeChar(tmPoint: 3));

        Assert.Equal((ushort)2, sm.SpendTmPoints(1));
        Assert.Equal((ushort)0, sm.SpendTmPoints(5));
        Assert.Equal((ushort)0, sm.TmPoint);
    }
}
