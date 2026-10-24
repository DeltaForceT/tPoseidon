using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class SpecialItemRulesTests
{
    private static readonly int[] BothSkills = [1101, 1102];
    private static readonly int[] FirstSkill = [1101];
    private static readonly int[] SumOfRatios = [50];

    private static Character Hero(Species species = Species.Lion, byte jobState = 1, byte job2 = 0, byte job3 = 0) => new()
    {
        CharacterId = 1,
        OwnerAccountId = 1,
        Slot = 0,
        Name = "Hero",
        Species = species,
        FType = FType.Power,
        JobState = jobState,
        Job2 = job2,
        Job3 = job3,
    };

    [Theory]
    [InlineData(0, true)]
    [InlineData(0x14, true)]
    [InlineData(0x15, false)]
    [InlineData(0xFF, false)]
    public void HairColoursStopBelowTwentyOne(byte color, bool valid) =>
        Assert.Equal(valid, HairDyeRules.ValidColor(color));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(9, 3)]
    public void TheHairSlotFollowsTheShownJobTier(byte jobState, int slot) =>
        Assert.Equal(slot, HairDyeRules.SlotOf(Hero(jobState: jobState)));

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(3, 4)]
    [InlineData(4, 3)]
    [InlineData(5, 6)]
    [InlineData(6, 5)]
    [InlineData(7, 8)]
    [InlineData(8, 7)]
    public void GenderPairsAreOddEvenNeighbours(int type, int opposite) =>
        Assert.Equal((byte)opposite, GenderChangeRules.OppositeType(type));

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(18)]
    public void OnlyTheEightBaseTypesChangeGender(int type) =>
        Assert.Null(GenderChangeRules.OppositeType(type));

    [Fact]
    public void AJobChangeBlocksTheGenderChange()
    {
        Assert.False(GenderChangeRules.HasChangedJob(Hero()));
        Assert.True(GenderChangeRules.HasChangedJob(Hero(job2: 10)));
        Assert.True(GenderChangeRules.HasChangedJob(Hero(job3: 19)));
        Assert.True(GenderChangeRules.HasChangedJob(Hero(species: Species.PolarBear)));
    }

    [Fact]
    public void TheGenderChangeNeedsRoomForTheWornItemsAndTheTwoStarterItems() =>
        Assert.Equal(7, GenderChangeRules.BagSlotsNeeded(5));

    [Fact]
    public void AStatPointBackTakesPointsOffTheStatsAboveTheLevelBaseline()
    {
        short[] points = [50, 40, 40, 20, 20, 20, 10, 10, 10, 30, 30, 30];
        byte[] take = [10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        Assert.True(StatResetRules.TryPartial(take, points, new short[] { 4, 2, 1, 3 }, 11, out var reset, out var refunded));

        Assert.Equal(10, refunded);
        Assert.Equal(40, reset[0]);
        Assert.Equal(40, reset[1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(41)]
    public void AStatPointBackTakesBetweenOneAndFortyPoints(int total)
    {
        short[] points = [200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200];
        var take = new byte[12];
        take[0] = (byte)Math.Min(total, 255);

        Assert.False(StatResetRules.TryPartial(take, points, new short[] { 4, 2, 1, 3 }, 11, out _, out _));
    }

    [Fact]
    public void AStatPointBackRefusesToGoBelowTheBaselineOfAnyStat()
    {
        short[] points = [41, 40, 40, 20, 20, 20, 10, 10, 10, 30, 30, 30];
        byte[] take = [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        Assert.False(StatResetRules.TryPartial(take, points, new short[] { 4, 2, 1, 3 }, 11, out _, out _));
    }

    [Fact]
    public void ThePointBackAllResetsEveryStatToItsBaselineAndCountsTheDifference()
    {
        short[] points = [50, 40, 40, 20, 25, 20, 10, 10, 10, 30, 30, 31];

        Assert.True(StatResetRules.TryAll(points, new short[] { 4, 2, 1, 3 }, 11, out var reset, out var refunded));

        Assert.Equal(16, refunded);
        Assert.Equal(new short[] { 40, 40, 40, 20, 20, 20, 10, 10, 10, 30, 30, 30 }, reset);
    }

    [Fact]
    public void ThePointBackAllRefusesWhenNothingIsAboveTheBaseline()
    {
        short[] points = [40, 40, 40, 20, 20, 20, 10, 10, 10, 30, 30, 30];

        Assert.False(StatResetRules.TryAll(points, new short[] { 4, 2, 1, 3 }, 11, out _, out _));
    }

    [Fact]
    public void ThePointBackAllRefusesAStatBelowItsBaseline()
    {
        short[] points = [39, 40, 40, 20, 20, 20, 10, 10, 10, 30, 30, 90];

        Assert.False(StatResetRules.TryAll(points, new short[] { 4, 2, 1, 3 }, 11, out _, out _));
    }

    [Theory]
    [InlineData(new byte[] { 4, 3, 2, 1 }, 0, true)]
    [InlineData(new byte[] { 3, 4, 2, 1 }, 1, true)]
    [InlineData(new byte[] { 3, 4, 2, 1 }, 0, false)]
    [InlineData(new byte[] { 4, 3, 2, 2 }, 0, false)]
    [InlineData(new byte[] { 4, 3, 2, 0 }, 0, false)]
    [InlineData(new byte[] { 4, 5, 1, 0 }, 0, false)]
    [InlineData(new byte[] { 4, 3, 3 }, 0, false)]
    public void TheGrowthGraphNeedsWeightsFromOneToFourSummingToTenWithFourOnTheMainGroup(byte[] weights, int group, bool valid) =>
        Assert.Equal(valid, StatResetRules.ValidGrowthWeights(weights, group));

    [Fact]
    public void TheGrowthGraphResetGivesEveryStatLevelMinusOneTimesItsGroupWeight()
    {
        var points = StatResetRules.GrowthPoints(new byte[] { 4, 3, 2, 1 }, 11);

        Assert.Equal(new short[] { 40, 40, 40, 30, 30, 30, 20, 20, 20, 10, 10, 10 }, points);
        Assert.Equal(40, StatResetRules.GrowthBonus(11));
        Assert.True(StatResetRules.GrowthConsistent(points, 40, 11));
        Assert.False(StatResetRules.GrowthConsistent(points, 39, 11));
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(4999, true)]
    [InlineData(5000, false)]
    [InlineData(6001, false)]
    public void SpecialSkillsCannotBeReset(int skillId, bool resettable) =>
        Assert.Equal(resettable, !SkillResetRules.IsSpecial(skillId));

    [Fact]
    public void ASkillWhichIsAPrerequisiteOfAnotherOwnedSkillCannotBeReset()
    {
        var cards = new Dictionary<int, SkillCardEntry>
        {
            [1102] = new() { Id = 1, AffectionSkill = 1102, PreSkills = [new SkillRequirement(1101, 3)] },
            [1101] = new() { Id = 2, AffectionSkill = 1101 },
        };

        Assert.True(SkillResetRules.IsPrerequisiteOfOwned(1101, BothSkills, skill => cards.GetValueOrDefault(skill)));
        Assert.False(SkillResetRules.IsPrerequisiteOfOwned(1101, FirstSkill, skill => cards.GetValueOrDefault(skill)));
        Assert.False(SkillResetRules.IsPrerequisiteOfOwned(1102, BothSkills, skill => cards.GetValueOrDefault(skill)));
    }

    [Theory]
    [InlineData(5, 7)]
    [InlineData(1, 3)]
    [InlineData(11, 20)]
    public void TheSkillRefundIsAffectionPointsPlusLevelsGainedPlusTheMasteryCostLessOne(int level, int refund)
    {
        var card = new SkillCardEntry { Id = 1, AffectionSkill = 1104, AffectionPoint = 3, RegisterLevel = 1 };

        Assert.Equal(refund, SkillResetRules.Refund(card, level, 8));
    }

    [Theory]
    [InlineData(80, true)]
    [InlineData(792, true)]
    [InlineData(793, false)]
    [InlineData(800, false)]
    public void TheStorageStopsAtEightHundredSlots(int slots, bool fits) =>
        Assert.Equal(fits, StorageExpansionRules.Fits(slots));

    [Theory]
    [InlineData(1, true)]
    [InlineData(999, true)]
    [InlineData(0, false)]
    [InlineData(1000, false)]
    public void ZonesRunFromOneToNineHundredNinetyNine(int zone, bool valid) =>
        Assert.Equal(valid, PortRules.IsZone(zone));

    [Theory]
    [InlineData(0x80, true)]
    [InlineData(0x81, true)]
    [InlineData(0x400, false)]
    [InlineData(0, false)]
    public void AMapOptionBitRefusesWarping(int option, bool refuses) =>
        Assert.Equal(refuses, PortRules.MapRefusesWarp(option));

    [Theory]
    [InlineData(0x400, true)]
    [InlineData(0x80, false)]
    public void AMapOptionBitRefusesArrivals(int option, bool refuses) =>
        Assert.Equal(refuses, PortRules.MapRefusesArrival(option));

    [Fact]
    public void TheBroomPicksByCumulativeRatio()
    {
        var entry = new RandomPortEntry(1, "go", [new RandomPortZone(10, 20), new RandomPortZone(20, 10), new RandomPortZone(30, 20)]);

        Assert.Equal(10, PortRules.PickRandomZone(entry, _ => 0));
        Assert.Equal(10, PortRules.PickRandomZone(entry, _ => 19));
        Assert.Equal(20, PortRules.PickRandomZone(entry, _ => 20));
        Assert.Equal(20, PortRules.PickRandomZone(entry, _ => 29));
        Assert.Equal(30, PortRules.PickRandomZone(entry, _ => 30));
        Assert.Equal(30, PortRules.PickRandomZone(entry, bound => bound - 1));
    }

    [Fact]
    public void TheBroomRollsOverTheSumOfTheRatios()
    {
        var entry = new RandomPortEntry(1, "go", [new RandomPortZone(10, 20), new RandomPortZone(20, 30)]);
        var bounds = new List<int>();

        PortRules.PickRandomZone(entry, bound =>
        {
            bounds.Add(bound);
            return 0;
        });

        Assert.Equal(SumOfRatios, bounds);
    }

    [Fact]
    public void ARowWithoutRatiosPicksNothing() =>
        Assert.Null(PortRules.PickRandomZone(new RandomPortEntry(1, "go", []), _ => 0));

    [Fact]
    public void OnlyTheEffectsWithServerCodeAreBuilt()
    {
        foreach (var effect in new[] { 14, 15, 19, 20, 21, 23, 24, 25, 26, 27, 28, 31, 32, 39 })
        {
            Assert.True(SpecialItemEffects.IsBuilt(effect));
        }
        Assert.False(SpecialItemEffects.IsBuilt(22));
        Assert.False(SpecialItemEffects.IsBuilt(33));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(33, 0, true)]
    [InlineData(35, 0, true)]
    [InlineData(0, 77, false)]
    [InlineData(0, 75, false)]
    [InlineData(0, 71, false)]
    [InlineData(22, 0, false)]
    public void ThePlainUseBranchSkipsTheSubTypesWithTheirOwnHelpers(int effect, int subType, bool plain) =>
        Assert.Equal(plain, SpecialItemEffects.IsPlainUse(effect, subType));

    [Fact]
    public void TheNormalFormIsRequiredByTheDyeTheGenderChangeAndTheResets()
    {
        foreach (var effect in new[] { 15, 21, 23, 24, 25, 26 }) Assert.True(SpecialItemEffects.RequiresNormalForm(effect));
        foreach (var effect in new[] { 14, 19, 20, 27, 28, 31, 32, 39 }) Assert.False(SpecialItemEffects.RequiresNormalForm(effect));
    }
}
