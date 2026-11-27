using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class GhostExchangeController
{
    private readonly MasterEquipStatusUpgrade _master;
    private readonly Func<GhostExchangeTables> _tables;
    private readonly Func<ItemPropertyRules> _propertyRules;
    private readonly Func<RefineTables> _refineTables;
    private readonly IItemPropertyStore _properties;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public GhostExchangeController(
        MasterEquipStatusUpgrade master,
        Func<GhostExchangeTables> tables,
        Func<ItemPropertyRules> propertyRules,
        Func<RefineTables> refineTables,
        IItemPropertyStore properties,
        IItemStore items,
        IItemParamLookup catalog,
        int bagCapacity,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _propertyRules = propertyRules ?? throw new ArgumentNullException(nameof(propertyRules));
        _refineTables = refineTables ?? throw new ArgumentNullException(nameof(refineTables));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenAsync(InGameSessionState player, bool neo)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = neo ? UpgradeWindow.GhostNeo : UpgradeWindow.GhostStone;
        player.UpgradeBusy = false;
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        await SendAsync(player, neo ? Opcode.S2C_GHOST_NEO_OPENED : Opcode.S2C_GHOST_STONE_OPENED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, $"{(neo ? "Neo equipment" : "growth stone")} window opened");
        return NpcScript.Silent;
    }

    public async Task CloseAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade is UpgradeWindow.GhostStone or UpgradeWindow.GhostNeo) player.Upgrade = UpgradeWindow.None;
        await SendAsync(player, Opcode.S2C_GHOST_CLOSED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "window closed");
    }

    public async Task ExchangeStoneAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!GhostExchangeFrames.TryReadStone(payload.Span, out var request))
        {
            await RefuseAsync(player, GhostExchangeRules.Unknown, $"stone request of {payload.Length} bytes not read").ConfigureAwait(false);
            return;
        }
        if (player.Upgrade != UpgradeWindow.GhostStone || player.UpgradeBusy || player.Character is not { } character || player.Stats is not { } stats)
        {
            await RefuseAsync(player, GhostExchangeRules.InvalidPlayerState, "growth stone window not open or busy").ConfigureAwait(false);
            return;
        }
        var tables = _tables();
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var source = bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(request.SourceWireUid));
        var sourceDefinition = source is null ? null : _catalog.GetById(source.ItemIndex);
        var target = _catalog.GetById((int)request.TargetItemId);
        var code = GhostExchangeRules.CheckStone(tables, sourceDefinition, source?.Stack ?? 0, request, target);
        if (code != GhostExchangeRules.Ok || source is null || sourceDefinition is null || target is null)
        {
            await RefuseAsync(player, code == GhostExchangeRules.Ok ? GhostExchangeRules.Unknown : code, $"item 0x{request.SourceWireUid:X} x{request.Count} for {request.TargetItemId}").ConfigureAwait(false);
            return;
        }
        var joins = target.IsStackable ? bag.FirstOrDefault(item => item.ItemIndex == target.Id) : null;
        var freed = source.Stack <= request.Count ? 1 : 0;
        if (joins is null && bag.Count - freed + 1 > _bagCapacity)
        {
            await RefuseAsync(player, GhostExchangeRules.InventoryFull, $"{bag.Count} items in the bag").ConfigureAwait(false);
            return;
        }
        if (BagWeight(bag) - sourceDefinition.Weight * request.Count + target.Weight * request.Count > player.PlayerMaxWt)
        {
            await RefuseAsync(player, GhostExchangeRules.OverWeight, $"weight over {player.PlayerMaxWt}").ConfigureAwait(false);
            return;
        }
        var rows = new List<EquipUpgradeItem>
        {
            EquipUpgradeOperations.Take(source, request.Count),
            joins is not null
                ? EquipUpgradeOperations.Update(joins, (ushort)Math.Min(joins.Stack + request.Count, ushort.MaxValue), joins.ItemSlot)
                : EquipUpgradeOperations.Insert(target.Id, target.ObjectClass, request.Count, 0),
        };
        if (await ApplyAsync(player, character.CharacterId, stats, rows).ConfigureAwait(false))
        {
            Log(player, $"exchanges {request.Count} x item {source.ItemIndex} for item {target.Id}");
        }
    }

    public async Task ExchangeNeoAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!GhostExchangeFrames.TryReadNeo(payload.Span, out var request))
        {
            await RefuseAsync(player, GhostExchangeRules.Unknown, $"Neo request of {payload.Length} bytes not read").ConfigureAwait(false);
            return;
        }
        if (player.Upgrade != UpgradeWindow.GhostNeo || player.UpgradeBusy || player.Character is not { } character || player.Stats is not { } stats)
        {
            await RefuseAsync(player, GhostExchangeRules.InvalidPlayerState, "Neo window not open or busy").ConfigureAwait(false);
            return;
        }
        var tables = _tables();
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var source = bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(request.SourceWireUid));
        var sourceDefinition = source is null ? null : _catalog.GetById(source.ItemIndex);
        var target = _catalog.GetById((int)request.TargetItemId);
        var code = GhostExchangeRules.CheckNeo(tables, source is not null, sourceDefinition, request, target);
        if (code != GhostExchangeRules.Ok || source is null || sourceDefinition is null || target is null)
        {
            await RefuseAsync(player, code == GhostExchangeRules.Ok ? GhostExchangeRules.Unknown : code, $"item 0x{request.SourceWireUid:X} for {request.TargetItemId}").ConfigureAwait(false);
            return;
        }
        if (GhostExchangeRules.RefineLevelAfter(tables, source.RefineLevel) is not { } level)
        {
            await RefuseAsync(player, GhostExchangeRules.Unknown, $"no GhostExchange_RefineLevel column for refine level {source.RefineLevel}").ConfigureAwait(false);
            return;
        }
        if (BagWeight(bag) - sourceDefinition.Weight + target.Weight > player.PlayerMaxWt)
        {
            await RefuseAsync(player, GhostExchangeRules.OverWeight, $"weight over {player.PlayerMaxWt}").ConfigureAwait(false);
            return;
        }
        int slots;
        lock (_rngGate) slots = _propertyRules().MixSlots(target.Id, target.CompoundSlot, _rng);
        var bonus = GhostExchangeRules.NeoRefineBonus(_refineTables(), target, _propertyRules().Defaults(target), level);
        var rows = new List<EquipUpgradeItem>
        {
            EquipUpgradeOperations.Delete(source),
            EquipUpgradeOperations.Insert(target.Id, target.ObjectClass, 1, (byte)Math.Clamp(slots, 0, byte.MaxValue), (short)target.RefineType, (byte)level),
        };
        var created = bonus is { } refineBonus ? new List<ItemProperty> { refineBonus } : new List<ItemProperty>();
        if (await ApplyAsync(player, character.CharacterId, stats, rows, created, source.ItemUid).ConfigureAwait(false))
        {
            Log(player, $"exchanges Neo item {source.ItemIndex} +{source.RefineLevel} for item {target.Id} +{level}");
        }
    }

    private async Task<bool> ApplyAsync(InGameSessionState player, ulong characterId, tPoseidon.Core.Game.Stats.StatManager stats, IReadOnlyList<EquipUpgradeItem> rows, IReadOnlyList<ItemProperty>? created = null, ulong removedUid = 0)
    {
        player.UpgradeBusy = true;
        EquipStatusUpgradeResult result;
        try
        {
            result = await _master.ApplyAsync(new EquipStatusUpgrade(characterId, 0, _bagCapacity, rows), default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (!result.Succeeded)
        {
            await RefuseAsync(player, result.Error == EquipStatusUpgradeRules.BagFull ? GhostExchangeRules.InventoryFull : GhostExchangeRules.Unknown,
                $"store refused with code {result.Error}").ConfigureAwait(false);
            return false;
        }
        stats.GrantMoney(result.RemainMoney - stats.Money, tPoseidon.Core.Game.Stats.StatChangeSource.EquipUpgrade);
        foreach (var removed in result.Items.Where(row => row.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(removed.ItemUid);
        if (created is { Count: > 0 })
        {
            var insert = result.Items.First(row => row.Action == EquipUpgradeAction.Insert);
            _properties.Replace(insert.ItemUid, created.ToList());
        }
        if (removedUid != 0) _properties.Delete(removedUid);
        var operations = created is { Count: > 0 }
            ? EquipUpgradeOperations.ToBagWithProperties(result.Items, created)
            : result.Items.Select(EquipUpgradeOperations.ToBag).ToList();
        await SendAsync(player, Opcode.S2C_GHOST_EXCHANGED, GhostExchangeFrames.Exchanged(operations)).ConfigureAwait(false);
        return true;
    }

    private int BagWeight(IReadOnlyList<Item> bag) => bag.Sum(item => (_catalog.GetById(item.ItemIndex)?.Weight ?? 0) * item.WeightUnits);

    private async Task RefuseAsync(InGameSessionState player, byte code, string reason)
    {
        Log(player, $"refused with code {code}: {reason}");
        await SendAsync(player, Opcode.S2C_GHOST_REFUSED, GhostExchangeFrames.Refused(code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [ghost-exchange] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
