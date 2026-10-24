using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Pets;

public sealed class PetEvolveMergeRulesTests
{
    private static readonly PetLevelUpTables Tables = new(new Dictionary<int, PetLevelUpEntry>
    {
        [70000] = new(70000, 70022, 19719, 2, true),
        [70001] = new(70001, 70023, 19719, 2, false),
    });

    private static readonly ItemParamEntry Puppy = new() { Id = 70000, Class = 2, Type = 13, RefineIndex = 56 };

    private static ItemProperty Stat(int index, short value) => new(ItemPropertyTypes.Stat, ItemPropertyRules.FirstStatId + index, value, 0);

    [Fact]
    public void EvolveChecksRunInTheServerOrder()
    {
        var material = new HeldItem(19719, 2);
        Assert.Equal(PetEvolveRules.PetMissing, PetEvolveRules.Check(null, false, material, Tables, out _));
        Assert.Equal(PetEvolveRules.NotEvolvable, PetEvolveRules.Check(Puppy with { Type = 2 }, false, material, Tables, out _));
        Assert.Equal(PetEvolveRules.PetEquipped, PetEvolveRules.Check(Puppy, true, material, Tables, out _));
        Assert.Equal(PetEvolveRules.Failed, PetEvolveRules.Check(Puppy with { Id = 70099 }, false, material, Tables, out _));
        Assert.Equal(PetEvolveRules.NotEvolvable, PetEvolveRules.Check(Puppy with { Id = 70001 }, false, material, Tables, out _));
        Assert.Equal(PetEvolveRules.WrongMaterial, PetEvolveRules.Check(Puppy, false, new HeldItem(19720, 2), Tables, out _));
        Assert.Equal(PetEvolveRules.WrongMaterial, PetEvolveRules.Check(Puppy, false, null, Tables, out _));
        Assert.Equal(PetEvolveRules.NotEnoughMaterial, PetEvolveRules.Check(Puppy, false, new HeldItem(19719, 1), Tables, out _));
        Assert.Equal(PetEvolveRules.Ok, PetEvolveRules.Check(Puppy, false, material, Tables, out var row));
        Assert.Equal(70022, row!.ResultId);
    }

    [Fact]
    public void MergeChecksRunInTheServerOrder()
    {
        Assert.Equal(PetMergeRules.PetMissing, PetMergeRules.Check(null, false, Puppy, false));
        Assert.Equal(PetMergeRules.PetEquipped, PetMergeRules.Check(Puppy, true, Puppy, false));
        Assert.Equal(PetMergeRules.NotPet, PetMergeRules.Check(Puppy with { Type = 2 }, false, Puppy, false));
        Assert.Equal(PetMergeRules.PetMissing, PetMergeRules.Check(Puppy, false, null, false));
        Assert.Equal(PetMergeRules.PetEquipped, PetMergeRules.Check(Puppy, false, Puppy, true));
        Assert.Equal(PetMergeRules.DifferentPets, PetMergeRules.Check(Puppy, false, Puppy with { Id = 70001 }, false));
        Assert.Equal(PetMergeRules.LowGrade, PetMergeRules.Check(Puppy with { RefineIndex = 45 }, false, Puppy with { RefineIndex = 45 }, false));
        Assert.Equal(PetMergeRules.Ok, PetMergeRules.Check(Puppy, false, Puppy, false));
    }

    [Fact]
    public void GainsCountBetterStatsAndMoreSlots()
    {
        var first = new[] { Stat(0, 10), Stat(1, 20), Stat(2, -5) };
        var second = new[] { Stat(0, 12), Stat(1, 20), Stat(2, -8) };
        Assert.Equal(2, PetMergeRules.Gains(first, second, 1, 1));
        Assert.Equal(3, PetMergeRules.Gains(first, second, 1, 2));
        Assert.Equal(0, PetMergeRules.Gains(second, first, 2, 1));
    }

    [Fact]
    public void BestGradeTakesTheBetterValueAndRandomGradeRollsBetween()
    {
        var first = new[] { Stat(0, 10), Stat(2, -5) };
        var second = new[] { Stat(0, 20), Stat(2, -8) };
        var best = PetMergeRules.Merged(PetMergeRules.BestGrade, first, second, Array.Empty<ItemProperty>(), () => 0);
        Assert.Equal(20, best.Single(p => p.Id == ItemPropertyRules.FirstStatId).Value);
        Assert.Equal(-8, best.Single(p => p.Id == ItemPropertyRules.FirstStatId + 2).Value);
        var rolled = PetMergeRules.Merged(PetMergeRules.RandomGrade, first, second, Array.Empty<ItemProperty>(), () => 3);
        Assert.Equal(13, rolled.Single(p => p.Id == ItemPropertyRules.FirstStatId).Value);
        Assert.Equal(-5, rolled.Single(p => p.Id == ItemPropertyRules.FirstStatId + 2).Value);
        var keep = PetMergeRules.Merged(4, first, second, Array.Empty<ItemProperty>(), () => 3);
        Assert.Equal(10, keep.Single(p => p.Id == ItemPropertyRules.FirstStatId).Value);
    }

    [Fact]
    public void MergedSlotsFollowTheGrade()
    {
        Assert.Equal(3, PetMergeRules.MergedSlots(PetMergeRules.BestGrade, 3, 1, () => 0));
        Assert.Equal(4, PetMergeRules.MergedSlots(PetMergeRules.BestGrade, 1, 4, () => 0));
        Assert.Equal(2, PetMergeRules.MergedSlots(PetMergeRules.RandomGrade, 1, 4, () => 1));
        Assert.Equal(1, PetMergeRules.MergedSlots(4, 1, 4, () => 1));
    }

    [Fact]
    public void LookOverridesCarryTheComposedLook()
    {
        var properties = new[]
        {
            new ItemProperty(ItemComposingRules.AppearanceType, ItemComposingRules.AppearanceId, 0, 4242),
            new ItemProperty(ItemComposingRules.OptionsType, ItemComposingRules.OptionsId, 0, 7),
        };
        var look = PetMergeRules.LookOverrides(properties).ToList();
        Assert.Equal(2, look.Count);
        Assert.Equal(4242u, look[0].Extra);
        Assert.Equal(7u, look[1].Extra);
        Assert.Empty(PetMergeRules.LookOverrides(Array.Empty<ItemProperty>()));
    }
}
