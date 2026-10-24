using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class EventSkillsTests
{
    private static readonly int[] Bases = [200, 100, 50, 300, 400, 500, 1000, 60, 80, 900, 120, 140];

    private static SkillTableRow Row(Dictionary<string, string> columns) => new() { Table = "ESAction_GMCheerAttack", Level = 1, Columns = columns };

    [Fact]
    public void EmotesHeartsAndGmBuffsRouteByTheirTarget()
    {
        Assert.True(EventSkills.IsSelf(EventSkills.HeartSpring));
        Assert.True(EventSkills.IsSelf(EventSkills.BubbleFlame));
        Assert.True(EventSkills.IsEmoteTarget(EventSkills.SurprisePotpourri));
        Assert.True(EventSkills.IsTargeted(1400));
        Assert.True(EventSkills.IsTargeted(1421));
        Assert.False(EventSkills.IsTargeted(1422));
        Assert.True(EventSkills.IsGround(EventSkills.GmDignityDefence));
        Assert.True(SupportSkills.TargetsPlayers(EventSkills.GmCheerAttack));
        Assert.True(SkillActions.IsBuilt(EventSkills.HeartSpring));
        Assert.False(SkillActions.IsBuilt(1306));
        Assert.Equal(1426, EventSkills.HeartDoOf(1400));
        Assert.Equal(EventSkills.GmCheerAttackDo, EventSkills.GmDoOf(EventSkills.GmDignityAttack));
        Assert.Equal(EventSkills.GmCheerDefenceDo, EventSkills.GmDoOf(EventSkills.GmCheerDefence));
    }

    [Fact]
    public void TheHeartFliesAtTwoHundredAndAtMostFourSeconds()
    {
        Assert.Equal(1500, EventSkills.HeartFlightMs(300));
        Assert.Equal(EventSkills.HeartFlightLimitMs, EventSkills.HeartFlightMs(5000));
        Assert.Equal(0, EventSkills.HeartFlightMs(0));
        Assert.Equal(8, EventSkills.HeartLuck(80, 0.1f));
        Assert.Equal(0, EventSkills.HeartLuck(80, 0f));
    }

    [Fact]
    public void CheerAddsFlatValuesAndDignityAddsAPercentOfTheBase()
    {
        var cheer = Row(new() { ["ApUp"] = "1360", ["AcUp"] = "62", ["MaUp"] = "85", ["DaUp"] = "85", ["HvUp"] = "85" });
        var dignity = Row(new() { ["ApUp"] = "50", ["AcUp"] = "50", ["LkUp"] = "50" });

        var flat = EventSkills.GmDeltas(cheer, false, Bases);
        Assert.Equal(EventSkills.GmStats, flat.Select(entry => entry.Stat).ToArray());
        Assert.Equal((SkillEffectKinds.StatAp, 1360), flat[0]);
        Assert.Equal((SkillEffectKinds.StatMd, 0), flat[3]);
        Assert.Equal((SkillEffectKinds.StatHv, 85), flat[7]);

        var percent = EventSkills.GmDeltas(dignity, EventSkills.IsGmPercent(EventSkills.GmDignityAttackSkill), Bases);
        Assert.Equal((SkillEffectKinds.StatAp, 100), percent[0]);
        Assert.Equal((SkillEffectKinds.StatAc, 50), percent[1]);
        Assert.Equal((SkillEffectKinds.StatLk, 40), percent[5]);
    }

    [Fact]
    public void TheEventKindsCarryTheirServerRules()
    {
        Assert.Equal(tPoseidon.Core.Data.EquipmentSlot.Drill, SkillEffectKinds.Find(EventSkills.FirstAnniversary)!.RequiresWornSlot);
        Assert.Equal(SkillEffectKinds.StatLk, SkillEffectKinds.Find(EventSkills.FirstAnniversary)!.Stat);
        Assert.Equal(SkillPeriodicHit.CoupleHeart, SkillEffectKinds.Find(1410)!.Periodic);
        Assert.Equal((sbyte)-1, SkillEffectKinds.Find(1436)!.HeaderClass);
        Assert.Equal(EventSkills.HeartDoMs, SkillEffectKinds.Find(1447)!.FixedDurationMs);
        Assert.True(SkillEffectKinds.Find(EventSkills.GmCheerDefenceDo)!.GmStats);
        Assert.True(MonsterSkills.Castable(MonsterSkills.MagicStorm, 1));
        Assert.True(MonsterSkills.Castable(MonsterSkills.EarthDivide, 10));
    }
}
