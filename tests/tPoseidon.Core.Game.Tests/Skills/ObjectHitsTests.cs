using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class ObjectHitsTests
{
    private static readonly ushort[] BrandishVictims = new ushort[] { 11, 10 };
    private static readonly ushort[] ThunderTwo = new ushort[] { 5, 6 };
    private static readonly ushort[] ThunderFive = new ushort[] { 5, 6, 7, 8 };
    private static readonly ushort[] SkyRayVictims = new ushort[] { 5, 7 };
    private static readonly ushort[] WithFlyers = new ushort[] { 4, 5 };
    private static readonly ushort[] WithoutFlyers = new ushort[] { 4 };
    private static readonly int[] WhisperRounds = new[] { 500, 2500, 4500 };

    private static AreaCandidate Monster(ushort id, int x, int y, int type = 1, ushort owner = 0, bool dead = false) => new(id, x, y, dead, type, owner);

    [Fact]
    public void SharpBrandishTakesTheTargetAndOnlyMonstersInTheConeBehindIt()
    {
        var target = Monster(10, 100, 0);
        var candidates = new[]
        {
            Monster(11, 300, 0),
            target,
            Monster(12, 100, 150),
            Monster(13, 200, 0, type: SkillAreaShapes.FlyerMonsterType),
            Monster(14, 600, 0),
        };

        var victims = ObjectHits.SharpBrandishVictims(1, (0, 0), target, 400, candidates);

        Assert.Equal(BrandishVictims, victims);
    }

    [Fact]
    public void SharpBrandishStopsAtSevenVictims()
    {
        var target = Monster(1, 50, 0);
        var candidates = Enumerable.Range(0, 10).Select(i => Monster((ushort)(1 + i), 50 + 5 * i, 0)).ToList();

        Assert.Equal(ObjectHits.SharpBrandishLimit, ObjectHits.SharpBrandishVictims(99, (0, 0), target, 500, candidates).Count);
    }

    [Fact]
    public void ThunderBoltCountsTheTargetFirstAgainstKillNum()
    {
        var target = Monster(5, 0, 0);
        var candidates = new[] { Monster(6, 10, 0), target, Monster(7, 20, 0), Monster(8, 30, 0), Monster(9, 300, 0) };

        Assert.Equal(ThunderTwo, ObjectHits.TargetThenAround(1, target, 250, 2, candidates));
        Assert.Equal(ThunderFive, ObjectHits.TargetThenAround(1, target, 250, 5, candidates));
    }

    [Fact]
    public void SkyRayPutsTheTargetFirstOnlyWhenSomethingWasCollected()
    {
        var target = Monster(5, 0, 0, owner: 77);
        var others = new[] { Monster(6, 10, 0), Monster(7, 20, 0) };

        Assert.Equal(SkyRayVictims, ObjectHits.SkyRayVictims(1, target, 150, others));
        Assert.Empty(ObjectHits.SkyRayVictims(1, target, 150, Array.Empty<AreaCandidate>()));
    }

    [Fact]
    public void AroundSkipsDeadOwnedAndUntouchableMonsters()
    {
        var candidates = new[]
        {
            Monster(1, 0, 0, dead: true),
            Monster(2, 0, 0, owner: 50),
            Monster(3, 0, 0, type: SkillAreaShapes.UntouchableMonsterType),
            Monster(4, 0, 0, owner: 9),
            Monster(5, 0, 0, type: SkillAreaShapes.FlyerMonsterType),
        };

        Assert.Equal(WithFlyers, ObjectHits.Around(9, (0, 0), 10, 7, skipsFlyers: false, candidates));
        Assert.Equal(WithoutFlyers, ObjectHits.Around(9, (0, 0), 10, 7, skipsFlyers: true, candidates));
    }

    [Fact]
    public void PowersFollowTheServerFormulas()
    {
        Assert.Equal((int)(1000 * (double)1.15f), ObjectHits.RatioPower(1000, 1.15f));
        Assert.Equal(40 * 3 + 1550, ObjectHits.ElementPower(40, 1550));
        Assert.Equal(1500 + 750, ObjectHits.CelestialPower(1500, ObjectHits.NoCelestialScan));
        Assert.Equal(1500 + (int)(1500 * (double)(float)(2 + 10.0) / 20.0), ObjectHits.CelestialPower(1500, 2));
        Assert.Equal(90 * 8 + 400, ObjectHits.FlashPower(400, 90, power: false));
        Assert.Equal(90 * 16 + 400, ObjectHits.FlashPower(400, 90, power: true));
        Assert.Equal((int)((300 + 8 * 50) * (double)2.05f), ObjectHits.CardPower(300, 50, 2.05f));
        Assert.Equal((int)(100 * (double)5.0f), ObjectHits.GalderPower(ObjectHits.GellderCost, 5.0f));
        Assert.Equal(150, ObjectHits.GalderCostOf(ObjectHits.MultiGellder));
    }

    [Fact]
    public void CardThrowFallsOffWithDistance()
    {
        Assert.Equal(1000, ObjectHits.CardFalloff(1000, 0f, 50));
        Assert.Equal((int)(1000 * (double)(float)(1.0 - 25f / 50)), ObjectHits.CardFalloff(1000, 25f, 50));
        Assert.Equal(0, ObjectHits.CardFalloff(1000, 50f, 50));
    }

    [Fact]
    public void KamikazeKeepsTheRemainRatioAndPassesOnTheLoss()
    {
        var (lost, victimDamage) = ObjectHits.KamikazeBlast(1000, 0.2f, 1.5f);

        Assert.Equal(1000 - (int)(1000 * (double)0.2f), lost);
        Assert.Equal((int)(lost * (double)1.5f), victimDamage);
    }

    [Fact]
    public void FireBurnLandsOnTheLuckRoll()
    {
        var chance = (int)((60 - 40 + 90) * 1.2f * 0.6f);

        Assert.True(ObjectHits.Lands(60, 40, 0.6f, chance - 1));
        Assert.False(ObjectHits.Lands(60, 40, 0.6f, chance));
        Assert.True(ObjectHits.Lands(0, 500, 1f, 19));
        Assert.False(ObjectHits.Lands(0, 500, 1f, 20));
    }

    [Fact]
    public void DarkWhisperStrikesAtHalfASecondThenEveryTwoSeconds()
    {
        var due = Enumerable.Range(1, 12).Where(ObjectHits.DarkWhisperRoundDue).Select(count => count * ObjectHits.DarkWhisperFirstMs);

        Assert.Equal(WhisperRounds, due);
    }

    [Fact]
    public void LimitIsCappedAndNeverNegative()
    {
        var row = new SkillTableRow { Table = "ESAction_WindSlash", Level = 1, Columns = new Dictionary<string, string> { ["KillNum"] = "12" } };

        Assert.Equal(ObjectHits.WindSlashVictims, ObjectHits.LimitOf(row, ObjectHits.WindSlashVictims));
        Assert.Equal(0, ObjectHits.LimitOf(new SkillTableRow { Table = "x", Level = 1, Columns = new Dictionary<string, string>() }, 5));
    }

    [Fact]
    public void ObjectsLiveWithoutStatsOrTm()
    {
        var kind = ObjectHits.LifeOf(ObjectHits.GaleBreak, SkillPeriodicHit.GaleBurst);

        Assert.False(kind.ChangesStat);
        Assert.False(kind.GrantsTm);
        Assert.Equal(SkillPeriodicHit.GaleBurst, kind.Periodic);
        Assert.True(SkillActions.IsBuilt(ObjectHits.FireBurn));
        Assert.True(GroundSkills.IsGround(ObjectHits.CelestialStrike));
        Assert.NotNull(SkillEffectKinds.Find(ObjectHits.FireBurnDo));
    }
}
