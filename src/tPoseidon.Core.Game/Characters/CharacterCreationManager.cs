using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Characters;

public sealed class CharacterCreationManager
{
    private const int MaxCharsPerAccount = 3;

    private readonly IAccountStore _accountStore;
    private readonly IItemStore _itemStore;
    private readonly ISkillStore? _skillStore;
    private readonly IMyCampStore? _myCampStore;
    private readonly ICoupleStore? _coupleStore;

    public CharacterCreationManager(
        IAccountStore accountStore,
        IItemStore itemStore,
        ISkillStore? skillStore = null,
        IMyCampStore? myCampStore = null,
        ICoupleStore? coupleStore = null)
    {
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(itemStore);
        _accountStore = accountStore;
        _itemStore = itemStore;
        _skillStore = skillStore;
        _myCampStore = myCampStore;
        _coupleStore = coupleStore;
    }

    public CharCreationOutcome CreateCharacter(CharacterCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsValidCharacterName(request.Name))
        {
            return CharCreationOutcome.Fail(CharCreationError.NameInvalid);
        }

        var existingChars = _accountStore.GetCharactersForAccount(request.OwnerAccountId);
        if (existingChars.Count >= MaxCharsPerAccount)
        {
            return CharCreationOutcome.Fail(CharCreationError.OverflowSlotCount);
        }
        if (existingChars.Any(c => c.Slot == request.Slot))
        {
            return CharCreationOutcome.Fail(CharCreationError.SlotOccupied);
        }

        var character = BuildCharacterRow(request);
        try
        {
            _accountStore.AddCharacter(character);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            return CharCreationOutcome.Fail(CharCreationError.NameTaken);
        }
        catch (Exception)
        {
            return CharCreationOutcome.Fail(CharCreationError.CharAttrInsertFailed);
        }

        const byte cosmeticItemClass = 2;
        const byte equippedSlotKind = 0;
        const byte consumableItemClass = 1;
        const byte inventorySlotKind = 1;

        var ear = _itemStore.GrantToCharacter(
            character.CharacterId,
            itemIndex: SpeciesDefaults.EarItemIndexForSpecies(character.Species),
            itemClass: cosmeticItemClass,
            slotKind: equippedSlotKind,
            slotPosition: 5,
            stack: 1);

        var tail = _itemStore.GrantToCharacter(
            character.CharacterId,
            itemIndex: SpeciesDefaults.TailItemIndexForSpecies(character.Species),
            itemClass: cosmeticItemClass,
            slotKind: equippedSlotKind,
            slotPosition: 6,
            stack: 1);

        var hpPotion = _itemStore.GrantToCharacter(
            character.CharacterId,
            itemIndex: 2006,
            itemClass: consumableItemClass,
            slotKind: inventorySlotKind,
            slotPosition: 0,
            stack: SpeciesDefaults.StarterQuickslotStack);

        var mpPotion = _itemStore.GrantToCharacter(
            character.CharacterId,
            itemIndex: 2206,
            itemClass: consumableItemClass,
            slotKind: inventorySlotKind,
            slotPosition: 1,
            stack: SpeciesDefaults.StarterQuickslotStack);

        var starterItems = new List<Item> { hpPotion, mpPotion };
        foreach (var starterItemIndex in SpeciesDefaults.StarterItemIndexes(character.Species))
        {
            starterItems.Add(_itemStore.GrantToCharacter(
                character.CharacterId,
                itemIndex: starterItemIndex,
                itemClass: cosmeticItemClass,
                slotKind: inventorySlotKind,
                slotPosition: (byte)starterItems.Count,
                stack: 1));
        }

        foreach (var starterSkillId in SpeciesDefaults.StarterSkillIds(character.Species, character.FType))
        {
            _skillStore?.Upsert(new Skill
            {
                CharacterId = character.CharacterId,
                SkillId = starterSkillId,
                SkillLevel = 1,
            });
        }

        if (_myCampStore is not null)
        {
            var myCampSlot = _myCampStore.CreateSlotForCharacter(character.CharacterId, "mycamp 1");

            var cell = _myCampStore.AddItemForAccount(request.OwnerAccountId, itemIndex: 18001);
            var wall1 = _myCampStore.AddItemForAccount(request.OwnerAccountId, itemIndex: 18002);
            var wall2 = _myCampStore.AddItemForAccount(request.OwnerAccountId, itemIndex: 18002);

            _myCampStore.InsertDeco(myCampSlot.SlotCode, cell.ItemUid, 2400, 2400, 0, 0, parentsUid: cell.ItemUid);
            _myCampStore.InsertDeco(myCampSlot.SlotCode, wall1.ItemUid, 0, -120, 0, 0, parentsUid: cell.ItemUid);
            _myCampStore.InsertDeco(myCampSlot.SlotCode, wall2.ItemUid, 0, -120, 0, 1, parentsUid: cell.ItemUid);

            if (!_myCampStore.IsInitializedForAccount(request.OwnerAccountId))
            {
                _myCampStore.MarkInitializedForAccount(request.OwnerAccountId);
            }
        }

        var snapshot = new CharacterSnapshot
        {
            Character = character,
            EquippedItems = new[] { ear, tail },
            InventoryItems = starterItems,
            Skills = _skillStore?.GetForCharacter(character.CharacterId) ?? Array.Empty<Skill>(),
            ActiveQuests = Array.Empty<Quest>(),
            UniqueDutyInfo = null,
            Awakening = null,
            CoupleCode = 0,
            PkInfo = null,
            ActiveBlock = null,
        };

        return CharCreationOutcome.Success(snapshot);
    }

    private static bool IsValidCharacterName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name.Length is < 1 or > 16) return false;
        if (name[0] == ' ' || name[^1] == ' ') return false;
        foreach (var c in name)
        {
            if (c < 0x20 || c > 0x7E) return false;
            if (c is '+' or '<' or '>' or '&' or '"' or '\'' or '\\' or '/') return false;
        }
        return true;
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("23505", StringComparison.Ordinal)
            || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }

    private static Character BuildCharacterRow(CharacterCreationRequest request)
    {
        var (ptsP, ptsM, ptsS, ptsC) = request.GrowthGraph ?? SpeciesDefaults.GrowthGraphForFType(request.FType);
        var now = DateTimeOffset.UtcNow;
        var draft = new Character
        {
            CharacterId = request.CharacterId,
            OwnerAccountId = request.OwnerAccountId,
            Slot = request.Slot,
            Name = request.Name,
            Species = request.Species,
            FType = request.FType,
            JobTier = 0x01,
            Hair = request.Hair,
            Money = 0,
            Level = 1,
            StarterEarItemIndex = SpeciesDefaults.EarItemIndexForSpecies(request.Species),
            StarterTailItemIndex = SpeciesDefaults.TailItemIndexForSpecies(request.Species),
            PositionX = SpeciesDefaults.CoralTownSpawnX,
            PositionY = SpeciesDefaults.CoralTownSpawnY,
            MapId = SpeciesDefaults.CoralTownMapId,
            Home = SpeciesDefaults.CoralTownMapId,
            PtsP = ptsP,
            PtsM = ptsM,
            PtsS = ptsS,
            PtsC = ptsC,
            GenTime = now,
            LoginTime = now,
            LogoutTime = now,
        };
        var stats = EffectiveStatsCalculator.Compute(draft);
        return new Character
        {
            CharacterId = draft.CharacterId,
            OwnerAccountId = draft.OwnerAccountId,
            Slot = draft.Slot,
            Name = draft.Name,
            Species = draft.Species,
            FType = draft.FType,
            JobTier = draft.JobTier,
            Hair = draft.Hair,
            Money = draft.Money,
            Level = draft.Level,
            StarterEarItemIndex = draft.StarterEarItemIndex,
            StarterTailItemIndex = draft.StarterTailItemIndex,
            PositionX = draft.PositionX,
            PositionY = draft.PositionY,
            MapId = draft.MapId,
            Home = draft.Home,
            PtsP = draft.PtsP,
            PtsM = draft.PtsM,
            PtsS = draft.PtsS,
            PtsC = draft.PtsC,
            GenTime = draft.GenTime,
            LoginTime = draft.LoginTime,
            LogoutTime = draft.LogoutTime,
            CurrentHp = (uint)Math.Max(1, stats.Hp),
            CurrentMp = (uint)Math.Max(0, stats.Mp),
        };
    }
}

public sealed record CharacterCreationRequest
{
    public required ulong CharacterId { get; init; }
    public required uint OwnerAccountId { get; init; }
    public required byte Slot { get; init; }
    public required string Name { get; init; }
    public required Species Species { get; init; }
    public required FType FType { get; init; }
    public ushort Hair { get; init; }
    public (byte PtsP, byte PtsM, byte PtsS, byte PtsC)? GrowthGraph { get; init; }
}

public sealed record CharacterCreationResult
{
    public required Character Character { get; init; }
    public required Item EarItem { get; init; }
    public required Item TailItem { get; init; }
    public required Item HpPotion { get; init; }
    public required Item MpPotion { get; init; }
    public required ushort StarterSkillId { get; init; }
    public MyCampSlot? MyCampSlot { get; init; }
}
