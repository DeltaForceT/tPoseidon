using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class PetEvolveMergeController
{
    private readonly MasterEquipStatusUpgrade _master;
    private readonly Func<PetLevelUpTables> _tables;
    private readonly Func<ItemPropertyRules> _propertyRules;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public PetEvolveMergeController(
        MasterEquipStatusUpgrade master,
        Func<PetLevelUpTables> tables,
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

    public async Task EvolveAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade != UpgradeWindow.PetLevelUp || player.Character is not { } character) return;
        if (!PetLevelUpFrames.TryReadRequest(payload.Span, out var request))
        {
            Log(player, $"evolve request of {payload.Length} bytes not read");
            return;
        }
        if (player.UpgradeBusy)
        {
            await RefuseAsync(player, Opcode.S2C_PET_EVOLVE_REFUSED, PetEvolveRules.Failed, "a request is pending").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var (pet, petEquipped) = Find(character.CharacterId, bag, request.PetWireUid);
        var material = request.CatalystWireUid == 0 ? null : bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(request.CatalystWireUid));
        if (material is not null && material.ItemUid == pet?.ItemUid) material = null;
        var petDefinition = pet is null ? null : _catalog.GetById(pet.ItemIndex);
        var code = PetEvolveRules.Check(petDefinition, petEquipped, material is null ? null : new HeldItem(material.ItemIndex, material.Stack), _tables(), out var row);
        if (code != PetEvolveRules.Ok || pet is null || petDefinition is null || material is null || row is null)
        {
            await RefuseAsync(player, Opcode.S2C_PET_EVOLVE_REFUSED, code, $"pet 0x{request.PetWireUid:X} material 0x{request.CatalystWireUid:X}").ConfigureAwait(false);
            return;
        }
        if (_catalog.GetById(row.ResultId) is not { } result || !Fits(player, bag, result))
        {
            await RefuseAsync(player, Opcode.S2C_PET_EVOLVE_REFUSED, PetEvolveRules.Failed, $"result pet {row.ResultId} missing or no room").ConfigureAwait(false);
            return;
        }
        var rules = _propertyRules();
        var previous = rules.Full(petDefinition, _properties.ForItem(pet.ItemUid));
        List<ItemProperty> evolved;
        int slots;
        lock (_rngGate)
        {
            evolved = rules.Evolved(result, previous, _rng);
            slots = rules.MixSlots(result.Id, pet.ItemSlot, _rng);
        }
        evolved.AddRange(PetMergeRules.LookOverrides(previous));
        var rows = new List<EquipUpgradeItem>
        {
            EquipUpgradeOperations.Insert(result.Id, result.ObjectClass, 1, (byte)Math.Clamp(slots, 0, byte.MaxValue)),
            EquipUpgradeOperations.Take(material, (ushort)row.CatalystCount),
            EquipUpgradeOperations.Delete(pet),
        };
        if (await ApplyAsync(player, character.CharacterId, rows, Opcode.S2C_PET_EVOLVE_REFUSED, PetEvolveRules.Failed).ConfigureAwait(false) is not { } applied) return;
        Store(player, applied, evolved, pet.ItemUid);
        await SendAsync(player, Opcode.S2C_PET_EVOLVE_RESULT, PetLevelUpFrames.Result(applied.Items.Select(EquipUpgradeOperations.ToBag).ToList())).ConfigureAwait(false);
        Log(player, $"pet {pet.ItemIndex} evolved into {row.ResultId} with {row.CatalystCount} x {row.CatalystItemId}");
    }

    public async Task MergeAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade != UpgradeWindow.PetLevelUp || player.Character is not { } character) return;
        if (!PetLevelUpFrames.TryReadRequest(payload.Span, out var request))
        {
            Log(player, $"merge request of {payload.Length} bytes not read");
            return;
        }
        if (player.UpgradeBusy)
        {
            await RefuseAsync(player, Opcode.S2C_PET_MERGE_REFUSED, PetMergeRules.Failed, "a request is pending").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var (first, firstEquipped) = Find(character.CharacterId, bag, request.PetWireUid);
        var (second, secondEquipped) = Find(character.CharacterId, bag, request.CatalystWireUid);
        if (second is not null && second.ItemUid == first?.ItemUid) second = null;
        var firstDefinition = first is null ? null : _catalog.GetById(first.ItemIndex);
        var secondDefinition = second is null ? null : _catalog.GetById(second.ItemIndex);
        var code = PetMergeRules.Check(firstDefinition, firstEquipped, secondDefinition, secondEquipped);
        if (code != PetMergeRules.Ok || first is null || second is null || firstDefinition is null || secondDefinition is null)
        {
            await RefuseAsync(player, Opcode.S2C_PET_MERGE_REFUSED, code, $"pets 0x{request.PetWireUid:X} and 0x{request.CatalystWireUid:X}").ConfigureAwait(false);
            return;
        }
        var rules = _propertyRules();
        var firstProperties = rules.Full(firstDefinition, _properties.ForItem(first.ItemUid));
        var secondProperties = rules.Full(secondDefinition, _properties.ForItem(second.ItemUid));
        if (PetMergeRules.Gains(firstProperties, secondProperties, first.ItemSlot, second.ItemSlot) < 1)
        {
            await RefuseAsync(player, Opcode.S2C_PET_MERGE_REFUSED, PetMergeRules.NoGain, $"pet 0x{request.CatalystWireUid:X} improves nothing").ConfigureAwait(false);
            return;
        }
        if (!Fits(player, bag, firstDefinition))
        {
            await RefuseAsync(player, Opcode.S2C_PET_MERGE_REFUSED, PetMergeRules.Failed, "no room").ConfigureAwait(false);
            return;
        }
        var grade = PetMergeRules.Grade(firstDefinition);
        List<ItemProperty> merged;
        byte slots;
        lock (_rngGate)
        {
            int Rand() => _rng.Next(PetMergeRules.RandMax + 1);
            merged = PetMergeRules.Merged(grade, firstProperties, secondProperties, rules.Defaults(firstDefinition), Rand);
            slots = PetMergeRules.MergedSlots(grade, first.ItemSlot, second.ItemSlot, Rand);
        }
        var rows = new List<EquipUpgradeItem>
        {
            EquipUpgradeOperations.Insert(firstDefinition.Id, firstDefinition.ObjectClass, 1, slots),
            EquipUpgradeOperations.Delete(first),
            EquipUpgradeOperations.Delete(second),
        };
        if (await ApplyAsync(player, character.CharacterId, rows, Opcode.S2C_PET_MERGE_REFUSED, PetMergeRules.Failed).ConfigureAwait(false) is not { } applied) return;
        Store(player, applied, merged, first.ItemUid, second.ItemUid);
        await SendAsync(player, Opcode.S2C_PET_MERGE_RESULT, PetLevelUpFrames.Result(applied.Items.Select(EquipUpgradeOperations.ToBag).ToList())).ConfigureAwait(false);
        Log(player, $"pets 0x{first.ItemUid:X} and 0x{second.ItemUid:X} merged (grade {grade})");
    }

    private (Item? Item, bool Equipped) Find(ulong characterId, IReadOnlyList<Item> bag, ulong wireUid)
    {
        if (wireUid == 0) return (null, false);
        var stored = BagItemIds.Stored(wireUid);
        if (bag.FirstOrDefault(item => item.ItemUid == stored) is { } held) return (held, false);
        var worn = _items.GetForCharacterBySlotKind(characterId, SlotKind.Equipment).FirstOrDefault(item => item.ItemUid == stored);
        return (worn, worn is not null);
    }

    private bool Fits(InGameSessionState player, IReadOnlyList<Item> bag, ItemParamEntry created)
    {
        if (bag.Count >= _bagCapacity) return false;
        var bagWeight = bag.Sum(held => (_catalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits);
        return bagWeight + created.Weight <= player.PlayerMaxWt;
    }

    private async Task<EquipStatusUpgradeResult?> ApplyAsync(InGameSessionState player, ulong characterId, List<EquipUpgradeItem> rows, Opcode refusal, ushort code)
    {
        player.UpgradeBusy = true;
        EquipStatusUpgradeResult applied;
        try
        {
            applied = await _master.ApplyAsync(new EquipStatusUpgrade(characterId, 0, _bagCapacity, rows), default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (applied.Succeeded && applied.Items.Any(item => item.Action == EquipUpgradeAction.Insert)) return applied;
        await RefuseAsync(player, refusal, code, $"store refused with code {applied.Error}").ConfigureAwait(false);
        return null;
    }

    private void Store(InGameSessionState player, EquipStatusUpgradeResult applied, IReadOnlyList<ItemProperty> properties, params ulong[] removed)
    {
        var created = applied.Items.First(item => item.Action == EquipUpgradeAction.Insert);
        _properties.Replace(created.ItemUid, properties.Take(ItemPropertyTypes.MaxStored).ToList());
        foreach (var uid in removed) _properties.Delete(uid);
        foreach (var gone in applied.Items.Where(item => item.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(gone.ItemUid);
    }

    private async Task RefuseAsync(InGameSessionState player, Opcode opcode, ushort code, string reason)
    {
        Log(player, $"refused with code {code}: {reason}");
        await SendAsync(player, opcode, PetLevelUpFrames.Refused(code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [pet-evolve-merge] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
