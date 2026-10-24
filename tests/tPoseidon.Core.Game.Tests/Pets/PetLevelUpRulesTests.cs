using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Pets;

public sealed class PetLevelUpRulesTests
{
    private static readonly PetLevelUpTables Tables = new(new Dictionary<int, PetLevelUpEntry>
    {
        [70000] = new(70000, 70022, 19719, 2, false),
    });

    private static readonly ItemParamEntry Puppy = new() { Id = 70000, Class = 2, Type = 13 };

    [Fact]
    public void ChecksRunInTheServerOrder()
    {
        Assert.Equal(PetLevelUpRules.PetMissing, PetLevelUpRules.Check(null, new HeldItem(19719, 2), Tables, out _));
        Assert.Equal(PetLevelUpRules.CatalystMissing, PetLevelUpRules.Check(Puppy, null, Tables, out _));
        Assert.Equal(PetLevelUpRules.CannotLevelUp, PetLevelUpRules.Check(Puppy with { CompoundSlot = 1 }, new HeldItem(19719, 2), Tables, out _));
        Assert.Equal(PetLevelUpRules.CannotLevelUp, PetLevelUpRules.Check(Puppy with { Id = 70099 }, new HeldItem(19719, 2), Tables, out _));
        Assert.Equal(PetLevelUpRules.CannotLevelUp, PetLevelUpRules.Check(Puppy with { Type = 2 }, new HeldItem(19719, 2), Tables, out _));
        Assert.Equal(PetLevelUpRules.WrongCatalyst, PetLevelUpRules.Check(Puppy, new HeldItem(19720, 2), Tables, out _));
        Assert.Equal(PetLevelUpRules.NotEnoughCatalyst, PetLevelUpRules.Check(Puppy, new HeldItem(19719, 1), Tables, out _));
        Assert.Equal(PetLevelUpRules.Ok, PetLevelUpRules.Check(Puppy, new HeldItem(19719, 2), Tables, out var row));
        Assert.Equal(70022, row!.ResultId);
    }
}
