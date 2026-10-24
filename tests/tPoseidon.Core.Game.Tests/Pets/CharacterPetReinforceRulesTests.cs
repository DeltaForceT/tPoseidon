using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Pets;

public sealed class CharacterPetReinforceRulesTests
{
    private static readonly int[] ApStats = [50, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly CharacterPetReinforceEntry ApRow = new(71916, 19832, false, 71915, ApStats);
    private static readonly CharacterPetReinforceTables Tables = new(new Dictionary<(int, int), CharacterPetReinforceEntry>
    {
        [(19832, 71915)] = ApRow,
    });

    private static readonly ItemParamEntry Reinforce = new() { Id = 19832, Class = 1, Type = 15, Effect = 48 };
    private static readonly ItemParamEntry Pet = new() { Id = 71915, Class = 2, Type = 13, SubType = 78 };

    [Fact]
    public void ChecksRunInTheServerOrder()
    {
        var none = Array.Empty<ItemProperty>();
        Assert.Equal(CharacterPetReinforceRules.ReinforceItemMissing, CharacterPetReinforceRules.Check(null, 1, Pet, false, none, Tables, out _));
        Assert.Equal(CharacterPetReinforceRules.ReinforceItemMissing, CharacterPetReinforceRules.Check(Reinforce, 0, Pet, false, none, Tables, out _));
        Assert.Equal(CharacterPetReinforceRules.NotReinforceItem, CharacterPetReinforceRules.Check(Reinforce with { Effect = 0 }, 1, Pet, false, none, Tables, out _));
        Assert.Equal(CharacterPetReinforceRules.PetMissing, CharacterPetReinforceRules.Check(Reinforce, 1, null, false, none, Tables, out _));
        Assert.Equal(CharacterPetReinforceRules.NotCharacterPet, CharacterPetReinforceRules.Check(Reinforce, 1, Pet with { SubType = 0 }, false, none, Tables, out _));
        Assert.Equal(CharacterPetReinforceRules.PetEquipped, CharacterPetReinforceRules.Check(Reinforce, 1, Pet, true, none, Tables, out _));
        Assert.Equal(CharacterPetReinforceRules.Failed, CharacterPetReinforceRules.Check(Reinforce with { Id = 19833 }, 1, Pet, false, none, Tables, out _));
        Assert.Equal(CharacterPetReinforceRules.Ok, CharacterPetReinforceRules.Check(Reinforce, 1, Pet, false, none, Tables, out var row));
        Assert.Equal(71916, row!.ResultId);
    }

    [Fact]
    public void AReinforcedPetIsLookedUpByItsBasicPet()
    {
        var marked = new[] { new ItemProperty(CharacterPetReinforceRules.MarkType, CharacterPetReinforceRules.MarkId, 0, 71915) };
        Assert.Equal(CharacterPetReinforceRules.Ok, CharacterPetReinforceRules.Check(Reinforce, 1, Pet with { Id = 71917 }, false, marked, Tables, out _));
    }

    [Fact]
    public void ResultKeepsOtherPropertiesAndAddsStatsRecoveryAndMark()
    {
        var pet = new[]
        {
            new ItemProperty(ItemPropertyTypes.Stat, 10000, 7, 0),
            new ItemProperty(ItemPropertyTypes.Stat, ItemPropertyRules.HpRecoveryId, 3, 0),
            new ItemProperty(CharacterPetReinforceRules.StatType, 10001, 10, 0),
            new ItemProperty(CharacterPetReinforceRules.MarkType, CharacterPetReinforceRules.MarkId, 0, 71915),
            new ItemProperty(ItemComposingRules.AppearanceType, ItemComposingRules.AppearanceId, 0, 5),
        };
        var result = new ItemParamEntry { Id = 71916, HpRecoveryRate = 20.5f, MpRecoveryRate = 3f };

        var plain = CharacterPetReinforceRules.ResultProperties(ApRow, pet, result);
        Assert.Equal(new[]
        {
            new ItemProperty(ItemPropertyTypes.Stat, 10000, 7, 0),
            new ItemProperty(ItemComposingRules.AppearanceType, ItemComposingRules.AppearanceId, 0, 5),
            new ItemProperty(ItemPropertyTypes.Stat, ItemPropertyRules.HpRecoveryId, 20, 0),
            new ItemProperty(ItemPropertyTypes.Stat, ItemPropertyRules.MpRecoveryId, 3, 0),
            new ItemProperty(CharacterPetReinforceRules.StatType, 10000, 50, 0),
            new ItemProperty(CharacterPetReinforceRules.MarkType, CharacterPetReinforceRules.MarkId, 0, 71915),
        }, plain);

        var rare = CharacterPetReinforceRules.ResultProperties(ApRow with { RareType = true }, pet, result);
        Assert.DoesNotContain(rare, property => property.Type == ItemComposingRules.AppearanceType);
    }

    [Fact]
    public void GunStatHasNoId()
    {
        Assert.Equal(10011, CharacterPetReinforceRules.StatId(11));
        Assert.Equal(0, CharacterPetReinforceRules.StatId(12));
    }
}
