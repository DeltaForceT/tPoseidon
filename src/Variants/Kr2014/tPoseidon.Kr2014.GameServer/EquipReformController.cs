using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class EquipReformController
{
    public const int ResetWindow = 1;

    private readonly MasterEquipStatusUpgrade _master;
    private readonly Func<EquipReformTables> _tables;
    private readonly Func<RefineTables> _refineTables;
    private readonly Func<ItemPropertyRules> _propertyRules;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public EquipReformController(
        MasterEquipStatusUpgrade master,
        Func<EquipReformTables> tables,
        Func<RefineTables> refineTables,
        Func<ItemPropertyRules> propertyRules,
        IItemStore items,
        IItemParamLookup catalog,
        IItemPropertyStore properties,
        int bagCapacity,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _refineTables = refineTables ?? throw new ArgumentNullException(nameof(refineTables));
        _propertyRules = propertyRules ?? throw new ArgumentNullException(nameof(propertyRules));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenAsync(InGameSessionState player, int window)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = window == ResetWindow ? UpgradeWindow.ReformReset : UpgradeWindow.EquipReform;
        player.UpgradeBusy = false;
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        await SendAsync(player, window == ResetWindow ? Opcode.S2C_REFORM_RESET_OPENED : Opcode.S2C_EQUIP_REFORM_OPENED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, window == ResetWindow ? "reform count reset window opened" : "reform window opened");
        return NpcScript.Silent;
    }

    public Task CloseAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade is UpgradeWindow.EquipReform or UpgradeWindow.ReformReset) player.Upgrade = UpgradeWindow.None;
        Log(player, "window closed");
        return Task.CompletedTask;
    }

    public async Task ReformAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade != UpgradeWindow.EquipReform || player.UpgradeBusy || player.Character is not { } character || player.Stats is not { } stats)
        {
            await RefuseAsync(player, EquipReformRules.Failed, "window not open or busy").ConfigureAwait(false);
            return;
        }
        if (!EquipReformFrames.TryReadRequest(payload.Span, out var request))
        {
            await RefuseAsync(player, EquipReformRules.Failed, $"request of {payload.Length} bytes not read").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        Item? InBag(ulong wireUid) => wireUid == 0 ? null : bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(wireUid));
        var target = InBag(request.TargetWireUid);
        var material = InBag(request.MaterialWireUid);
        var elixir = InBag(request.FirstElixirWireUid);
        var missing = target is null || material is null
            || (request.FirstElixirWireUid != 0 && elixir is null)
            || (request.SecondElixirWireUid != 0 && InBag(request.SecondElixirWireUid) is null);
        if (missing || target is null || material is null || target.ItemUid == material.ItemUid)
        {
            await RefuseAsync(player, EquipReformRules.Failed, $"target 0x{request.TargetWireUid:X} or material 0x{request.MaterialWireUid:X} not in the bag").ConfigureAwait(false);
            return;
        }
        var tables = _tables();
        var needed = EquipReformRules.ElixirsNeeded(request);
        if (elixir is not null && elixir.Stack < needed)
        {
            await RefuseAsync(player, EquipReformRules.ElixirShort, $"elixir stack {elixir.Stack} below {needed}").ConfigureAwait(false);
            return;
        }
        foreach (var choice in request.Choices.Where(choice => choice.IsChosen))
        {
            if (choice.ElixirItemId == 0)
            {
                await RefuseAsync(player, EquipReformRules.ElixirMismatch, $"stat slot {choice.Slot} chosen without an elixir").ConfigureAwait(false);
                return;
            }
            if (elixir is null || elixir.ItemIndex != choice.ElixirItemId || !tables.Elixirs.ContainsKey(choice.ElixirItemId)
                || choice.Slot < 0 || choice.Slot > EquipReformRules.GunApSlot)
            {
                await RefuseAsync(player, EquipReformRules.ElixirShort, $"stat slot {choice.Slot} elixir {choice.ElixirItemId} not registered").ConfigureAwait(false);
                return;
            }
        }
        if (_catalog.GetById(target.ItemIndex) is not { } definition)
        {
            await RefuseAsync(player, EquipReformRules.Failed, $"item {target.ItemIndex} has no definition").ConfigureAwait(false);
            return;
        }
        var stored = _properties.ForItem(target.ItemUid);
        var row = tables.MaterialFor(definition.MinLevel);
        var materialCode = EquipReformRules.CheckMaterial(definition, stored, row, material.ItemIndex, material.Stack);
        if (materialCode != 0 || row is null)
        {
            await RefuseAsync(player, EquipReformRules.Failed, $"item {definition.Id} (level {definition.MinLevel}) with material {material.ItemIndex} x{material.Stack} is not reformable").ConfigureAwait(false);
            return;
        }
        var fee = EquipReformRules.Fee(definition, tables.CostDiscount);
        if (fee > stats.Money)
        {
            await RefuseAsync(player, EquipReformRules.NotEnoughMoney, $"fee {fee}, money {stats.Money}").ConfigureAwait(false);
            return;
        }
        var hasGunAp = _propertyRules().Full(definition, stored).Any(property => property.Type == ItemPropertyTypes.Stat && property.Id == ItemPropertyRules.GunApId);
        EquipReformOutcome outcome;
        lock (_rngGate)
        {
            outcome = EquipReformRules.Reform(definition, stored, hasGunAp, target.RefineLevel, request, tables, _refineTables(), () => _rng.Next(ItemPropertyRules.CurveRolls));
        }
        var rows = new List<EquipUpgradeItem>(2) { EquipUpgradeOperations.Take(material, (ushort)row.ItemCount) };
        if (elixir is not null) rows.Add(EquipUpgradeOperations.Take(elixir, (ushort)needed));
        player.UpgradeBusy = true;
        EquipStatusUpgradeResult result;
        try
        {
            result = await _master.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, (uint)fee, _bagCapacity, rows), default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (!result.Succeeded)
        {
            await RefuseAsync(player, result.Error == EquipStatusUpgradeRules.NotEnoughMoney ? EquipReformRules.NotEnoughMoney : EquipReformRules.Failed,
                $"store refused with code {result.Error}").ConfigureAwait(false);
            return;
        }
        _properties.Replace(target.ItemUid, outcome.Stored);
        stats.GrantMoney(result.RemainMoney - stats.Money, tPoseidon.Core.Game.Stats.StatChangeSource.EquipUpgrade);
        foreach (var removed in result.Items.Where(item => item.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(removed.ItemUid);
        var materialOperation = EquipUpgradeOperations.ToBag(result.Items.First(item => item.ItemUid == material.ItemUid));
        BagOperation? elixirOperation = elixir is null ? null : EquipUpgradeOperations.ToBag(result.Items.First(item => item.ItemUid == elixir.ItemUid));
        await SendAsync(player, Opcode.S2C_EQUIP_REFORM_RESULT, EquipReformFrames.Result(
            materialOperation,
            elixirOperation,
            BagItemIds.OnWire(target.ItemUid),
            (uint)target.ItemIndex,
            _propertyRules().Full(definition, outcome.Stored),
            result.RemainMoney,
            outcome.Refine)).ConfigureAwait(false);
        Log(player, $"item {definition.Id} reformed for {fee} gelder: {string.Join(", ", outcome.Rolled.Select(property => $"{property.Id}={property.Value}"))}; {outcome.Remaining} reforms left, refine bonus {outcome.Refine.PropertyId}={outcome.Refine.Value}");
    }

    public async Task ResetAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade != UpgradeWindow.ReformReset || player.UpgradeBusy || player.Character is not { } character || player.Stats is not { } stats)
        {
            await RefuseAsync(player, EquipReformRules.Failed, "reset window not open or busy").ConfigureAwait(false);
            return;
        }
        if (!EquipReformFrames.TryReadResetRequest(payload.Span, out var request))
        {
            await RefuseAsync(player, EquipReformRules.Failed, $"reset request of {payload.Length} bytes not read").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var target = bag.FirstOrDefault(item => request.TargetWireUid != 0 && item.ItemUid == BagItemIds.Stored(request.TargetWireUid));
        var material = bag.FirstOrDefault(item => request.MaterialWireUid != 0 && item.ItemUid == BagItemIds.Stored(request.MaterialWireUid));
        if (target is null || material is null || _catalog.GetById(target.ItemIndex) is not { } definition || _catalog.GetById(material.ItemIndex) is not { } materialDefinition)
        {
            await RefuseAsync(player, EquipReformRules.Failed, $"target 0x{request.TargetWireUid:X} or material 0x{request.MaterialWireUid:X} not in the bag").ConfigureAwait(false);
            return;
        }
        var stored = _properties.ForItem(target.ItemUid);
        var code = EquipReformRules.CheckReset(definition, stored, materialDefinition);
        if (code != 0)
        {
            await RefuseAsync(player, code, $"item {definition.Id} reset refused").ConfigureAwait(false);
            return;
        }
        player.UpgradeBusy = true;
        EquipStatusUpgradeResult result;
        try
        {
            result = await _master.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, 0, _bagCapacity, [EquipUpgradeOperations.Take(material, 1)]), default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (!result.Succeeded)
        {
            await RefuseAsync(player, EquipReformRules.Failed, $"store refused with code {result.Error}").ConfigureAwait(false);
            return;
        }
        _properties.Replace(target.ItemUid, EquipReformRules.Reset(definition, stored));
        if (result.Items.Any(item => item.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(material.ItemUid);
        await SendAsync(player, Opcode.S2C_REFORM_RESET_DONE, EquipReformFrames.ResetDone(BagItemIds.OnWire(target.ItemUid), BagItemIds.OnWire(material.ItemUid))).ConfigureAwait(false);
        Log(player, $"item {definition.Id} reform count reset to {definition.ReformCount} with item {material.ItemIndex}");
    }

    private async Task RefuseAsync(InGameSessionState player, uint code, string reason)
    {
        Log(player, $"refused with code 0x{code:X}: {reason}");
        var money = player.Stats?.Money ?? 0u;
        await SendAsync(player, Opcode.S2C_EQUIP_REFORM_REFUSED, EquipReformFrames.Refused(code, money)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [reform] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
