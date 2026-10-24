using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class MeltingController
{
    private readonly MasterEquipStatusUpgrade _master;
    private readonly Func<MeltingTables> _tables;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public MeltingController(
        MasterEquipStatusUpgrade master,
        Func<MeltingTables> tables,
        IItemStore items,
        IItemParamLookup catalog,
        IItemPropertyStore properties,
        int bagCapacity,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = UpgradeWindow.Melting;
        player.UpgradeBusy = false;
        await SendAsync(player, Opcode.S2C_MELTING_OPENED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "window opened");
        return NpcScript.Silent;
    }

    public async Task CloseAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade == UpgradeWindow.Melting) player.Upgrade = UpgradeWindow.None;
        await SendAsync(player, Opcode.S2C_MELTING_CLOSED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "window closed");
    }

    public async Task MeltAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!MeltingFrames.TryReadRequest(payload.Span, out var wireUids))
        {
            await RefuseAsync(player, MeltingRules.Unknown, $"request of {payload.Length} bytes not read").ConfigureAwait(false);
            return;
        }
        if (player.Upgrade != UpgradeWindow.Melting || player.UpgradeBusy || player.Character is not { } character || player.Stats is not { } stats)
        {
            await RefuseAsync(player, MeltingRules.InvalidState, "window not open or busy").ConfigureAwait(false);
            return;
        }
        var stored = wireUids.Select(BagItemIds.Stored).ToList();
        if (stored.Count is 0 or > MeltingRules.MaxItems || stored.Distinct().Count() != stored.Count)
        {
            await RefuseAsync(player, MeltingRules.Unknown, $"{stored.Count} items, duplicates {stored.Count - stored.Distinct().Count()}").ConfigureAwait(false);
            return;
        }
        var tables = _tables();
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var worn = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Equipment);
        var melted = new List<(Item Item, ItemParamEntry Definition)>(stored.Count);
        foreach (var uid in stored)
        {
            var item = bag.FirstOrDefault(held => held.ItemUid == uid);
            var definition = item is null ? null : _catalog.GetById(item.ItemIndex);
            var code = MeltingRules.CheckItem(item is not null, worn.Any(held => held.ItemUid == uid), item is not null && Busy(player, item), definition);
            if (code != MeltingRules.Ok || item is null || definition is null)
            {
                await RefuseAsync(player, code, $"item 0x{uid:X}").ConfigureAwait(false);
                return;
            }
            melted.Add((item, definition));
        }
        if (tables.Info is not { } info)
        {
            await RefuseAsync(player, MeltingRules.NoMeltingInfo, "no MeltingInfo row").ConfigureAwait(false);
            return;
        }
        var total = melted.Sum(entry => MeltingRules.Score(info, entry.Definition, entry.Item,
            MeltingRules.StatQuality(entry.Definition, _properties.ForItem(entry.Item.ItemUid), tables.RandomStatMinPercent, tables.RandomStatMaxPercent)));
        if (MeltingRules.Band(tables.Drops, total) is not { } band || _catalog.GetById(band.DropItem) is not { } box)
        {
            await RefuseAsync(player, MeltingRules.NoMeltingInfo, $"no MeltingDropInfo band for score {total}").ConfigureAwait(false);
            return;
        }
        var fee = MeltingRules.Fee(band, melted.Count);
        if (fee > stats.Money)
        {
            await RefuseAsync(player, MeltingRules.NotEnoughGelder, $"fee {fee}, money {stats.Money}").ConfigureAwait(false);
            return;
        }
        var joins = box.IsStackable ? bag.FirstOrDefault(held => held.ItemIndex == box.Id) : null;
        if (joins is null && bag.Count - melted.Count + 1 > _bagCapacity)
        {
            await RefuseAsync(player, MeltingRules.NoBagSpace, $"{bag.Count} items in the bag").ConfigureAwait(false);
            return;
        }
        var weight = bag.Sum(held => (_catalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits)
            - melted.Sum(entry => entry.Definition.Weight * entry.Item.WeightUnits)
            + box.Weight;
        if (weight > player.PlayerMaxWt)
        {
            await RefuseAsync(player, MeltingRules.OverWeight, $"weight {weight} over {player.PlayerMaxWt}").ConfigureAwait(false);
            return;
        }
        var rows = melted.Select(entry => EquipUpgradeOperations.Delete(entry.Item)).ToList();
        rows.Add(joins is not null
            ? EquipUpgradeOperations.Update(joins, (ushort)Math.Min(joins.Stack + 1, ushort.MaxValue), joins.ItemSlot)
            : EquipUpgradeOperations.Insert(box.Id, box.ObjectClass, 1, box.ObjectClass == ItemParamEntry.RareClass ? (byte)box.CompoundSlot : (byte)0));
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
            await RefuseAsync(player, result.Error == EquipStatusUpgradeRules.NotEnoughMoney ? MeltingRules.NotEnoughGelder
                : result.Error == EquipStatusUpgradeRules.BagFull ? MeltingRules.NoBagSpace : MeltingRules.Unknown, $"store refused with code {result.Error}").ConfigureAwait(false);
            return;
        }
        stats.GrantMoney(result.RemainMoney - stats.Money, tPoseidon.Core.Game.Stats.StatChangeSource.EquipUpgrade);
        foreach (var removed in result.Items.Where(row => row.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(removed.ItemUid);
        await SendAsync(player, Opcode.S2C_MELTING_RESULT, MeltingFrames.Result(result.RemainMoney, result.Items.Select(EquipUpgradeOperations.ToBag).ToList())).ConfigureAwait(false);
        Log(player, $"melts {melted.Count} items (score {total}, band {band.Id}) into item {box.Id} for {fee} gelder");
    }

    private static bool Busy(InGameSessionState player, Item item)
    {
        var wireUid = BagItemIds.OnWire(item.ItemUid);
        if (item.BreakedAt > Item.NeverBreaks) return true;
        if (player.Stall is { } stall && stall.Slots.Any(slot => BagItemIds.Stored(slot.WireUid) == item.ItemUid)) return true;
        return player.Trade is { } trade && trade.SideOf(player).Offered.Any(slot => BagItemIds.Stored(slot.WireUid) == item.ItemUid || slot.WireUid == wireUid);
    }

    private async Task RefuseAsync(InGameSessionState player, byte code, string reason)
    {
        Log(player, $"refused with code {code}: {reason}");
        await SendAsync(player, Opcode.S2C_MELTING_REFUSED, MeltingFrames.Refused(code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [melting] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
