using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class CrazyHammerTests
{
    private static SkillTableRow Row(string table, int level, Dictionary<string, string> columns) => new()
    {
        Table = table, Level = level, Columns = columns,
    };

    private static readonly SkillTableRow CrazyFirst = Row("ESAction_CrazyHammer", 1, new()
    {
        ["Ratio"] = "2.000000", ["GaugeP"] = "195", ["Seconds"] = "20", ["DX"] = "30", ["AtPoint"] = "3", ["SkillPoint"] = "1",
    });

    private static readonly int[] Bases = { 100, 50, 12, 30, 20, 10, 5, 8, 4, 900, 120, 60 };

    private static readonly SkillTableRow TripleMaster = Row("ESAction_TripleCrazy", 11, new() { ["Ratio"] = "2.400000", ["Seconds"] = "20" });

    private static readonly SkillTableRow QuintupleFirst = Row("ESAction_QuintupleCrazy", 1, new()
    {
        ["KillNum"] = "2", ["Radius"] = "90", ["Seconds"] = "20",
    });

    [Fact]
    public void TheGaugeFillsUpToItsMaximumAndThenBurstsBackToZero()
    {
        var gauge = new CrazyHammerGauge();

        Assert.Equal(new CrazyGaugeFill(true, false, 3), gauge.Fill(3, 7));
        Assert.Equal(new CrazyGaugeFill(true, false, 6), gauge.Fill(3, 7));
        Assert.Equal(new CrazyGaugeFill(true, true, 0), gauge.Fill(3, 7));
        Assert.Equal(0, gauge.Current);
        Assert.False(gauge.Fill(0, 7).Changed);
    }

    [Fact]
    public void TheGaugeIsAnnouncedOncePerMap()
    {
        var gauge = new CrazyHammerGauge();

        Assert.True(gauge.NeedsAnnounce(3));
        Assert.Equal(0, gauge.Announce(0x500, 3));
        Assert.False(gauge.NeedsAnnounce(3));
        Assert.True(gauge.NeedsAnnounce(4));
        Assert.Equal(0x500, gauge.Announce(0x501, 4));
    }

    [Fact]
    public void CrazyModeAddsBaseAttackTimesTheRatiosToNormalAttacks()
    {
        Assert.Equal(300 + 200, CrazyHammer.NormalAttackAp(300, 100, CrazyFirst, null));
        Assert.Equal(300 + (int)(100 * 2.0f + 100 * 2.4f), CrazyHammer.NormalAttackAp(300, 100, CrazyFirst, TripleMaster));
    }

    [Fact]
    public void QuadrupleCrazyRaisesTheCriticalChanceByItsRatioInPoints()
    {
        Assert.Equal(39, CrazyHammer.CriticalBonus(Row("ESAction_QuadrupleCrazy", 10, new() { ["CriticalRatio"] = "0.390000" })));
        Assert.Equal(0, CrazyHammer.CriticalBonus(null));
    }

    [Fact]
    public void TheShockwaveHitsOthersAroundTheStruckMonsterUpToKillNumInAll()
    {
        var struck = new AreaCandidate(0x10, 100, 100, false, 0, 0);
        var victims = SkillAreaShapes.Collect(
            CrazyHammer.Shockwave, QuintupleFirst, 0x1, (60, 100), struck,
            new[]
            {
                struck,
                new AreaCandidate(0x11, 150, 100, false, 0, 0),
                new AreaCandidate(0x12, 120, 100, false, 0, 0),
                new AreaCandidate(0x13, 400, 100, false, 0, 0),
            });

        Assert.Equal(new ushort[] { 0x10, 0x11 }, victims);
        Assert.Equal(MeleeResolution.Normal(400, 100), CrazyHammer.ShockwaveDamage(400, 100));
    }

    [Fact]
    public void CrazyModeFixesDxAtTheNegativeColumnValue()
    {
        var kind = SkillEffectKinds.Find(CrazyHammer.ModeAction)!;
        var mode = new SkillEffect
        {
            ObjectId = 0x500, Kind = kind, SkillId = CrazyHammer.CrazyHammerSkill, EsClass = 0, CasterEntityId = 1, OwnerEntityId = 1,
            Amount = 30f, DurationMs = 20000, Mode = SkillEffectMode.Applied,
        };
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(mode);

        var deltas = book.Deltas(Bases, _ => null);

        Assert.Equal(SkillEffectKinds.StatDx, kind.Stat);
        Assert.Equal("DX", kind.AmountColumn);
        Assert.Equal(-30, 12 + deltas[SkillEffectKinds.StatDx]);
    }

    [Fact]
    public void ACriticalBonusTurnsMoreLandedHitsIntoCriticals()
    {
        var plain = 0;
        var boosted = 0;
        for (var seed = 0; seed < 400; seed++)
        {
            var outcome = MeleeResolution.PlayerAgainstMonster(500, 200, 0, MeleeResolution.NotArmed, 0, 0, 0, 100000, new Random(seed));
            var boostedOutcome = MeleeResolution.PlayerAgainstMonster(500, 200, 0, MeleeResolution.NotArmed, 0, 0, 0, 100000, new Random(seed), 45);
            if ((outcome.Result & MeleeResolution.ResultKindMask) >= MeleeResolution.ResultCritical) plain++;
            if ((boostedOutcome.Result & MeleeResolution.ResultKindMask) >= MeleeResolution.ResultCritical) boosted++;
        }

        Assert.True(boosted > plain);
    }
}
