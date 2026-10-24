namespace tPoseidon.Core.Data.Tests;

public sealed class CharacterEditTests
{
    private static InMemoryCharStore StoreWith(params string[] names)
    {
        var store = new InMemoryCharStore();
        for (var index = 0; index < names.Length; index++)
        {
            store.AddCharacter(new Character
            {
                CharacterId = (ulong)(index + 1),
                OwnerAccountId = (uint)(index + 1),
                Slot = 0,
                Name = names[index],
                Species = Species.Lion,
                FType = FType.Power,
                Hair = 4,
                Hair2 = 5,
                Hair3 = 6,
            });
        }
        return store;
    }

    [Fact]
    public void ARenameChangesOnlyTheName()
    {
        var store = StoreWith("Oldname", "Other");

        Assert.Equal(CharacterEditCodes.Done, store.EditCharacter(1, CharacterEdit.Rename("Newname", 19711)));

        Assert.Equal("Newname", store.GetCharacterById(1)!.Name);
        Assert.Equal(Species.Lion, store.GetCharacterById(1)!.Species);
    }

    [Fact]
    public void ARenameToATakenNameIsRefusedWhateverTheCase()
    {
        var store = StoreWith("Oldname", "Other");

        Assert.Equal(CharacterEditCodes.NameTaken, store.EditCharacter(1, CharacterEdit.Rename("OTHER", 1)));
        Assert.Equal("Oldname", store.GetCharacterById(1)!.Name);
    }

    [Fact]
    public void ARenameToAForbiddenOrMalformedNameIsRefused()
    {
        var store = StoreWith("Oldname");
        store.ForbiddenNames = ["badword"];

        Assert.Equal(CharacterEditCodes.InvalidName, store.EditCharacter(1, CharacterEdit.Rename("xBadWordx", 1)));
        Assert.Equal(CharacterEditCodes.InvalidName, store.EditCharacter(1, CharacterEdit.Rename("with space", 1)));
        Assert.Equal(CharacterEditCodes.InvalidName, store.EditCharacter(1, CharacterEdit.Rename("fullＡwidth", 1)));
        Assert.Equal("Oldname", store.GetCharacterById(1)!.Name);
    }

    [Fact]
    public void AnEditOfAnUnknownCharacterFails() =>
        Assert.Equal(CharacterEditCodes.Failed, StoreWith("Oldname").EditCharacter(9, CharacterEdit.Storage(88)));

    [Theory]
    [InlineData(1, 9, 5, 6)]
    [InlineData(2, 4, 9, 6)]
    [InlineData(3, 4, 5, 9)]
    public void TheHairEditTouchesOnlyTheNamedSlot(byte slot, ushort hair, ushort hair2, ushort hair3)
    {
        var store = StoreWith("Oldname");

        store.EditCharacter(1, CharacterEdit.Hair(slot, 9));

        var edited = store.GetCharacterById(1)!;
        Assert.Equal((hair, hair2, hair3), (edited.Hair, edited.Hair2, edited.Hair3));
    }

    [Fact]
    public void TheGenderEditSetsTheSpeciesClearsTheHairAndSwapsTheStarterEarAndTail()
    {
        var store = StoreWith("Oldname");

        store.EditCharacter(1, CharacterEdit.Gender(Species.Fox));

        var edited = store.GetCharacterById(1)!;
        Assert.Equal(Species.Fox, edited.Species);
        Assert.Equal(0, edited.Hair);
        Assert.Equal(5, edited.Hair2);
        Assert.Equal(SpeciesDefaults.EarItemIndexForSpecies(Species.Fox), edited.StarterEarItemIndex);
        Assert.Equal(SpeciesDefaults.TailItemIndexForSpecies(Species.Fox), edited.StarterTailItemIndex);
    }

    [Fact]
    public void TheGrowthAndStorageEditsStoreTheirValues()
    {
        var store = StoreWith("Oldname");

        store.EditCharacter(1, CharacterEdit.GrowthWeights(4, 3, 2, 1));
        store.EditCharacter(1, CharacterEdit.Storage(88));

        var edited = store.GetCharacterById(1)!;
        Assert.Equal((4, 3, 2, 1), (edited.PtsP, edited.PtsM, edited.PtsS, edited.PtsC));
        Assert.Equal(88, edited.WarehouseSlots);
    }

    [Theory]
    [InlineData("abc_DEF^1", true)]
    [InlineData("abc def", false)]
    [InlineData("abc-def", false)]
    [InlineData("abc！", false)]
    [InlineData("가각", true)]
    public void TheStoreAcceptsLettersDigitsUnderscoreAndCaretOnly(string name, bool valid) =>
        Assert.Equal(valid, CharacterNameValidity.IsValid(name, []));

    [Fact]
    public void AnImprintIsKeptPerTypeAndReplacedWhenTheTypeIsDrunkAgain()
    {
        var store = new InMemoryImprintStore();
        var first = new ImprintRecord(1, 0, 37109, DateTimeOffset.UtcNow.AddHours(24));
        var second = first with { ItemId = 37134, ExpiresAt = first.ExpiresAt.AddHours(1) };

        store.Save(first);
        store.Save(new ImprintRecord(1, 10, 37119, first.ExpiresAt));
        store.Save(second);

        Assert.Equal(new[] { second, new ImprintRecord(1, 10, 37119, first.ExpiresAt) }, store.Load(1));
        Assert.Empty(store.Load(2));
    }

    [Fact]
    public void ASkillIsDeletedByItsId()
    {
        var store = new InMemorySkillStore();
        store.Upsert(new Skill { CharacterId = 1, SkillId = 1104, SkillLevel = 5 });

        Assert.True(store.Delete(1, 1104));
        Assert.False(store.Delete(1, 1104));
        Assert.Null(store.Find(1, 1104));
    }
}
