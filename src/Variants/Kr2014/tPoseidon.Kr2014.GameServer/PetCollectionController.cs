using System.Collections.Concurrent;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Drilling;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class PetCollectionRun
{
    public PetCollectionRun(IEnumerable<PetCollectedItem> bag)
    {
        Bag = bag.ToList();
    }

    public SemaphoreSlim Gate { get; } = new(1, 1);
    public PetCollectionState State { get; set; } = PetCollectionRules.AtLogin;
    public List<PetCollectedItem> Bag { get; }
    public PetCollectionPlan? Plan { get; set; }
    public ulong PetUid { get; set; }
    public ulong FoodUid { get; set; }
    public int FoodLeft { get; set; }
    public DateTimeOffset NextTickAt { get; set; }
}

internal sealed class PetCollectionController
{
    private readonly Func<PetDigTables> _tables;
    private readonly Func<DrillTables> _drillTables;
    private readonly IPetCollectionStore _store;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly ItemManager _itemManager;
    private readonly string _logTag;
    private readonly ConcurrentDictionary<ulong, PetCollectionRun> _runs = new();
    private readonly Dictionary<ushort, DrillLoot?> _loot = new();
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public PetCollectionController(
        Func<PetDigTables> tables,
        Func<DrillTables> drillTables,
        IPetCollectionStore store,
        IItemStore items,
        IItemParamLookup catalog,
        ItemManager itemManager,
        string logTag)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _drillTables = drillTables ?? throw new ArgumentNullException(nameof(drillTables));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public bool IsCollecting(InGameSessionState player) =>
        player.Character is { } character && _runs.TryGetValue(character.CharacterId, out var run) && run.State == PetCollectionState.Collecting;

    public byte[] ConnectPhaseBag(ulong characterId)
    {
        var run = new PetCollectionRun(_store.Load(characterId));
        _runs[characterId] = run;
        return PetCollectionFrames.Bag(run.Bag.Select(Entry).ToList());
    }

    public async Task LoginAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return;
        var run = RunOf(character.CharacterId);
        await SendAsync(player, Opcode.S2C_PET_COLLECTION_BAG, PetCollectionFrames.Bag(run.Bag.Select(Entry).ToList())).ConfigureAwait(false);
        Log(player, $"pet bag sent: {run.Bag.Count} items");
    }

    public async Task StartAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return;
        if (!PetCollectionFrames.TryReadStart(payload.Span, out var frame))
        {
            Log(player, $"start request of {payload.Length} bytes not read");
            return;
        }
        var run = RunOf(character.CharacterId);
        await run.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var petUid = BagItemIds.Stored(frame.PetWireUid);
            var foodUid = BagItemIds.Stored(frame.FoodWireUid);
            var request = new PetCollectionStartRequest(
                run.State,
                player.Stats?.Level ?? character.Level,
                player.IsDead,
                Slot(character.CharacterId, frame.PetWireUid, now),
                Slot(character.CharacterId, frame.FoodWireUid, now),
                frame.FoodCount,
                player.MapId,
                run.Bag.Count);
            var code = PetCollectionRules.CheckStart(request, _tables(), out var plan);
            if (code != PetCollectionRules.Ok || plan is null)
            {
                Log(player, $"start refused with code {code}: pet 0x{frame.PetWireUid:X} food 0x{frame.FoodWireUid:X} x{frame.FoodCount} on map {player.MapId}");
                await SendAsync(player, Opcode.S2C_PET_COLLECTION_START_REFUSED, PetCollectionFrames.Code(code)).ConfigureAwait(false);
                return;
            }
            run.State = PetCollectionState.Collecting;
            run.Plan = plan;
            run.PetUid = petUid;
            run.FoodUid = foodUid;
            run.FoodLeft = frame.FoodCount;
            run.NextTickAt = now + plan.TickPeriod;
            await SendAsync(player, Opcode.S2C_PET_COLLECTION_STARTED, Array.Empty<byte>()).ConfigureAwait(false);
            Log(player, $"pet {plan.PetItemId} collects with {frame.FoodCount} x {plan.Food.ItemId} every {plan.Food.SpeedSeconds} s on map {player.MapId}");
        }
        finally
        {
            run.Gate.Release();
        }
    }

    public async Task StopAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return;
        var run = RunOf(character.CharacterId);
        await run.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!PetCollectionRules.CanStopFrom(run.State))
            {
                Log(player, $"stop refused in state {run.State}");
                await SendAsync(player, Opcode.S2C_PET_COLLECTION_STOP_REFUSED, PetCollectionFrames.Code(PetCollectionRules.StopRefused)).ConfigureAwait(false);
                return;
            }
            await HaltAsync(player, run, "stopped by the player").ConfigureAwait(false);
        }
        finally
        {
            run.Gate.Release();
        }
    }

    public async Task ReturnAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return;
        var run = RunOf(character.CharacterId);
        await run.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!PetCollectionRules.CanReturnFrom(run.State))
            {
                Log(player, $"return refused in state {run.State}");
                await SendAsync(player, Opcode.S2C_PET_COLLECTION_RETURN_REFUSED, PetCollectionFrames.Code(PetCollectionRules.ReturnRefused)).ConfigureAwait(false);
                return;
            }
            run.State = PetCollectionRules.AfterReturn(run.Bag.Count);
            run.Plan = null;
            run.FoodLeft = 0;
            await SendAsync(player, Opcode.S2C_PET_COLLECTION_RETURNED, Array.Empty<byte>()).ConfigureAwait(false);
            Log(player, $"pet returned holding {run.Bag.Count} items (state {run.State})");
        }
        finally
        {
            run.Gate.Release();
        }
    }

    public async Task GetAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return;
        if (!PetCollectionFrames.TryReadItem(payload.Span, out var collectedUid))
        {
            Log(player, $"get request of {payload.Length} bytes not read");
            return;
        }
        var run = RunOf(character.CharacterId);
        await run.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var entry = run.Bag.FirstOrDefault(item => item.CollectedUid == collectedUid);
            if (entry is null || _catalog.GetById(entry.ItemId) is not { } definition)
            {
                await RefuseAsync(player, Opcode.S2C_PET_COLLECTION_GET_REFUSED, PetCollectionRules.GetFailed, $"item 0x{collectedUid:X} is not in the pet bag").ConfigureAwait(false);
                return;
            }
            var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
            var stack = definition.IsStackable ? bag.FirstOrDefault(item => item.ItemIndex == entry.ItemId) : null;
            var bagWeight = bag.Sum(held => (_catalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits);
            var units = definition.ObjectClass == Item.RareClass ? 1 : entry.Count;
            if (!PickupRules.Fits(player.PlayerMaxWt, bagWeight, bag.Count, stack is not null, definition.Weight, units))
            {
                await RefuseAsync(player, Opcode.S2C_PET_COLLECTION_GET_REFUSED, PetCollectionRules.GetNoRoom, $"item {entry.ItemId} x{entry.Count}: bag weight {bagWeight} of {player.PlayerMaxWt}, {bag.Count} items").ConfigureAwait(false);
                return;
            }
            if (!_store.Remove(character.CharacterId, entry.CollectedUid))
            {
                await RefuseAsync(player, Opcode.S2C_PET_COLLECTION_GET_REFUSED, PetCollectionRules.GetFailed, $"item 0x{collectedUid:X} was not in the stored pet bag").ConfigureAwait(false);
                return;
            }
            run.Bag.Remove(entry);
            BagOperation placed;
            if (stack is not null)
            {
                var total = _items.AdjustStack(stack.ItemUid, entry.Count);
                placed = new BagOperation(QuestResultFrames.StackChanged, definition.ObjectClass, (uint)entry.ItemId, BagItemIds.OnWire(stack.ItemUid), total);
            }
            else
            {
                var minted = _itemManager.GrantRewardItem(character.CharacterId, entry.ItemId, entry.Count, itemClassOverride: definition.ObjectClass);
                placed = new BagOperation(QuestResultFrames.Created, definition.ObjectClass, (uint)entry.ItemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot);
            }
            run.State = PetCollectionRules.AfterBagChange(run.State, run.Bag.Count);
            await SendAsync(player, Opcode.S2C_PET_COLLECTION_GOT, PetCollectionFrames.Moved(Removal(entry), placed)).ConfigureAwait(false);
            Log(player, $"item {entry.ItemId} x{entry.Count} moved from the pet bag; {run.Bag.Count} left (state {run.State})");
        }
        finally
        {
            run.Gate.Release();
        }
    }

    public async Task DestroyAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return;
        if (!PetCollectionFrames.TryReadItem(payload.Span, out var collectedUid))
        {
            Log(player, $"destroy request of {payload.Length} bytes not read");
            return;
        }
        var run = RunOf(character.CharacterId);
        await run.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var entry = run.Bag.FirstOrDefault(item => item.CollectedUid == collectedUid);
            if (entry is null || !_store.Remove(character.CharacterId, entry.CollectedUid))
            {
                await RefuseAsync(player, Opcode.S2C_PET_COLLECTION_DESTROY_REFUSED, PetCollectionRules.DestroyRefused, $"item 0x{collectedUid:X} is not in the pet bag").ConfigureAwait(false);
                return;
            }
            run.Bag.Remove(entry);
            run.State = PetCollectionRules.AfterBagChange(run.State, run.Bag.Count);
            await SendAsync(player, Opcode.S2C_PET_COLLECTION_DESTROYED, PetCollectionFrames.Destroyed(Removal(entry))).ConfigureAwait(false);
            Log(player, $"item {entry.ItemId} x{entry.Count} discarded from the pet bag; {run.Bag.Count} left (state {run.State})");
        }
        finally
        {
            run.Gate.Release();
        }
    }

    public async Task TickAsync(InGameSessionState player, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character || !_runs.TryGetValue(character.CharacterId, out var run)) return;
        if (!run.Gate.Wait(0)) return;
        try
        {
            if (run.State != PetCollectionState.Collecting || run.Plan is not { } plan || now < run.NextTickAt) return;
            run.NextTickAt = now + plan.TickPeriod;
            var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
            var food = bag.FirstOrDefault(item => item.ItemUid == run.FoodUid);
            if (bag.All(item => item.ItemUid != run.PetUid) || food is null || food.ItemIndex != plan.Food.ItemId)
            {
                await HaltAsync(player, run, "the pet or its food left the bag").ConfigureAwait(false);
                return;
            }
            var tables = _tables();
            var areaCode = PetCollectionRules.CheckArea(tables, player.MapId, plan);
            if (areaCode != PetCollectionRules.Ok)
            {
                await HaltAsync(player, run, $"map {player.MapId} does not allow this pet (code {areaCode})").ConfigureAwait(false);
                return;
            }
            PetCollectionDraw? draw;
            lock (_rngGate) draw = PetCollectionRules.Draw(plan, PetCollectionRules.DepthPenalty(tables, player.MapId, _rng.Next), _rng.Next, () => GroundItem(player.MapId));
            var spent = SpendOne(food);
            run.FoodLeft--;
            if (draw is { } drawn && _catalog.GetById(drawn.ItemId) is { IsMoney: false } definition)
            {
                var collected = _store.Add(character.CharacterId, drawn.ItemId, definition.ObjectClass, (ushort)Math.Clamp(drawn.Count, 1, ushort.MaxValue));
                run.Bag.Add(collected);
                var created = new BagOperation(QuestResultFrames.Created, collected.ItemClass, (uint)collected.ItemId, collected.CollectedUid, collected.Count);
                await SendAsync(player, Opcode.S2C_PET_COLLECTION_COLLECTED, PetCollectionFrames.Collected(created, spent)).ConfigureAwait(false);
                Log(player, $"pet collected {(drawn.IsExItem ? "EX " : string.Empty)}item {drawn.ItemId} x{collected.Count}; {run.Bag.Count} of {plan.Pet.BagSize}, food left {run.FoodLeft}");
            }
            else
            {
                await SendAsync(player, Opcode.S2C_ITEM_ADDED, QuestResultFrames.ItemOperations(new[] { spent })).ConfigureAwait(false);
                Log(player, $"pet found nothing; food left {run.FoodLeft}");
            }
            if (PetCollectionRules.StopsAfterTick(run.FoodLeft, run.Bag.Count, plan.Pet))
            {
                await HaltAsync(player, run, run.FoodLeft < 1 ? "the food ran out" : "the pet bag is full").ConfigureAwait(false);
            }
        }
        finally
        {
            run.Gate.Release();
        }
    }

    private async Task HaltAsync(InGameSessionState player, PetCollectionRun run, string reason)
    {
        run.State = PetCollectionState.Stopped;
        await SendAsync(player, Opcode.S2C_PET_COLLECTION_STOPPED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, $"collection stopped: {reason}");
    }

    private PetCollectionRun RunOf(ulong characterId) =>
        _runs.GetOrAdd(characterId, id => new PetCollectionRun(_store.Load(id)));

    private PetCollectionSlot? Slot(ulong characterId, ulong wireUid, DateTimeOffset now)
    {
        if (wireUid == 0) return null;
        var stored = BagItemIds.Stored(wireUid);
        var held = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).FirstOrDefault(item => item.ItemUid == stored);
        var worn = held is null ? _items.GetForCharacterBySlotKind(characterId, SlotKind.Equipment).FirstOrDefault(item => item.ItemUid == stored) : null;
        if ((held ?? worn) is not { } item) return null;
        return new PetCollectionSlot(_catalog.GetById(item.ItemIndex), worn is not null, TimedItemRules.HasExpired(item, now), item.Stack);
    }

    private (int ItemId, int Count) GroundItem(ushort mapId)
    {
        if (!_loot.TryGetValue(mapId, out var loot))
        {
            var drill = _drillTables();
            loot = drill.ZoneItems.TryGetValue(mapId, out var row) ? new DrillLoot(row, drill.ItemTables, drill.EventRateAdd) : null;
            _loot[mapId] = loot;
        }
        var itemId = loot?.Pop(_ => false, _rng) ?? PetCollectionRules.NothingDrawn;
        return itemId == PetCollectionRules.NothingDrawn ? (itemId, 0) : (itemId, _itemManager.NewStack(itemId, 1));
    }

    private BagOperation SpendOne(Item food)
    {
        var itemClass = _catalog.GetById(food.ItemIndex)?.ObjectClass ?? food.ItemClass;
        var left = _items.AdjustStack(food.ItemUid, -1);
        return left > 0
            ? new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)food.ItemIndex, BagItemIds.OnWire(food.ItemUid), left)
            : new BagOperation(QuestResultFrames.Removed, itemClass, (uint)food.ItemIndex, BagItemIds.OnWire(food.ItemUid), 0);
    }

    private static BagOperation Removal(PetCollectedItem entry) =>
        new(QuestResultFrames.Removed, entry.ItemClass, (uint)entry.ItemId, entry.CollectedUid, 0);

    private static PetBagEntry Entry(PetCollectedItem item) => new(item.ItemClass, (uint)item.ItemId, item.CollectedUid, item.Count);

    private async Task RefuseAsync(InGameSessionState player, Opcode opcode, byte code, string reason)
    {
        Log(player, $"refused with code {code}: {reason}");
        await SendAsync(player, opcode, PetCollectionFrames.Code(code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [pet-collection] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
