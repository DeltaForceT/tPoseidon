using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Characters;

namespace tPoseidon.Core.GameTests.Characters;

public class CharacterCreationManagerTests
{
    private const ulong CharId = 0x10000001;
    private const uint AccountId = 12345;
    private static readonly int[] PolarBearInventory = { 2006, 2206, 92018 };
    private static readonly int[] PolarBearSkills = { 4701, 4709 };
    private static readonly ushort[] LionSenseSkills = { 3015 };

    private static InMemoryAccountStore SeedAccount()
    {
        var store = new InMemoryAccountStore();
        store.UpsertAccount(new Account
        {
            AccountId = AccountId,
            Username = "tester",
            PasswordPlaceholder = "x",
            CharacterIds = Array.Empty<ulong>(),
        });
        return store;
    }

    private static CharacterCreationRequest MakeRequest(byte slot = 0) => new()
    {
        CharacterId = CharId + slot,
        OwnerAccountId = AccountId,
        Slot = slot,
        Name = "Tester",
        Species = Species.Lion,
        FType = FType.Sense,
        Hair = 1,
    };

    [Fact]
    public void CreateCharacter_PersistsCharacterRow()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore);

        var outcome = mgr.CreateCharacter(MakeRequest());

        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.Snapshot);
        Assert.Equal(CharId, outcome.Snapshot!.Character.CharacterId);
        Assert.NotNull(accountStore.GetCharacterById(CharId));
    }

    [Fact]
    public void CreateCharacter_GrantsSpeciesCorrectEarAndTail()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore);

        var outcome = mgr.CreateCharacter(MakeRequest());

        Assert.True(outcome.IsSuccess);
        var equipped = outcome.Snapshot!.EquippedItems;
        Assert.Equal(2, equipped.Count);
        var ear = equipped.First(i => i.SlotPosition == 5);
        var tail = equipped.First(i => i.SlotPosition == 6);
        Assert.Equal(6905, ear.ItemIndex);
        Assert.Equal(6955, tail.ItemIndex);
    }

    [Fact]
    public void CreateCharacter_GrantsThirtyHpAndMpPotions()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore);

        var outcome = mgr.CreateCharacter(MakeRequest());

        var inventory = outcome.Snapshot!.InventoryItems;
        Assert.Equal(2, inventory.Count);
        var hp = inventory.First(i => i.ItemIndex == 2006);
        var mp = inventory.First(i => i.ItemIndex == 2206);
        Assert.Equal(30, hp.Stack);
        Assert.Equal(30, mp.Stack);
    }

    [Fact]
    public void CreateCharacter_StarterSpawnIsCoralTown()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore);

        var outcome = mgr.CreateCharacter(MakeRequest());

        var character = outcome.Snapshot!.Character;
        Assert.Equal(SpeciesDefaults.CoralTownMapId, character.MapId);
        Assert.Equal(SpeciesDefaults.CoralTownSpawnX, character.PositionX);
        Assert.Equal(SpeciesDefaults.CoralTownSpawnY, character.PositionY);
    }

    [Fact]
    public void CreateCharacter_StarterSkillIsPersisted()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var skillStore = new InMemorySkillStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore, skillStore);

        var outcome = mgr.CreateCharacter(MakeRequest() with { FType = FType.Sense });

        Assert.True(outcome.IsSuccess);
        var skills = skillStore.GetForCharacter(outcome.Snapshot!.Character.CharacterId);
        Assert.Contains(skills, s => s.SkillId == SpeciesDefaults.StarterSkillIdForFType(FType.Sense));
    }

    [Fact]
    public void CreateCharacter_FreshCharStartsAtLevel1JobTier1()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore);

        var outcome = mgr.CreateCharacter(MakeRequest());

        var character = outcome.Snapshot!.Character;
        Assert.Equal(1, character.Level);
        Assert.Equal(1, character.JobTier);
        Assert.Equal(0u, character.Money);
    }

    [Fact]
    public void CreateCharacter_AddsAllGrantedItemsToInventory()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore);

        mgr.CreateCharacter(MakeRequest());

        var equipped = itemStore.GetForCharacterBySlotKind(CharId, 0);
        var inventory = itemStore.GetForCharacterBySlotKind(CharId, 1);
        Assert.Equal(2, equipped.Count);
        Assert.Equal(2, inventory.Count);
    }

    [Fact]
    public void CreateCharacter_RejectsFourthCharacter()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore);

        mgr.CreateCharacter(MakeRequest(slot: 0));
        mgr.CreateCharacter(MakeRequest(slot: 1) with { Name = "T2" });
        mgr.CreateCharacter(MakeRequest(slot: 2) with { Name = "T3" });
        var fourth = mgr.CreateCharacter(MakeRequest(slot: 3) with { Name = "T4" });

        Assert.False(fourth.IsSuccess);
        Assert.Equal(CharCreationError.OverflowSlotCount, fourth.Error);
    }

    [Fact]
    public void CreateCharacter_RejectsDuplicateSlot()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore);

        mgr.CreateCharacter(MakeRequest(slot: 0));
        var dupe = mgr.CreateCharacter(MakeRequest(slot: 0) with { Name = "Dupe" });

        Assert.False(dupe.IsSuccess);
        Assert.Equal(CharCreationError.SlotOccupied, dupe.Error);
    }

    [Fact]
    public void CreateCharacter_RejectsInvalidName()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore);

        var outcome = mgr.CreateCharacter(MakeRequest() with { Name = "Bad+Name" });
        Assert.False(outcome.IsSuccess);
        Assert.Equal(CharCreationError.NameInvalid, outcome.Error);

        var tooLong = mgr.CreateCharacter(MakeRequest() with { Name = new string('A', 17) });
        Assert.False(tooLong.IsSuccess);
        Assert.Equal(CharCreationError.NameInvalid, tooLong.Error);
    }

    [Fact]
    public void APolarBearStartsWithBearEarsHerHammersAndHerTwoHammerSkills()
    {
        var accountStore = SeedAccount();
        var itemStore = new InMemoryItemStore();
        var skillStore = new InMemorySkillStore();
        var mgr = new CharacterCreationManager(accountStore, itemStore, skillStore);

        var outcome = mgr.CreateCharacter(MakeRequest() with { Species = Species.PolarBear, FType = FType.Power });

        Assert.True(outcome.IsSuccess);
        var equipped = outcome.Snapshot!.EquippedItems;
        Assert.Equal(6932, equipped.First(i => i.SlotPosition == 5).ItemIndex);
        Assert.Equal(6982, equipped.First(i => i.SlotPosition == 6).ItemIndex);
        Assert.Equal(
            PolarBearInventory,
            outcome.Snapshot.InventoryItems.Select(item => item.ItemIndex).ToArray());
        Assert.Equal(
            PolarBearSkills,
            skillStore.GetForCharacter(CharId).Select(skill => skill.SkillId).OrderBy(id => id).ToArray());
    }

    [Fact]
    public void OtherSpeciesKeepTheirJobGroupStarterSkill()
    {
        Assert.Equal(LionSenseSkills, SpeciesDefaults.StarterSkillIds(Species.Lion, FType.Sense));
        Assert.Empty(SpeciesDefaults.StarterItemIndexes(Species.Lion));
        Assert.Equal(6905, SpeciesDefaults.EarItemIndexForSpecies(Species.Lion));
    }
}
