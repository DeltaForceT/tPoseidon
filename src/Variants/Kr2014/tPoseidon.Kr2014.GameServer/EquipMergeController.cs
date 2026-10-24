using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class EquipMergeController
{
    private readonly MasterEquipStatusUpgrade _master;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly Func<ItemPropertyRules> _rules;
    private readonly Func<RefineTables> _refines;
    private readonly IReadOnlyDictionary<int, int> _supports;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public EquipMergeController(
        MasterEquipStatusUpgrade master,
        IItemStore items,
        IItemParamLookup catalog,
        IItemPropertyStore properties,
        Func<ItemPropertyRules> rules,
        Func<RefineTables> refines,
        IReadOnlyDictionary<int, int> supports,
        int bagCapacity,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _refines = refines ?? throw new ArgumentNullException(nameof(refines));
        _supports = supports ?? throw new ArgumentNullException(nameof(supports));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = UpgradeWindow.EquipMerge;
        player.UpgradeBusy = false;
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        await SendAsync(player, (Opcode)EquipMergeFrames.Opened, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "equipment fusion window opened");
        return NpcScript.Silent;
    }

    public async Task CloseAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade == UpgradeWindow.EquipMerge) player.Upgrade = UpgradeWindow.None;
        await SendAsync(player, (Opcode)EquipMergeFrames.Closed, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "equipment fusion window closed");
    }

    public async Task MergeAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!EquipMergeFrames.TryReadRequest(payload.Span, out var request))
        {
            Log(player, $"request of {payload.Length} bytes not read");
            return;
        }
        if (player.Upgrade != UpgradeWindow.EquipMerge || player.Character is not { } character || player.Stats is not { } stats)
        {
            Log(player, "fusion request outside the fusion window");
            return;
        }
        if (player.UpgradeBusy)
        {
            await RefuseAsync(player, EquipMergeRules.Failed, "a request is pending").ConfigureAwait(false);
            return;
        }
        var worn = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Equipment);
        if (worn.Any(item => item.ItemUid == BagItemIds.Stored(request.MainWireUid) || item.ItemUid == BagItemIds.Stored(request.SubWireUid)))
        {
            await RefuseAsync(player, EquipMergeRules.Equipped, "an item is worn").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        Item? InBag(ulong wireUid) => wireUid == 0 ? null : bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(wireUid));
        var main = InBag(request.MainWireUid);
        var sub = InBag(request.SubWireUid);
        if (main is null || sub is null || main.ItemUid == sub.ItemUid
            || _catalog.GetById(main.ItemIndex) is not { } mainDefinition || _catalog.GetById(sub.ItemIndex) is not { } subDefinition)
        {
            await RefuseAsync(player, EquipMergeRules.NotHeld, $"main 0x{request.MainWireUid:X} or sub 0x{request.SubWireUid:X} not in the bag").ConfigureAwait(false);
            return;
        }
        var code = EquipMergeRules.Check(mainDefinition, subDefinition);
        if (code != EquipMergeRules.Ok || EquipMergeRules.ModeOf(mainDefinition.RefineIndex) is not { } mode)
        {
            await RefuseAsync(player, code, $"item {main.ItemIndex} / {sub.ItemIndex} (type {mainDefinition.Type}, refine index {mainDefinition.RefineIndex})").ConfigureAwait(false);
            return;
        }
        var support = InBag(request.SupportWireUid);
        if (request.SupportWireUid != 0 && (support is null || !_supports.ContainsKey(support.ItemIndex) || support.ItemIndex == EquipMergeRules.BaseSupportRow))
        {
            await RefuseAsync(player, EquipMergeRules.BadSupport, $"support 0x{request.SupportWireUid:X} is not a fusion support item").ConfigureAwait(false);
            return;
        }
        var cost = EquipMergeRules.Cost(mainDefinition);
        if (stats.Money < cost)
        {
            await RefuseAsync(player, EquipMergeRules.Failed, $"fee {cost} above money {stats.Money}").ConfigureAwait(false);
            return;
        }
        var chance = EquipMergeRules.Chance(_supports, support?.ItemIndex);
        bool success;
        lock (_rngGate) success = EquipMergeRules.Succeeds(chance, _rng.Next(EquipMergeRules.RollRange));
        var mainProperties = Effective(main, mainDefinition);
        var properties = success ? EquipMergeRules.MergeStats(mainProperties, Effective(sub, subDefinition), mode, Between) : mainProperties;
        properties = WithRefineBonus(properties, main, mainDefinition);
        var mixSlot = success ? EquipMergeRules.MergeMixSlots(main.ItemSlot, sub.ItemSlot, mode, Between) : main.ItemSlot;
        var rows = new List<EquipUpgradeItem>(4)
        {
            EquipUpgradeOperations.Delete(main),
            EquipUpgradeOperations.Delete(sub),
            new(EquipUpgradeAction.Insert, 0, main.ItemIndex, main.ItemClass, 1, mixSlot, main.RefineType, main.RefineLevel, main.RefineState),
        };
        if (support is not null) rows.Add(EquipUpgradeOperations.Take(support, 1));
        player.UpgradeBusy = true;
        EquipStatusUpgradeResult result;
        try
        {
            result = await _master.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, (uint)cost, _bagCapacity, rows), default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (!result.Succeeded || result.Items.FirstOrDefault(row => row.Action == EquipUpgradeAction.Insert) is not { } merged)
        {
            await RefuseAsync(player, EquipMergeRules.Failed, $"store refused with code {result.Error}").ConfigureAwait(false);
            return;
        }
        _properties.Replace(merged.ItemUid, properties);
        stats.GrantMoney((int)((long)result.RemainMoney - stats.Money), tPoseidon.Core.Game.Stats.StatChangeSource.EquipUpgrade);
        player.TimedItems.Forget(main.ItemUid);
        player.TimedItems.Forget(sub.ItemUid);
        await SendAsync(player, (Opcode)EquipMergeFrames.Merged, EquipMergeFrames.Result(success, result.RemainMoney,
            result.Items.Select(EquipUpgradeOperations.ToBag).ToArray())).ConfigureAwait(false);
        Log(player, $"fusion of item {main.ItemIndex} {(success ? "succeeded" : "failed")} (chance {chance} %, mode {mode}, fee {cost}, support {(support is null ? "none" : support.ItemIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))}): {properties.Count} properties, mix slot {mixSlot}");
    }

    private List<ItemProperty> Effective(Item item, ItemParamEntry definition)
    {
        var stored = _properties.ForItem(item.ItemUid);
        return stored.Concat(_rules().Defaults(definition).Where(fallback => !stored.Any(property => property.Type == fallback.Type && property.Id == fallback.Id))).ToList();
    }

    private IReadOnlyList<ItemProperty> WithRefineBonus(IReadOnlyList<ItemProperty> properties, Item main, ItemParamEntry definition)
    {
        if (main.RefineLevel == 0 || RefineRules.PropertyIdFor(definition.RefineType) is not int propertyId
            || !_refines().LevelRows.TryGetValue(definition.RefineIndex, out var levelRow) || main.RefineLevel > levelRow.Accumulation.Count) return properties;
        var baseValue = properties.FirstOrDefault(property => property.Type == ItemPropertyTypes.Stat && property.Id == propertyId).Value;
        return properties.Where(property => !(property.Type == ItemPropertyTypes.RefineBonus && property.Id == propertyId))
            .Append(new ItemProperty(ItemPropertyTypes.RefineBonus, propertyId, RefineRules.Bonus(baseValue, levelRow.Accumulation[main.RefineLevel - 1]), 0))
            .ToList();
    }

    private int Between(int low, int high)
    {
        lock (_rngGate) return _rng.Next(low, high);
    }

    private async Task RefuseAsync(InGameSessionState player, byte code, string reason)
    {
        Log(player, $"fusion refused with code {code}: {reason}");
        await SendAsync(player, (Opcode)EquipMergeFrames.Refused, EquipMergeFrames.Code(code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [fusion] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
