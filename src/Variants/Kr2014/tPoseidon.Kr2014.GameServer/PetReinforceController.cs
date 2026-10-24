using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class PetReinforceController
{
    private readonly Func<PetGrowthTables> _tables;
    private readonly Func<ItemPropertyRules> _propertyRules;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public PetReinforceController(
        Func<PetGrowthTables> tables,
        Func<ItemPropertyRules> propertyRules,
        IItemStore items,
        IItemParamLookup catalog,
        IItemPropertyStore properties,
        string logTag)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _propertyRules = propertyRules ?? throw new ArgumentNullException(nameof(propertyRules));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = UpgradeWindow.PetReinforce;
        await SendAsync(player, Opcode.S2C_PET_REINFORCE_OPENED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "reinforce window opened");
        return NpcScript.Silent;
    }

    public void Close(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade == UpgradeWindow.PetReinforce) player.Upgrade = UpgradeWindow.None;
        Log(player, "reinforce window closed");
    }

    public async Task ReinforceAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade != UpgradeWindow.PetReinforce || player.Character is not { } character) return;
        if (!PetReinforceFrames.TryReadRequest(payload.Span, out var request))
        {
            await RefuseAsync(player, $"request of {payload.Length} bytes").ConfigureAwait(false);
            return;
        }
        var tables = _tables();
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        Item? InBag(ulong wireUid) => wireUid == 0 ? null : bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(wireUid));
        var aidItems = new[] { request.FirstAidWireUid, request.SecondAidWireUid }.Where(uid => uid != 0).Select(InBag).ToList();
        if (aidItems.Any(item => item is null))
        {
            await RefuseAsync(player, "an aid item is not in the bag").ConfigureAwait(false);
            return;
        }
        var aidRows = aidItems.Select(item => tables.Aids.TryGetValue(item!.ItemIndex, out var help) ? help : null).ToList();
        if (aidRows.Count == 2 && aidRows[0] is { } first && aidRows[1] is { } second
            && GrowthRules.AidsClash(Aid(first), Aid(second)))
        {
            await RefuseAsync(player, $"aid items {first.ItemId} and {second.ItemId} do the same thing").ConfigureAwait(false);
            return;
        }
        var aids = aidRows.Where(row => row is not null).Select(row => Aid(row!)).ToList();
        var pet = InBag(request.PetWireUid);
        if (pet is null || _catalog.GetById(pet.ItemIndex) is not { ObjectClass: ItemParamEntry.RareClass } definition)
        {
            await RefuseAsync(player, $"pet 0x{request.PetWireUid:X} not found").ConfigureAwait(false);
            return;
        }
        var stored = _properties.ForItem(pet.ItemUid);
        var rules = _propertyRules();
        var slot = GrowthRules.FreeSlot(rules.Full(definition, stored), pet.ItemSlot);
        if (slot == GrowthRules.NoSlot)
        {
            await RefuseAsync(player, $"pet {pet.ItemIndex} has no free mix slot ({pet.ItemSlot} slots)").ConfigureAwait(false);
            return;
        }
        if (pet.RefineState == RefineRules.Broken && (definition.Options & GrowthRules.BrokenUsableOption) == 0)
        {
            await RefuseAsync(player, $"pet {pet.ItemIndex} refine state {pet.RefineState}").ConfigureAwait(false);
            return;
        }
        var material = InBag(request.MaterialWireUid);
        if (material is null)
        {
            await RefuseAsync(player, $"material 0x{request.MaterialWireUid:X} not found").ConfigureAwait(false);
            return;
        }
        if (!tables.PetProperties.TryGetValue(pet.ItemIndex, out var petRow))
        {
            await RefuseAsync(player, $"pet {pet.ItemIndex} has no {PetGrowthTables.PropertyTable} row").ConfigureAwait(false);
            return;
        }
        if (!tables.Materials.TryGetValue(material.ItemIndex, out var materialRow)
            || !petRow.EnableProperties.Contains(materialRow.EnableProperty)
            || material.Stack < materialRow.ItemNum
            || definition.MinLevel < materialRow.EquipLevel)
        {
            await RefuseAsync(player, $"material {material.ItemIndex} does not fit pet {pet.ItemIndex}").ConfigureAwait(false);
            return;
        }
        var chance = GrowthRules.Chance(petRow.ApplyRatio, aids, player.PlayerLk, definition.MinLevel, 0f);
        var deltas = GrowthRules.Deltas(materialRow, aids);
        short ability;
        lock (_rngGate)
        {
            ability = _rng.Next(GrowthRules.ChanceRoll) < chance && rules.CurveBin(materialRow.Curve, _rng) is { } bin
                ? GrowthRules.Ability(materialRow, deltas, bin)
                : GrowthRules.FailedAbility(materialRow, deltas);
        }
        var materialOperation = Take(material, materialRow.ItemNum);
        var aidOperations = aidItems.Select(item => Take(item!, 1)).ToList();
        while (aidOperations.Count < 2) aidOperations.Add(new BagOperation(0, ItemParamEntry.StackableClass, 0, 0, 0));
        var element = new ItemProperty(slot, materialRow.EnableProperty, ability, (uint)material.ItemIndex);
        _properties.Replace(pet.ItemUid, stored.Append(element).ToList());
        await SendAsync(player, Opcode.S2C_PET_REINFORCE_RESULT,
            PetReinforceFrames.Result(materialOperation, BagItemIds.OnWire(pet.ItemUid), element, aidOperations)).ConfigureAwait(false);
        Log(player, $"pet {pet.ItemIndex} slot {slot}: property {materialRow.EnableProperty} = {ability} (chance {chance}%, material {material.ItemIndex})");
    }

    public async Task<int> OpenResetAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = UpgradeWindow.PetReinforceReset;
        await SendAsync(player, Opcode.S2C_PET_REINFORCE_RESET_OPENED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "reset window opened");
        return NpcScript.Silent;
    }

    public void CloseReset(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade == UpgradeWindow.PetReinforceReset) player.Upgrade = UpgradeWindow.None;
        Log(player, "reset window closed");
    }

    public async Task ResetAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade != UpgradeWindow.PetReinforceReset || player.Character is not { } character) return;
        if (!PetReinforceFrames.TryReadResetRequest(payload.Span, out var request))
        {
            await RefuseResetAsync(player, $"request of {payload.Length} bytes").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        Item? InBag(ulong wireUid) => wireUid == 0 ? null : bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(wireUid));
        var pet = InBag(request.PetWireUid);
        if (pet is null || _catalog.GetById(pet.ItemIndex) is not { ObjectClass: ItemParamEntry.RareClass })
        {
            await RefuseResetAsync(player, $"pet 0x{request.PetWireUid:X} not found").ConfigureAwait(false);
            return;
        }
        if (!_tables().PetProperties.ContainsKey(pet.ItemIndex))
        {
            await RefuseResetAsync(player, $"pet {pet.ItemIndex} has no {PetGrowthTables.PropertyTable} row").ConfigureAwait(false);
            return;
        }
        var stored = _properties.ForItem(pet.ItemUid);
        if (!stored.Any(property => property.Type is >= 0 and <= ItemPropertyTypes.LastMixSlot))
        {
            await RefuseResetAsync(player, $"pet {pet.ItemIndex} has no mix slot in use").ConfigureAwait(false);
            return;
        }
        var material = InBag(request.MaterialWireUid);
        if (material is null || material.ItemIndex != PetGrowthTables.ResetMaterialItemId)
        {
            await RefuseResetAsync(player, $"material 0x{request.MaterialWireUid:X} is not item {PetGrowthTables.ResetMaterialItemId}").ConfigureAwait(false);
            return;
        }
        if (request.Slot > ItemPropertyTypes.LastMixSlot || !stored.Any(property => property.Type == (int)request.Slot))
        {
            await RefuseResetAsync(player, $"slot {request.Slot} of pet {pet.ItemIndex} is empty").ConfigureAwait(false);
            return;
        }
        var materialOperation = Take(material, 1);
        _properties.Replace(pet.ItemUid, stored.Where(property => property.Type != (int)request.Slot).ToList());
        await SendAsync(player, Opcode.S2C_PET_REINFORCE_RESET_RESULT,
            PetReinforceFrames.ResetResult(BagItemIds.OnWire(pet.ItemUid), request.Slot, materialOperation)).ConfigureAwait(false);
        Log(player, $"pet {pet.ItemIndex} slot {request.Slot} cleared");
    }

    private async Task RefuseResetAsync(InGameSessionState player, string reason)
    {
        Log(player, $"reset refused: {reason}");
        await SendAsync(player, Opcode.S2C_PET_REINFORCE_RESET_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
    }

    private static GrowthAid Aid(MaturingHelpRow row) => new(row.ApplyRatio, row.MinAbility, row.MaxAbility);

    private BagOperation Take(Item held, int count)
    {
        var itemClass = _catalog.GetById(held.ItemIndex)?.ObjectClass ?? held.ItemClass;
        if (held.Stack <= count)
        {
            _items.Delete(held.ItemUid);
            return new BagOperation(QuestResultFrames.Removed, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), 0);
        }
        var left = _items.AdjustStack(held.ItemUid, -count);
        return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), left);
    }

    private async Task RefuseAsync(InGameSessionState player, string reason)
    {
        Log(player, $"reinforce refused: {reason}");
        await SendAsync(player, Opcode.S2C_GROWTH_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [pet-reinforce] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
