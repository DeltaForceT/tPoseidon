using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Pets;

public sealed class PetRetrainRulesTests
{
    private static readonly PetRetrainMaterialEntry Protein = new(19690, 1, 1, -25, 35);

    private static PetRetrainTables Tables() => new(
        new Dictionary<int, PetRetrainMaterialEntry> { [19690] = Protein },
        new Dictionary<int, int> { [19691] = 1 },
        [1, 9, 13, 15, 16, 15, 13, 10, 5, 2, 1]);

    private static readonly ItemParamEntry Pet = new()
    {
        Id = 72429,
        Class = 2,
        Type = 13,
        StatBonuses = [40, 0, 0, 0, 20, 0, 0, 0, 0, 0, 0, 0],
    };

    private static readonly ItemParamEntry Sword = new() { Id = 87003, Class = 2, Type = 2 };

    [Fact]
    public void ChecksFollowTheClientTexts()
    {
        var tables = Tables();
        var protein = new HeldItem(19690, 1);
        var lockItem = new HeldItem(19691, 1);
        Assert.Equal(PetRetrainRules.NoPet, PetRetrainRules.Check(null, protein, false, null, 0, tables));
        Assert.Equal(PetRetrainRules.NotAPet, PetRetrainRules.Check(Sword, protein, false, null, 0, tables));
        Assert.Equal(PetRetrainRules.NoMaterial, PetRetrainRules.Check(Pet, null, false, null, 0, tables));
        Assert.Equal(PetRetrainRules.StoreFailed, PetRetrainRules.Check(Pet, new HeldItem(19000, 1), false, null, 0, tables));
        Assert.Equal(PetRetrainRules.NotEnoughMaterial, PetRetrainRules.Check(Pet, new HeldItem(19690, 0), false, null, 0, tables));
        Assert.Equal(PetRetrainRules.LockItemMissing, PetRetrainRules.Check(Pet, protein, false, null, 10000, tables));
        Assert.Equal(PetRetrainRules.LockStatMissing, PetRetrainRules.Check(Pet, protein, true, lockItem, 0, tables));
        Assert.Equal(PetRetrainRules.NotALockItem, PetRetrainRules.Check(Pet, protein, true, new HeldItem(19690, 1), 10000, tables));
        Assert.Equal(PetRetrainRules.StatNotLockable, PetRetrainRules.Check(Pet, protein, true, lockItem, 10001, tables));
        Assert.Equal(PetRetrainRules.Ok, PetRetrainRules.Check(Pet, protein, true, lockItem, 10004, tables));
        Assert.Equal(PetRetrainRules.Ok, PetRetrainRules.Check(Pet, protein, false, null, 0, tables));
    }

    [Fact]
    public void RetrainRollsTheBaseStatsAndKeepsTheLockedOne()
    {
        ItemProperty[] current = [new(10, 10000, 44, 0), new(10, 10004, 25, 0)];
        var outcome = PetRetrainRules.Retrain(Pet, [], current, Protein, 10004, Tables(), () => 9999);
        Assert.Equal([new ItemProperty(10, 10000, 54, 0), new ItemProperty(10, 10004, 25, 0)], outcome.Rolled);
        var low = PetRetrainRules.Retrain(Pet, [new(12, 26000, 0, 1)], current, Protein, 0, Tables(), () => 0);
        Assert.Equal([new ItemProperty(12, 26000, 0, 1), new ItemProperty(10, 10000, 30, 0), new ItemProperty(10, 10004, 15, 0)], low.Stored);
    }
}
