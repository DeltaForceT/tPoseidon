using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Soul;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class SoulController
{
    public const int ChargeWindow = 0;
    public const int ReinforceWindow = 1;
    public const int CraftWindow = 2;
    public static readonly TimeSpan DrainCheckInterval = TimeSpan.FromMinutes(1);

    private readonly MasterSoul _master;
    private readonly MasterEquipStatusUpgrade _upgrades;
    private readonly Func<SoulSystemTables> _tables;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public SoulController(
        MasterSoul master,
        MasterEquipStatusUpgrade upgrades,
        Func<SoulSystemTables> tables,
        IItemStore items,
        IItemParamLookup catalog,
        IItemPropertyStore properties,
        int bagCapacity,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task SyncAsync(InGameSessionState player, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return;
        if (player.Soul is null)
        {
            if (await _master.LoadAsync(character.CharacterId, ct).ConfigureAwait(false) is not { } account)
            {
                Log(player, "soul account not loaded");
                return;
            }
            player.Soul = new SoulHold(account.Soul, account.Guardians);
            await CatchUpAsync(player, false).ConfigureAwait(false);
        }
        await SendStatusAsync(player, null).ConfigureAwait(false);
    }

    public async Task GuardianChangedAsync(InGameSessionState player, ulong previousGuardianUid, ulong guardianUid)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Soul is not { } hold || player.Character is not { } character || previousGuardianUid == guardianUid) return;
        var now = DateTimeOffset.UtcNow;
        var saves = new List<GuardianSoulRow>(2);
        lock (hold.Gate)
        {
            if (previousGuardianUid != 0)
            {
                var cleared = SoulRules.WithoutSource(hold.Guardian(previousGuardianUid) ?? SoulRules.Fresh(previousGuardianUid, now)) with { Gauge = 0 };
                hold.Set(cleared);
                saves.Add(cleared);
            }
            var started = (hold.Guardian(guardianUid) ?? SoulRules.Fresh(guardianUid, now)) with { LastTick = now };
            hold.Set(started);
            saves.Add(started);
        }
        foreach (var row in saves) await _master.SaveGuardianAsync(character.CharacterId, row, default).ConfigureAwait(false);
        Log(player, $"guardian {previousGuardianUid} -> {guardianUid}: source cancelled, gauge reset");
        await SendStatusAsync(player, null).ConfigureAwait(false);
    }

    public async Task<int> OpenAsync(InGameSessionState player, int window)
    {
        ArgumentNullException.ThrowIfNull(player);
        var (kind, opcode) = window switch
        {
            ChargeWindow => (UpgradeWindow.SoulCharge, Opcode.S2C_SOUL_CHARGE_OPENED),
            ReinforceWindow => (UpgradeWindow.SoulReinforce, Opcode.S2C_SOUL_REINFORCE_OPENED),
            _ => (UpgradeWindow.SoulCraft, Opcode.S2C_SOUL_CRAFT_OPENED),
        };
        player.Upgrade = kind;
        player.UpgradeBusy = false;
        await SendAsync(player, opcode, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, $"{kind} window opened");
        return NpcScript.Silent;
    }

    public async Task CloseAsync(InGameSessionState player, int window)
    {
        ArgumentNullException.ThrowIfNull(player);
        var (kind, opcode) = window switch
        {
            ChargeWindow => (UpgradeWindow.SoulCharge, Opcode.S2C_SOUL_CHARGE_CLOSED),
            ReinforceWindow => (UpgradeWindow.SoulReinforce, Opcode.S2C_SOUL_REINFORCE_CLOSED),
            _ => (UpgradeWindow.SoulCraft, Opcode.S2C_SOUL_CRAFT_CLOSED),
        };
        if (player.Upgrade == kind) player.Upgrade = UpgradeWindow.None;
        await SendAsync(player, opcode, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, $"{kind} window closed");
    }

    public async Task RegisterAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!SoulFrames.TryReadRegister(payload.Span, out var wireUid, out var count))
        {
            await NotifyAsync(player, SoulRules.NotifyBadItem, $"register request of {payload.Length} bytes").ConfigureAwait(false);
            return;
        }
        if (player.Soul is not { } hold || player.Character is not { } character || player.Guardians?.Equipped is not { } guardian)
        {
            await NotifyAsync(player, SoulRules.NotifyBadGuardian, "no equipped guardian").ConfigureAwait(false);
            return;
        }
        var tables = _tables();
        var source = InBag(character.CharacterId, wireUid);
        var isSource = source is not null && SoulRules.GaugeUpOf(tables.Sources, guardian.GuardianType, source.ItemIndex) is not null;
        GuardianSoulRow? saved = null;
        byte code;
        lock (hold.Gate)
        {
            var row = hold.Guardian(guardian.GuardianUid) ?? SoulRules.Fresh(guardian.GuardianUid, DateTimeOffset.UtcNow);
            code = SoulRules.CheckRegister(true, source is not null, isSource, count, source?.Stack ?? 0, SoulRules.HasSource(row));
            if (code == SoulRules.Ok && source is not null)
            {
                saved = row with { SourceItemUid = source.ItemUid, SourceCount = count };
                hold.Set(saved);
            }
        }
        if (saved is null)
        {
            await NotifyAsync(player, code, $"source 0x{wireUid:X} x{count} for guardian {guardian.GuardianType}").ConfigureAwait(false);
            return;
        }
        await _master.SaveGuardianAsync(character.CharacterId, saved, default).ConfigureAwait(false);
        Log(player, $"source item {source!.ItemIndex} x{count} registered on guardian {guardian.GuardianUid}");
        await SendStatusAsync(player, null).ConfigureAwait(false);
    }

    public async Task UnregisterAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Soul is not { } hold || player.Character is not { } character || player.Guardians?.Equipped is not { } guardian)
        {
            await NotifyAsync(player, SoulRules.NotifyBadGuardian, "no equipped guardian").ConfigureAwait(false);
            return;
        }
        GuardianSoulRow cleared;
        lock (hold.Gate)
        {
            cleared = SoulRules.WithoutSource(hold.Guardian(guardian.GuardianUid) ?? SoulRules.Fresh(guardian.GuardianUid, DateTimeOffset.UtcNow));
            hold.Set(cleared);
        }
        await _master.SaveGuardianAsync(character.CharacterId, cleared, default).ConfigureAwait(false);
        Log(player, $"source unregistered from guardian {guardian.GuardianUid}");
        await SendStatusAsync(player, null).ConfigureAwait(false);
    }

    public async Task TakePotionsAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!SoulFrames.TryReadTake(payload.Span, out var count))
        {
            await NotifyAsync(player, SoulRules.NotifyBadPotionCount, $"take request of {payload.Length} bytes").ConfigureAwait(false);
            return;
        }
        if (player.Soul is not { } hold || player.Character is not { } character || player.Guardians?.Equipped is not { } guardian || player.Stats is not { } stats)
        {
            await NotifyAsync(player, SoulRules.NotifyBadGuardian, "no equipped guardian").ConfigureAwait(false);
            return;
        }
        if (_tables().Info is not { } info || _catalog.GetById(info.PotionItemId) is not { } potion)
        {
            await NotifyAsync(player, SoulRules.NotifyBadPotionInfo, "no soul potion row").ConfigureAwait(false);
            return;
        }
        int ready;
        lock (hold.Gate)
        {
            if (hold.Busy)
            {
                ready = -1;
            }
            else
            {
                ready = hold.Guardian(guardian.GuardianUid)?.PotionsReady ?? 0;
                if (SoulRules.CheckTakePotions(count, ready) == SoulRules.Ok) hold.Busy = true;
            }
        }
        if (ready < 0)
        {
            await NotifyAsync(player, SoulRules.NotifyRetry, "a soul request is pending").ConfigureAwait(false);
            return;
        }
        if (SoulRules.CheckTakePotions(count, ready) is var takeCode and not SoulRules.Ok)
        {
            await NotifyAsync(player, takeCode, $"{count} potions asked, {ready} ready").ConfigureAwait(false);
            return;
        }
        try
        {
            var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
            var stack = potion.IsStackable ? bag.FirstOrDefault(held => held.ItemIndex == potion.Id) : null;
            if (stack is null && bag.Count >= _bagCapacity)
            {
                await NotifyAsync(player, SoulRules.NotifyBagFull, $"{bag.Count} items in the bag").ConfigureAwait(false);
                return;
            }
            var weight = bag.Sum(held => (_catalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits) + potion.Weight * count;
            if (weight > player.PlayerMaxWt)
            {
                await NotifyAsync(player, SoulRules.NotifyOverWeight, $"weight {weight} over {player.PlayerMaxWt}").ConfigureAwait(false);
                return;
            }
            var row = stack is not null
                ? EquipUpgradeOperations.Update(stack, (ushort)Math.Min(stack.Stack + count, ushort.MaxValue), stack.ItemSlot)
                : EquipUpgradeOperations.Insert(potion.Id, potion.ObjectClass, count, 0);
            var result = await _upgrades.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, 0, _bagCapacity, [row]), default).ConfigureAwait(false);
            if (!result.Succeeded || result.Items.Count != 1)
            {
                await NotifyAsync(player, result.Error == EquipStatusUpgradeRules.BagFull ? SoulRules.NotifyBagFull : SoulRules.NotifyRetry,
                    $"store refused the potions with code {result.Error}").ConfigureAwait(false);
                return;
            }
            stats.GrantMoney(result.RemainMoney - stats.Money, tPoseidon.Core.Game.Stats.StatChangeSource.EquipUpgrade);
            GuardianSoulRow saved;
            lock (hold.Gate)
            {
                var current = hold.Guardian(guardian.GuardianUid) ?? SoulRules.Fresh(guardian.GuardianUid, DateTimeOffset.UtcNow);
                saved = current with { PotionsReady = Math.Max(0, current.PotionsReady - count) };
                hold.Set(saved);
            }
            await _master.SaveGuardianAsync(character.CharacterId, saved, default).ConfigureAwait(false);
            Log(player, $"{count} soul potions taken, {saved.PotionsReady} left");
            await SendStatusAsync(player, EquipUpgradeOperations.ToBag(result.Items[0])).ConfigureAwait(false);
        }
        finally
        {
            lock (hold.Gate) hold.Busy = false;
        }
    }

    public async Task<bool> UsePotionAsync(InGameSessionState player, Item potion, ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(potion);
        ArgumentNullException.ThrowIfNull(definition);
        if (player.Soul is not { } hold || player.Character is not { } character) return false;
        if (player.Guardians is not { Awakened: true })
        {
            Log(player, $"soul potion {potion.ItemIndex} refused: not awakened");
            await SendAsync(player, Opcode.S2C_SOUL_POTION_REFUSED, SoulFrames.Code(SoulRules.PotionCannotUse)).ConfigureAwait(false);
            return false;
        }
        BagOperation operation;
        if (potion.Stack > 1)
        {
            var left = _items.AdjustStack(potion.ItemUid, -1);
            operation = new BagOperation(QuestResultFrames.StackChanged, potion.ItemClass, (uint)potion.ItemIndex, BagItemIds.OnWire(potion.ItemUid), left);
        }
        else
        {
            _items.Delete(potion.ItemUid);
            operation = new BagOperation(QuestResultFrames.Removed, potion.ItemClass, (uint)potion.ItemIndex, BagItemIds.OnWire(potion.ItemUid), 0);
        }
        uint soul;
        lock (hold.Gate)
        {
            hold.Soul = (uint)Math.Min(uint.MaxValue, (long)hold.Soul + Math.Max(0, definition.Value));
            soul = hold.Soul;
        }
        await _master.SaveSoulAsync(character.CharacterId, soul, default).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_SOUL_POTION_USED, SoulFrames.PotionUsed(soul, [operation])).ConfigureAwait(false);
        Log(player, $"soul potion {potion.ItemIndex} used: soul +{definition.Value} = {soul}");
        return true;
    }

    public async Task ChargeAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!SoulFrames.TryReadCharge(payload.Span, out var wireUid, out var amount) || player.Soul is not { } hold || player.Character is not { } character)
        {
            await SendAsync(player, Opcode.S2C_SOUL_CHARGE_REFUSED, SoulFrames.Code(SoulRules.ChargeState)).ConfigureAwait(false);
            return;
        }
        var item = InBag(character.CharacterId, wireUid);
        var soulItem = item is not null && _tables().Equipment.ContainsKey(item.ItemIndex);
        var stored = item is null ? Array.Empty<ItemProperty>() : _properties.ForItem(item.ItemUid);
        SoulCharge charge = default;
        uint soul;
        byte code;
        lock (hold.Gate)
        {
            code = SoulRules.CheckCharge(player.Upgrade == UpgradeWindow.SoulCharge && !hold.Busy, soulItem, amount, hold.Soul);
            if (code == SoulRules.Ok)
            {
                charge = SoulRules.Charge(SoulRules.PropertyValue(stored, SoulRules.LevelType, SoulRules.LevelId), SoulRules.PropertyValue(stored, SoulRules.RemainType, SoulRules.RemainId), amount);
                hold.Soul -= (uint)charge.Added;
            }
            soul = hold.Soul;
        }
        if (code != SoulRules.Ok || item is null)
        {
            Log(player, $"charge refused with code {code}: item 0x{wireUid:X}, amount {amount}, soul {soul}");
            await SendAsync(player, Opcode.S2C_SOUL_CHARGE_REFUSED, SoulFrames.Code(code)).ConfigureAwait(false);
            return;
        }
        var remain = new ItemProperty(SoulRules.RemainType, SoulRules.RemainId, charge.Remaining, 0);
        _properties.Replace(item.ItemUid, SoulRules.WithProperty(stored, remain.Type, remain.Id, remain.Value));
        await _master.SaveSoulAsync(character.CharacterId, soul, default).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_SOUL_CHARGED, SoulFrames.Charged(soul, [new SoulItemInstance(BagItemIds.OnWire(item.ItemUid), (uint)item.ItemIndex, remain)])).ConfigureAwait(false);
        Log(player, $"item {item.ItemIndex} charged with {charge.Added} soul (remaining {charge.Remaining}), soul left {soul}");
    }

    public async Task CraftAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!SoulFrames.TryReadCraft(payload.Span, out var resultItemId, out var materialUids) || player.Soul is not { } hold || player.Character is not { } character || player.Stats is not { } stats)
        {
            await SendAsync(player, Opcode.S2C_SOUL_CRAFT_REFUSED, SoulFrames.Code(SoulRules.WindowState)).ConfigureAwait(false);
            return;
        }
        var recipe = _tables().CraftsByResult.TryGetValue((int)resultItemId, out var found) ? found : null;
        var offered = Offered(character.CharacterId, materialUids);
        var hasMaterials = recipe is not null && SoulRules.HasMaterials(recipe.Materials, offered);
        var result = recipe is null ? null : _catalog.GetById(recipe.ResultItemId);
        if (!TryBeginSoulSpend(player, hold, UpgradeWindow.SoulCraft,
            soul => SoulRules.CheckCraft(true, result is null ? null : recipe, hasMaterials, soul), out var code))
        {
            Log(player, $"craft of item {resultItemId} refused with code {code}");
            await SendAsync(player, Opcode.S2C_SOUL_CRAFT_REFUSED, SoulFrames.Code(code)).ConfigureAwait(false);
            return;
        }
        try
        {
            var rows = SoulRules.Consumption(recipe!.Materials, offered).Select(take => EquipUpgradeOperations.Take(take.Item, (ushort)take.Count)).ToList();
            rows.Add(EquipUpgradeOperations.Insert(result!.Id, result.ObjectClass, (ushort)Math.Max(1, recipe.ResultCount),
                result.ObjectClass == ItemParamEntry.RareClass ? (byte)result.CompoundSlot : (byte)0));
            var applied = await _upgrades.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, 0, _bagCapacity, rows), default).ConfigureAwait(false);
            if (!applied.Succeeded)
            {
                Log(player, $"craft of item {resultItemId}: store refused with code {applied.Error}");
                await SendAsync(player, Opcode.S2C_SOUL_CRAFT_REFUSED, SoulFrames.Code(SoulRules.Unknown)).ConfigureAwait(false);
                return;
            }
            stats.GrantMoney(applied.RemainMoney - stats.Money, tPoseidon.Core.Game.Stats.StatChangeSource.EquipUpgrade);
            var soul = Spend(hold, (uint)recipe.Fee);
            await _master.SaveSoulAsync(character.CharacterId, soul, default).ConfigureAwait(false);
            foreach (var removed in applied.Items.Where(row => row.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(removed.ItemUid);
            await SendAsync(player, Opcode.S2C_SOUL_CRAFTED, SoulFrames.Crafted(true, soul, applied.Items.Select(EquipUpgradeOperations.ToBag).ToList())).ConfigureAwait(false);
            Log(player, $"crafted item {result.Id} x{recipe.ResultCount} for {recipe.Fee} soul, soul left {soul}");
        }
        finally
        {
            lock (hold.Gate) hold.Busy = false;
        }
    }

    public async Task ReinforceAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!SoulFrames.TryReadReinforce(payload.Span, out var targetUid, out var materialUids) || player.Soul is not { } hold || player.Character is not { } character || player.Stats is not { } stats)
        {
            await SendAsync(player, Opcode.S2C_SOUL_REINFORCE_REFUSED, SoulFrames.Code(SoulRules.WindowState)).ConfigureAwait(false);
            return;
        }
        var target = InBag(character.CharacterId, targetUid);
        var definition = target is null ? null : _catalog.GetById(target.ItemIndex);
        var soulItem = target is not null && definition is not null && _tables().Equipment.ContainsKey(target.ItemIndex);
        var stored = target is null ? Array.Empty<ItemProperty>() : _properties.ForItem(target.ItemUid);
        var level = SoulRules.PropertyValue(stored, SoulRules.LevelType, SoulRules.LevelId);
        var row = definition is null ? null : SoulRules.IntensifyRow(_tables().Intensify, level + 1, definition.MinLevel);
        var offered = Offered(character.CharacterId, materialUids.Where(uid => uid != targetUid).ToArray());
        var hasMaterials = row is not null && SoulRules.HasMaterials(row.Materials, offered);
        if (!TryBeginSoulSpend(player, hold, UpgradeWindow.SoulReinforce,
            soul => SoulRules.CheckIntensify(true, soulItem, level, row, hasMaterials, soul), out var code))
        {
            Log(player, $"reinforce of 0x{targetUid:X} (level {level}) refused with code {code}");
            await SendAsync(player, Opcode.S2C_SOUL_REINFORCE_REFUSED, SoulFrames.Code(code)).ConfigureAwait(false);
            return;
        }
        try
        {
            var rows = SoulRules.Consumption(row!.Materials, offered).Select(take => EquipUpgradeOperations.Take(take.Item, (ushort)take.Count)).ToList();
            var applied = await _upgrades.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, 0, _bagCapacity, rows), default).ConfigureAwait(false);
            if (!applied.Succeeded)
            {
                Log(player, $"reinforce of 0x{targetUid:X}: store refused with code {applied.Error}");
                await SendAsync(player, Opcode.S2C_SOUL_REINFORCE_REFUSED, SoulFrames.Code(SoulRules.Unknown)).ConfigureAwait(false);
                return;
            }
            stats.GrantMoney(applied.RemainMoney - stats.Money, tPoseidon.Core.Game.Stats.StatChangeSource.EquipUpgrade);
            var next = (short)(level + 1);
            var levelProperty = new ItemProperty(SoulRules.LevelType, SoulRules.LevelId, next, 0);
            _properties.Replace(target!.ItemUid, SoulRules.WithProperty(stored, levelProperty.Type, levelProperty.Id, next));
            var soul = Spend(hold, (uint)row.Cost);
            await _master.SaveSoulAsync(character.CharacterId, soul, default).ConfigureAwait(false);
            foreach (var removed in applied.Items.Where(item => item.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(removed.ItemUid);
            await SendAsync(player, Opcode.S2C_SOUL_REINFORCED, SoulFrames.Reinforced(soul,
                applied.Items.Select(EquipUpgradeOperations.ToBag).ToList(),
                [new SoulItemInstance(BagItemIds.OnWire(target.ItemUid), (uint)target.ItemIndex, levelProperty)])).ConfigureAwait(false);
            Log(player, $"item {target.ItemIndex} soul level {level} -> {next} for {row.Cost} soul, soul left {soul}");
        }
        finally
        {
            lock (hold.Gate) hold.Busy = false;
        }
    }

    public async Task TickAsync(InGameSessionState player, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Soul is null || player.Character is null) return;
        if (_tables().Info is { } info && player.Guardians?.Equipped is { } guardian
            && player.Soul.Guardian(guardian.GuardianUid) is { } row && SoulRules.TickDue(row, info, now))
        {
            await CatchUpAsync(player, true).ConfigureAwait(false);
        }
        await DrainAsync(player, now).ConfigureAwait(false);
    }

    private async Task CatchUpAsync(InGameSessionState player, bool online)
    {
        if (player.Soul is not { } hold || player.Character is not { } character || player.Guardians?.Equipped is not { } guardian) return;
        if (_tables() is not { Info: { } info } tables || info.GaugeMinutes <= 0) return;
        var now = DateTimeOffset.UtcNow;
        GuardianSoulRow saved;
        Item? source;
        var consumed = 0;
        var made = 0;
        var ticks = 0;
        bool created;
        lock (hold.Gate)
        {
            if (hold.Busy) return;
            var known = hold.Guardian(guardian.GuardianUid);
            created = known is null;
            var row = known ?? SoulRules.Fresh(guardian.GuardianUid, now);
            source = row.SourceItemUid == 0 ? null : _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory).FirstOrDefault(item => item.ItemUid == row.SourceItemUid);
            var gaugeUp = source is null ? null : SoulRules.GaugeUpOf(tables.Sources, guardian.GuardianType, source.ItemIndex);
            var stack = source?.Stack ?? 0;
            var perGauge = SoulRules.PotionsPerGauge(tables.PotionCounts, guardian.Level, online);
            while (SoulRules.TickDue(row, info, now))
            {
                var tick = SoulRules.Tick(row, info, gaugeUp, stack - consumed, perGauge);
                consumed += tick.SourcesConsumed;
                made += tick.PotionsMade;
                row = tick.State;
                ticks++;
            }
            saved = row;
            hold.Set(saved);
        }
        BagOperation? sourceChange = null;
        if (consumed > 0 && source is not null)
        {
            if (source.Stack > consumed)
            {
                var left = _items.AdjustStack(source.ItemUid, -consumed);
                sourceChange = new BagOperation(QuestResultFrames.StackChanged, source.ItemClass, (uint)source.ItemIndex, BagItemIds.OnWire(source.ItemUid), left);
            }
            else
            {
                _items.Delete(source.ItemUid);
                sourceChange = new BagOperation(QuestResultFrames.Removed, source.ItemClass, (uint)source.ItemIndex, BagItemIds.OnWire(source.ItemUid), 0);
            }
        }
        if (ticks == 0 && !created) return;
        await _master.SaveGuardianAsync(character.CharacterId, saved, default).ConfigureAwait(false);
        if (sourceChange is { } change) await SendAsync(player, Opcode.S2C_ITEM_OPERATION, QuestResultFrames.ItemOperation(change)).ConfigureAwait(false);
        if (ticks > 0)
        {
            Log(player, $"{ticks} gauge ticks ({(online ? "online" : "offline")}): {consumed} sources used, {made} potions made, gauge {saved.Gauge}, {saved.PotionsReady} ready");
            if (online) await SendStatusAsync(player, null).ConfigureAwait(false);
        }
    }

    private async Task DrainAsync(InGameSessionState player, DateTimeOffset now)
    {
        if (player.Soul is not { } hold || player.Character is not { } character) return;
        var tables = _tables();
        lock (hold.Gate)
        {
            if (!tables.TartarosMaps.Contains(player.MapId))
            {
                hold.ForgetDrains([]);
                hold.NextDrainCheck = null;
                return;
            }
            if (hold.NextDrainCheck is { } check && check > now) return;
            hold.NextDrainCheck = now.Add(DrainCheckInterval);
        }
        var worn = EquipmentRules.WornBy(_items, _catalog, character.CharacterId)
            .Where(held => tables.Equipment.ContainsKey(held.Item.ItemIndex))
            .ToList();
        var drained = new List<SoulItemInstance>();
        foreach (var held in worn)
        {
            var entry = tables.Equipment[held.Item.ItemIndex];
            bool due;
            lock (hold.Gate)
            {
                var next = hold.DrainDue(held.Item.ItemUid);
                due = next is { } at && at <= now;
                if (next is null || due) hold.SetDrainDue(held.Item.ItemUid, now.AddMinutes(Math.Max(1, entry.DecreaseMinutes)));
            }
            if (!due) continue;
            var stored = _properties.ForItem(held.Item.ItemUid);
            var remaining = SoulRules.PropertyValue(stored, SoulRules.RemainType, SoulRules.RemainId);
            if (remaining <= 0) continue;
            var left = SoulRules.Drain(remaining, entry.DecreaseSoul);
            _properties.Replace(held.Item.ItemUid, SoulRules.WithProperty(stored, SoulRules.RemainType, SoulRules.RemainId, left));
            drained.Add(new SoulItemInstance(BagItemIds.OnWire(held.Item.ItemUid), (uint)held.Item.ItemIndex, new ItemProperty(SoulRules.RemainType, SoulRules.RemainId, left, 0)));
        }
        lock (hold.Gate) hold.ForgetDrains(worn.Select(held => held.Item.ItemUid));
        if (drained.Count == 0) return;
        await SendAsync(player, Opcode.S2C_SOUL_ITEM_PROPERTIES, SoulFrames.PropertyUpdates(drained)).ConfigureAwait(false);
        Log(player, $"soul drained on {drained.Count} worn items on map {player.MapId}");
    }

    private static bool TryBeginSoulSpend(InGameSessionState player, SoulHold hold, UpgradeWindow window, Func<uint, byte> check, out byte code)
    {
        lock (hold.Gate)
        {
            if (player.Upgrade != window || hold.Busy)
            {
                code = SoulRules.WindowState;
                return false;
            }
            code = check(hold.Soul);
            if (code != SoulRules.Ok) return false;
            hold.Busy = true;
            return true;
        }
    }

    private static uint Spend(SoulHold hold, uint cost)
    {
        lock (hold.Gate)
        {
            hold.Soul = hold.Soul >= cost ? hold.Soul - cost : 0;
            return hold.Soul;
        }
    }

    private Item? InBag(ulong characterId, ulong wireUid) =>
        wireUid == 0 ? null : _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(wireUid));

    private List<Item> Offered(ulong characterId, IReadOnlyList<ulong> wireUids)
    {
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        return wireUids.Where(uid => uid != 0).Select(BagItemIds.Stored).Distinct()
            .Select(uid => bag.FirstOrDefault(item => item.ItemUid == uid))
            .OfType<Item>()
            .ToList();
    }

    private async Task SendStatusAsync(InGameSessionState player, BagOperation? potion)
    {
        if (player.Soul is not { } hold) return;
        var tables = _tables();
        SoulManageStatus status;
        lock (hold.Gate)
        {
            var guardian = player.Guardians?.Equipped;
            var row = guardian is null ? null : hold.Guardian(guardian.GuardianUid);
            status = new SoulManageStatus(
                hold.Soul,
                guardian?.GuardianUid ?? 0,
                (ushort)Math.Clamp(row?.Gauge ?? 0, 0, ushort.MaxValue),
                (ushort)Math.Clamp(tables.Info?.GaugeMax ?? 0, 0, ushort.MaxValue),
                row is not null && SoulRules.HasSource(row) ? BagItemIds.OnWire(row.SourceItemUid) : 0,
                (ushort)Math.Clamp(row is not null && SoulRules.HasSource(row) ? row.SourceCount : 0, 0, ushort.MaxValue),
                (uint)(tables.Info?.PotionItemId ?? 0),
                (ushort)Math.Clamp(row?.PotionsReady ?? 0, 0, ushort.MaxValue),
                0);
        }
        await SendAsync(player, Opcode.S2C_SOUL_MANAGE_STATUS, SoulFrames.ManageStatus(status, potion)).ConfigureAwait(false);
    }

    private async Task NotifyAsync(InGameSessionState player, byte code, string reason)
    {
        Log(player, $"refused with code {code}: {reason}");
        await SendAsync(player, Opcode.S2C_SOUL_MANAGE_REFUSED, SoulFrames.Code(code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [soul] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
