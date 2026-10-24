using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class HarconSkillsTests
{
    private static SkillTableRow Row(Dictionary<string, string> columns) => new() { Table = "ESAction_Mon_Pressure", Level = 1, Columns = columns };

    private static MonsterTacticEntry Timed(int id, int factorType, int factorValue, int ratio = 100) => new()
    {
        Id = id, TacticsName = "Tactics_Mon1098", FactorType = factorType, FactorValue = factorValue, ActionType = MonsterTactics.CastActionType,
        SkillId = 5131, SkillLevel = 2, TimeSeconds = 10, ActionRatio = ratio, RandomWeight = 0,
    };

    [Fact]
    public void SlotsSitAroundTheCentreAndSkipEmptyColumns()
    {
        var row = Row(new()
        {
            ["XPos1"] = "0", ["YPos1"] = "300", ["HitTime1"] = "2000",
            ["XPos2"] = "-120", ["YPos2"] = "-240", ["HitTime2"] = "2000", ["WaitTime2"] = "500",
        });

        var slots = HarconSkills.Slots(row, (1000, 1000));

        Assert.Equal(new[] { new HarconSlot(0, 1000, 1300, 0, 2000), new HarconSlot(1, 880, 760, 500, 2000) }, slots);
    }

    [Fact]
    public void PickTakesHitCountDistinctSlotsInSlotOrder()
    {
        var slots = Enumerable.Range(0, 8).Select(index => new HarconSlot((byte)index, index, index, 0, 2000)).ToList();

        var picked = HarconSkills.Pick(slots, 3, bound => bound - 1);

        Assert.Equal(3, picked.Count);
        Assert.Equal(picked.Select(slot => slot.Slot).Distinct().Count(), picked.Count);
        Assert.True(picked.SequenceEqual(picked.OrderBy(slot => slot.Slot)));
        Assert.Equal(8, HarconSkills.Pick(slots, 20, bound => 0).Count);
    }

    [Fact]
    public void ThePlanAddsWaitingSlotsAndFiresEachSlotOnce()
    {
        var plan = new HarconPlan(new[] { new HarconSlot(0, 1, 1, 0, 2000), new HarconSlot(1, 2, 2, 500, 2000) });

        Assert.Single(plan.Initial);
        Assert.Equal(2500, plan.LastMs);
        Assert.Empty(plan.TakeDue(100));
        Assert.Equal(new[] { (plan.Slots[1], HarconStep.Add) }, plan.TakeDue(600));
        Assert.Equal(new[] { (plan.Slots[0], HarconStep.Fire) }, plan.TakeDue(2000));
        Assert.Equal(new[] { (plan.Slots[1], HarconStep.Fire) }, plan.TakeDue(2600));
        Assert.True(plan.Done);
        Assert.Empty(plan.TakeDue(9000));
    }

    [Fact]
    public void TimedTacticsFireOnTheirScheduleAheadOfTheWeights()
    {
        var tactics = new MonsterTactics(new[] { Timed(1, MonsterTactics.EveryFactor, 7000), Timed(2, MonsterTactics.OnceAfterFactor, 3000) });

        tactics.Advance(2500);
        tactics.Rearm(0, null, 1);
        Assert.Null(tactics.Pick(_ => 0));

        tactics.Advance(1000);
        tactics.Rearm(0, null, 1);
        Assert.Equal(2, tactics.Pick(_ => 0)!.Id);

        tactics.Advance(4000);
        tactics.Rearm(0, null, 1);
        Assert.Equal(1, tactics.Pick(_ => 0)!.Id);
        tactics.Advance(11000);
        tactics.Rearm(0, null, 1);
        Assert.Equal(1, tactics.Pick(_ => 0)!.Id);
        tactics.Advance(11000);
        tactics.Rearm(0, null, 1);
        Assert.Equal(1, tactics.Pick(_ => 0)!.Id);
    }

    [Fact]
    public void TheEventVisualsAndHarconSkillsAreRouted()
    {
        Assert.True(EventSkills.IsPointEmote(EventSkills.FireCracker));
        Assert.True(EventSkills.IsSelf(EventSkills.MoonRabbit));
        Assert.True(EventSkills.IsSelf(EventSkills.NumberOne));
        Assert.True(EventSkills.IsTargeted(EventSkills.DancingRudolf));
        Assert.True(SkillActions.IsBuilt(EventSkills.HappyXmas));
        Assert.True(MonsterSkills.Castable(HarconSkills.CronosImpact, 1));
        Assert.Equal(SkillPeriodicHit.HarconSlots, SkillEffectKinds.Find(HarconSkills.TimeBomb)!.Periodic);
        Assert.Equal(tPoseidon.Core.Data.EquipmentSlot.Drill, SkillEffectKinds.Find(EventSkills.SecondAnniversary)!.RequiresWornSlot);
    }
}
