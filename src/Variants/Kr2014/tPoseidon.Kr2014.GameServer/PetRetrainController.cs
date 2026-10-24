using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class PetRetrainController
{
    private readonly MasterEquipStatusUpgrade _master;
    private readonly Func<PetRetrainTables> _tables;
    private readonly Func<ItemPropertyRules> _propertyRules;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public PetRetrainController(
        MasterEquipStatusUpgrade master,
        Func<PetRetrainTables> tables,
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

    public async Task<int> OpenAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = UpgradeWindow.PetRetrain;
        player.UpgradeBusy = false;
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_PET_RETRAIN_OPENED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "window opened");
        return NpcScript.Silent;
    }

    public Task CloseAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade == UpgradeWindow.PetRetrain) player.Upgrade = UpgradeWindow.None;
        Log(player, "window closed");
        return Task.CompletedTask;
    }

    public async Task RetrainAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!PetRetrainFrames.TryReadRequest(payload.Span, out var request))
        {
            Log(player, $"request of {payload.Length} bytes not read");
            return;
        }
        if (player.Upgrade != UpgradeWindow.PetRetrain || player.UpgradeBusy || player.Character is not { } character)
        {
            Log(player, "request outside the retrain window or while one is pending");
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        Item? InBag(ulong wireUid) => wireUid == 0 ? null : bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(wireUid));
        var pet = InBag(request.PetWireUid);
        var material = InBag(request.MaterialWireUid);
        var lockItem = InBag(request.LockWireUid);
        var petDefinition = pet is null ? null : _catalog.GetById(pet.ItemIndex);
        var tables = _tables();
        var code = PetRetrainRules.Check(
            petDefinition,
            material is null || material.ItemUid == pet?.ItemUid ? null : new HeldItem(material.ItemIndex, material.Stack),
            request.LockWireUid != 0,
            lockItem is null || lockItem.ItemUid == material?.ItemUid ? null : new HeldItem(lockItem.ItemIndex, lockItem.Stack),
            request.LockStatId,
            tables);
        if (code != PetRetrainRules.Ok || pet is null || petDefinition is null || material is null || !tables.Materials.TryGetValue(material.ItemIndex, out var row))
        {
            await RefuseAsync(player, code, $"pet 0x{request.PetWireUid:X} material 0x{request.MaterialWireUid:X} lock 0x{request.LockWireUid:X} stat {request.LockStatId}").ConfigureAwait(false);
            return;
        }
        var stored = _properties.ForItem(pet.ItemUid);
        PetRetrainOutcome outcome;
        lock (_rngGate)
        {
            outcome = PetRetrainRules.Retrain(petDefinition, stored, _propertyRules().Full(petDefinition, stored), row, request.LockStatId, tables,
                () => _rng.Next(ItemPropertyRules.CurveRolls));
        }
        var rows = new List<EquipUpgradeItem>(2) { EquipUpgradeOperations.Take(material, (ushort)row.ItemCount) };
        if (lockItem is not null) rows.Add(EquipUpgradeOperations.Take(lockItem, (ushort)row.LockItemCount));
        player.UpgradeBusy = true;
        EquipStatusUpgradeResult result;
        try
        {
            result = await _master.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, 0, _bagCapacity, rows), default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (!result.Succeeded)
        {
            await RefuseAsync(player, PetRetrainRules.StoreFailed, $"store refused with code {result.Error}").ConfigureAwait(false);
            return;
        }
        _properties.Replace(pet.ItemUid, outcome.Stored);
        foreach (var removed in result.Items.Where(item => item.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(removed.ItemUid);
        await SendAsync(player, Opcode.S2C_PET_RETRAIN_RESULT, PetRetrainFrames.Result(
            BagItemIds.OnWire(pet.ItemUid),
            outcome.Rolled,
            result.Items.Select(EquipUpgradeOperations.ToBag).ToList())).ConfigureAwait(false);
        Log(player, $"pet {petDefinition.Id} retrained with item {material.ItemIndex}: {string.Join(", ", outcome.Rolled.Select(property => $"{property.Id}={property.Value}"))}{(request.LockStatId != 0 ? $", {request.LockStatId} locked" : string.Empty)}");
    }

    private async Task RefuseAsync(InGameSessionState player, ushort code, string reason)
    {
        Log(player, $"refused with code {code}: {reason}");
        await SendAsync(player, Opcode.S2C_PET_RETRAIN_REFUSED, PetRetrainFrames.Refused(code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [pet-retrain] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
