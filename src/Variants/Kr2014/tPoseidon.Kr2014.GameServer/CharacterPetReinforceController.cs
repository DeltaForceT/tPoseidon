using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class CharacterPetReinforceController
{
    private readonly MasterEquipStatusUpgrade _master;
    private readonly Func<CharacterPetReinforceTables> _tables;
    private readonly Func<ItemPropertyRules> _propertyRules;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public CharacterPetReinforceController(
        MasterEquipStatusUpgrade master,
        Func<CharacterPetReinforceTables> tables,
        Func<ItemPropertyRules> propertyRules,
        IItemStore items,
        IItemParamLookup catalog,
        IItemPropertyStore properties,
        int bagCapacity,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _propertyRules = propertyRules ?? throw new ArgumentNullException(nameof(propertyRules));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task ReinforceAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade != UpgradeWindow.PetLevelUp || player.Character is not { } character) return;
        if (!CharacterPetReinforceFrames.TryReadRequest(payload.Span, out var request))
        {
            Log(player, $"request of {payload.Length} bytes not read");
            return;
        }
        if (player.UpgradeBusy)
        {
            await RefuseAsync(player, CharacterPetReinforceRules.Failed, "a request is pending").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        Item? InBag(ulong wireUid) => wireUid == 0 ? null : bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(wireUid));
        var reinforce = InBag(request.ReinforceItemWireUid);
        var pet = InBag(request.PetWireUid);
        var equipped = pet is null && request.PetWireUid != 0
            ? _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Equipment).FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(request.PetWireUid))
            : null;
        pet ??= equipped;
        if (pet is not null && pet.ItemUid == reinforce?.ItemUid) pet = null;
        var petDefinition = pet is null ? null : _catalog.GetById(pet.ItemIndex);
        var stored = pet is null ? Array.Empty<ItemProperty>() : _properties.ForItem(pet.ItemUid);
        var petProperties = petDefinition is null ? stored : _propertyRules().Full(petDefinition, stored);
        var code = CharacterPetReinforceRules.Check(
            reinforce is null ? null : _catalog.GetById(reinforce.ItemIndex),
            reinforce?.Stack ?? 0,
            petDefinition,
            equipped is not null,
            petProperties,
            _tables(),
            out var row);
        if (code != CharacterPetReinforceRules.Ok || reinforce is null || pet is null || row is null)
        {
            await RefuseAsync(player, code, $"item 0x{request.ReinforceItemWireUid:X} pet 0x{request.PetWireUid:X}").ConfigureAwait(false);
            return;
        }
        if (_catalog.GetById(row.ResultId) is not { } result)
        {
            await RefuseAsync(player, CharacterPetReinforceRules.Failed, $"result pet {row.ResultId} has no item row").ConfigureAwait(false);
            return;
        }
        var rows = new List<EquipUpgradeItem>
        {
            EquipUpgradeOperations.Insert(result.Id, result.ObjectClass, 1, pet.ItemSlot),
            EquipUpgradeOperations.Take(reinforce, 1),
            EquipUpgradeOperations.Delete(pet),
        };
        player.UpgradeBusy = true;
        EquipStatusUpgradeResult applied;
        try
        {
            applied = await _master.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, 0, _bagCapacity, rows), default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (!applied.Succeeded || applied.Items.FirstOrDefault(item => item.Action == EquipUpgradeAction.Insert) is not { } created)
        {
            await RefuseAsync(player, CharacterPetReinforceRules.Failed, $"store refused with code {applied.Error}").ConfigureAwait(false);
            return;
        }
        _properties.Replace(created.ItemUid, CharacterPetReinforceRules.ResultProperties(row, petProperties, result));
        _properties.Delete(pet.ItemUid);
        foreach (var removed in applied.Items.Where(item => item.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(removed.ItemUid);
        await SendAsync(player, Opcode.S2C_CHARACTER_PET_REINFORCE_RESULT,
            CharacterPetReinforceFrames.Result(applied.Items.Select(EquipUpgradeOperations.ToBag).ToList())).ConfigureAwait(false);
        Log(player, $"character pet {pet.ItemIndex} -> {row.ResultId} with item {reinforce.ItemIndex}");
    }

    private async Task RefuseAsync(InGameSessionState player, ushort code, string reason)
    {
        Log(player, $"refused with code {code}: {reason}");
        await SendAsync(player, Opcode.S2C_CHARACTER_PET_REINFORCE_REFUSED, CharacterPetReinforceFrames.Refused(code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [character-pet-reinforce] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
