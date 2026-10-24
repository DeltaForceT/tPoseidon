using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class SlotExtendController
{
    private readonly MasterEquipStatusUpgrade _master;
    private readonly Func<SlotExtendTables> _tables;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public SlotExtendController(
        MasterEquipStatusUpgrade master,
        Func<SlotExtendTables> tables,
        IItemStore items,
        IItemParamLookup catalog,
        int bagCapacity,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = UpgradeWindow.SlotExtend;
        player.UpgradeBusy = false;
        await SendAsync(player, Opcode.S2C_SLOT_EXTEND_OPENED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "window opened");
        return NpcScript.Silent;
    }

    public async Task CloseAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade == UpgradeWindow.SlotExtend) player.Upgrade = UpgradeWindow.None;
        await SendAsync(player, Opcode.S2C_SLOT_EXTEND_CLOSED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "window closed");
    }

    public async Task ExtendAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!SlotExtendFrames.TryReadRequest(payload.Span, out var request))
        {
            await RefuseAsync(player, SlotExtendRules.Unknown, $"request of {payload.Length} bytes not read").ConfigureAwait(false);
            return;
        }
        if (player.Upgrade != UpgradeWindow.SlotExtend || player.UpgradeBusy || player.Character is not { } character || player.Stats is not { } stats)
        {
            await RefuseAsync(player, SlotExtendRules.InvalidState, "window not open or busy").ConfigureAwait(false);
            return;
        }
        var tables = _tables();
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        Item? InBag(ulong wireUid) => bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(wireUid));
        var equipment = InBag(request.EquipmentWireUid);
        var material = InBag(request.MaterialWireUid);
        var subItems = request.SubItemWireUids.Select(InBag).ToList();
        var materialRow = material is not null && tables.Materials.TryGetValue(material.ItemIndex, out var foundMaterial) ? foundMaterial : null;
        var subRows = subItems.Select(sub => sub is not null && tables.SubItems.TryGetValue(sub.ItemIndex, out var row) ? row : null).ToList();
        var candidate = new SlotExtendCandidate(equipment is null ? null : _catalog.GetById(equipment.ItemIndex), equipment?.ItemSlot ?? 0, materialRow, subRows);
        var equipped = equipment is null
            && _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Equipment).Any(item => item.ItemUid == BagItemIds.Stored(request.EquipmentWireUid));
        var code = equipped ? SlotExtendRules.InvalidEquipment : SlotExtendRules.Check(candidate);
        if (code != SlotExtendRules.Ok || equipment is null || material is null || materialRow is null)
        {
            await RefuseAsync(player, code == SlotExtendRules.Ok ? SlotExtendRules.Unknown : code, $"equipment 0x{request.EquipmentWireUid:X} material 0x{request.MaterialWireUid:X}").ConfigureAwait(false);
            return;
        }
        var target = equipment.ItemSlot + 1;
        var subs = subRows.OfType<SlotExtendSubItemEntry>().ToList();
        var chance = SlotExtendRules.Chance(materialRow, subs, target);
        double roll;
        lock (_rngGate) roll = _rng.NextDouble();
        var success = SlotExtendRules.Succeeds(chance, roll);
        var guarded = SlotExtendRules.Guarded(subs);
        var rows = new List<EquipUpgradeItem>(4);
        if (success) rows.Add(EquipUpgradeOperations.Update(equipment, equipment.Stack, (byte)target));
        else if (!guarded) rows.Add(EquipUpgradeOperations.Delete(equipment));
        rows.Add(EquipUpgradeOperations.Take(material, 1));
        rows.AddRange(subItems.OfType<Item>().Select(sub => EquipUpgradeOperations.Take(sub, 1)));
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
            await RefuseAsync(player, SlotExtendRules.Unknown, $"store refused with code {result.Error}").ConfigureAwait(false);
            return;
        }
        stats.GrantMoney(result.RemainMoney - stats.Money, tPoseidon.Core.Game.Stats.StatChangeSource.EquipUpgrade);
        foreach (var removed in result.Items.Where(row => row.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(removed.ItemUid);
        await SendAsync(player, Opcode.S2C_SLOT_EXTEND_RESULT, SlotExtendFrames.Result(result.RemainMoney, result.Items.Select(EquipUpgradeOperations.ToBag).ToList())).ConfigureAwait(false);
        Log(player, $"item {equipment.ItemIndex} slot {equipment.ItemSlot} -> {target}: {(success ? "success" : guarded ? "failed, guarded" : "failed, destroyed")} (chance {chance:F2}%, material {material.ItemIndex}, {subs.Count} sub items)");
    }

    private async Task RefuseAsync(InGameSessionState player, byte code, string reason)
    {
        Log(player, $"refused with code {code}: {reason}");
        await SendAsync(player, Opcode.S2C_SLOT_EXTEND_REFUSED, SlotExtendFrames.Refused(code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [slot-extend] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
