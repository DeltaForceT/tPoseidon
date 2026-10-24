using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class RecycleController
{
    private readonly Func<IReadOnlyDictionary<int, RecycleChangeEntry>> _changes;
    private readonly Func<IReadOnlyDictionary<int, RecycleShopEntry>> _shop;
    private readonly Func<IReadOnlySet<int>> _excluded;
    private readonly Func<IReadOnlySet<int>> _myCampExcluded;
    private readonly IMyCampStore _myCamp;
    private readonly IItemPropertyStore _properties;
    private readonly IItemStore _items;
    private readonly ItemManager _itemManager;
    private readonly IItemParamLookup _catalog;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public RecycleController(
        Func<IReadOnlyDictionary<int, RecycleChangeEntry>> changes,
        Func<IReadOnlyDictionary<int, RecycleShopEntry>> shop,
        Func<IReadOnlySet<int>> excluded,
        Func<IReadOnlySet<int>> myCampExcluded,
        IMyCampStore myCamp,
        IItemPropertyStore properties,
        IItemStore items,
        ItemManager itemManager,
        IItemParamLookup catalog,
        string logTag)
    {
        _changes = changes ?? throw new ArgumentNullException(nameof(changes));
        _shop = shop ?? throw new ArgumentNullException(nameof(shop));
        _excluded = excluded ?? throw new ArgumentNullException(nameof(excluded));
        _myCampExcluded = myCampExcluded ?? throw new ArgumentNullException(nameof(myCampExcluded));
        _myCamp = myCamp ?? throw new ArgumentNullException(nameof(myCamp));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenCouponExchangeAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_RECYCLE_OPENED, RecycleFrames.Opened(RecycleKind.CouponExchange)).ConfigureAwait(false);
        player.Recycle = RecycleKind.CouponExchange;
        Log(player, "coupon exchange opened");
        return NpcScript.Silent;
    }

    public async Task<int> OpenItemRecycleAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_RECYCLE_OPENED, RecycleFrames.Opened(RecycleKind.Inventory)).ConfigureAwait(false);
        player.Recycle = RecycleKind.Inventory;
        Log(player, "item recycle opened");
        return NpcScript.Silent;
    }

    public async Task<int> OpenMyCampRecycleAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        player.Recycle = RecycleKind.MyCamp;
        await SendAsync(player, Opcode.S2C_RECYCLE_OPENED, RecycleFrames.Opened(RecycleKind.MyCamp)).ConfigureAwait(false);
        Log(player, "my camp recycle opened");
        return NpcScript.Silent;
    }

    public async Task RecycleMyCampItemsAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Recycle != RecycleKind.MyCamp || player.Character is not { } character) return;
        if (!RecycleFrames.TryReadMyCampItems(payload.Span, out var request))
        {
            Log(player, $"my camp recycle of {payload.Length} bytes not read");
            return;
        }
        _shop().TryGetValue(RecycleRules.MyCampRecycleType, out var row);
        var excluded = _myCampExcluded();
        foreach (var entry in request.Entries)
        {
            if (excluded.Contains(entry.ItemId) || row is null || row.CouponCount <= 0 || _catalog.GetById(entry.ItemId) is null)
            {
                Log(player, $"my camp item {entry.ItemId} cannot be recycled");
                await SendAsync(player, Opcode.S2C_RECYCLE_MYCAMP_RESULT, RecycleFrames.MyCampRefused(RecycleRules.MyCampItemRefused)).ConfigureAwait(false);
                return;
            }
        }
        if (request.Entries.Count == 0 || row is null) return;
        var seen = new HashSet<ulong>();
        var owned = request.Entries.All(entry =>
            seen.Add(entry.ItemUid)
            && _myCamp.FindItem(entry.ItemUid) is { } held
            && held.AccountId == character.OwnerAccountId
            && held.ItemWhere != MyCampItemWhere.Deleted
            && held.ItemIndex == entry.ItemId);
        var coupons = request.Entries.Count * row.CouponCount;
        if (request.CouponItemId != (uint)row.CouponItemId || request.CouponCount != coupons)
        {
            Log(player, $"my camp recycle asks {request.CouponCount} x item {request.CouponItemId}, the table gives {coupons} x item {row.CouponItemId}");
        }
        var couponDefinition = _catalog.GetById(row.CouponItemId);
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var joins = couponDefinition is { IsStackable: true } && bag.Any(item => item.ItemIndex == row.CouponItemId);
        if (!joins && bag.Count >= PickupRules.BagCapacity)
        {
            Log(player, "my camp recycle HasAcceptableSpace refused");
            return;
        }
        if (!owned || _myCamp.BulkDeleteItems(request.Entries.Select(entry => entry.ItemUid)) != request.Entries.Count)
        {
            Log(player, "my camp recycle: camp items not removed");
            await SendAsync(player, Opcode.S2C_RECYCLE_MYCAMP_RESULT, RecycleFrames.MyCampRefused(RecycleRules.MyCampExchangeFailed)).ConfigureAwait(false);
            return;
        }
        var couponClass = couponDefinition?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        var stack = joins ? bag.FirstOrDefault(item => item.ItemIndex == row.CouponItemId) : null;
        BagOperation operation;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, coupons);
            operation = new BagOperation(QuestResultFrames.StackChanged, couponClass, (uint)row.CouponItemId, BagItemIds.OnWire(stack.ItemUid), total);
        }
        else
        {
            var minted = _itemManager.GrantRewardItem(character.CharacterId, row.CouponItemId, (ushort)Math.Min(coupons, ushort.MaxValue), itemClassOverride: couponClass);
            operation = new BagOperation(QuestResultFrames.Created, couponClass, (uint)row.CouponItemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot);
        }
        await SendAsync(player, Opcode.S2C_RECYCLE_MYCAMP_RESULT, RecycleFrames.Exchanged(new[] { operation })).ConfigureAwait(false);
        Log(player, $"recycles {request.Entries.Count} camp item(s) for {coupons} coupon(s) of item {row.CouponItemId}");
    }

    public async Task RecycleItemsAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Recycle != RecycleKind.Inventory || player.Character is not { } character) return;
        if (!RecycleFrames.TryReadItems(payload.Span, out var request))
        {
            Log(player, $"item recycle of {payload.Length} bytes not read");
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var shop = _shop();
        var excluded = _excluded();
        var coupons = 0;
        var couponItemId = 0;
        var seen = new HashSet<ulong>();
        var picked = new List<(Item Item, ItemParamEntry Definition)>(request.Entries.Count);
        foreach (var entry in request.Entries)
        {
            var held = bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(entry.WireUid));
            if (held is null || held.ItemIndex != (int)entry.ItemId || !seen.Add(held.ItemUid) || _catalog.GetById(held.ItemIndex) is not { } definition)
            {
                Log(player, $"[Recycle Cancel] invalid item id {entry.ItemId} uid {entry.WireUid}");
                await SendAsync(player, Opcode.S2C_RECYCLE_ITEMS_RESULT, RecycleFrames.ItemsRefused()).ConfigureAwait(false);
                return;
            }
            var options = ItemComposingRules.Options(definition, _properties.ForItem(held.ItemUid));
            var type = excluded.Contains(held.ItemIndex) ? null : RecycleRules.RecycleTypeOf(definition.Type, options);
            if (type is null || !shop.TryGetValue(type.Value, out var row) || row.CouponCount <= 0)
            {
                Log(player, $"item {held.ItemIndex} cannot be recycled (type {type?.ToString() ?? "none"})");
                return;
            }
            coupons += row.CouponCount;
            couponItemId = row.CouponItemId;
            picked.Add((held, definition));
        }
        if (picked.Count == 0 || coupons != request.CouponTotal)
        {
            Log(player, $"ExchangeCouponCnt Mismatch: {coupons} computed, {request.CouponTotal} sent");
            await SendAsync(player, Opcode.S2C_RECYCLE_ITEMS_RESULT, RecycleFrames.ItemsRefused()).ConfigureAwait(false);
            return;
        }
        var couponDefinition = _catalog.GetById(couponItemId);
        var joins = couponDefinition is { IsStackable: true } && bag.Any(item => item.ItemIndex == couponItemId);
        var freed = picked.Count(entry => entry.Item.Stack <= 1);
        if (!joins && bag.Count - freed >= PickupRules.BagCapacity)
        {
            Log(player, "HasAcceptableSpace refused");
            await SendAsync(player, Opcode.S2C_RECYCLE_ITEMS_RESULT, RecycleFrames.ItemsRefused()).ConfigureAwait(false);
            return;
        }
        var operations = new List<BagOperation>(picked.Count + 1);
        foreach (var (item, definition) in picked)
        {
            if (item.Stack > 1)
            {
                var left = _items.AdjustStack(item.ItemUid, -1);
                operations.Add(new BagOperation(QuestResultFrames.StackChanged, definition.ObjectClass, (uint)item.ItemIndex, BagItemIds.OnWire(item.ItemUid), left));
            }
            else
            {
                _items.Delete(item.ItemUid);
                operations.Add(new BagOperation(QuestResultFrames.Removed, definition.ObjectClass, (uint)item.ItemIndex, BagItemIds.OnWire(item.ItemUid), 0));
            }
        }
        var couponClass = couponDefinition?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        var stack = joins
            ? _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory).FirstOrDefault(item => item.ItemIndex == couponItemId)
            : null;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, coupons);
            operations.Add(new BagOperation(QuestResultFrames.StackChanged, couponClass, (uint)couponItemId, BagItemIds.OnWire(stack.ItemUid), total));
        }
        else
        {
            var minted = _itemManager.GrantRewardItem(character.CharacterId, couponItemId, (ushort)Math.Min(coupons, ushort.MaxValue), itemClassOverride: couponClass);
            operations.Add(new BagOperation(QuestResultFrames.Created, couponClass, (uint)couponItemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot));
        }
        await SendAsync(player, Opcode.S2C_RECYCLE_ITEMS_RESULT, RecycleFrames.Exchanged(operations)).ConfigureAwait(false);
        Log(player, $"recycles {picked.Count} item(s) for {coupons} coupon(s) of item {couponItemId}");
    }

    public async Task CloseAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!RecycleFrames.TryReadClose(payload.Span, out var kind)) return;
        player.Recycle = null;
        await SendAsync(player, Opcode.S2C_RECYCLE_CLOSED, RecycleFrames.Closed((RecycleKind)kind)).ConfigureAwait(false);
        Log(player, $"recycle window {kind} closed");
    }

    public async Task ExchangeCouponsAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Recycle != RecycleKind.CouponExchange || player.Character is not { } character) return;
        if (!RecycleFrames.TryReadCouponExchange(payload.Span, out var request))
        {
            Log(player, $"coupon exchange of {payload.Length} bytes not read");
            return;
        }
        _changes().TryGetValue((int)request.ResultItemId, out var row);
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var source = bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(request.SourceWireUid));
        var resultDefinition = _catalog.GetById((int)request.ResultItemId);
        var joins = resultDefinition is { IsStackable: true } && bag.Any(item => item.ItemIndex == (int)request.ResultItemId);
        var fits = joins || bag.Count < PickupRules.BagCapacity;
        var refusal = RecycleRules.Check(request, row, source?.ItemIndex, source?.Stack ?? 0, fits);
        if (refusal is not null || source is null || row is null)
        {
            Log(player, $"coupon exchange refused: {refusal} (item {request.SourceItemId} x{request.SourceCount} for {request.ResultItemId})");
            return;
        }
        var operations = new List<BagOperation>(2);
        var sourceClass = _catalog.GetById(source.ItemIndex)?.ObjectClass ?? source.ItemClass;
        if (source.Stack > request.SourceCount)
        {
            var left = _items.AdjustStack(source.ItemUid, -request.SourceCount);
            operations.Add(new BagOperation(QuestResultFrames.StackChanged, sourceClass, (uint)source.ItemIndex, BagItemIds.OnWire(source.ItemUid), left));
        }
        else
        {
            _items.Delete(source.ItemUid);
            operations.Add(new BagOperation(QuestResultFrames.Removed, sourceClass, (uint)source.ItemIndex, BagItemIds.OnWire(source.ItemUid), 0));
        }
        var resultCount = (ushort)Math.Clamp(row.ExchangeResCnt, 1, ushort.MaxValue);
        var resultClass = resultDefinition?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        var stack = joins
            ? _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory).FirstOrDefault(item => item.ItemIndex == row.ExchangeRes)
            : null;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, resultCount);
            operations.Add(new BagOperation(QuestResultFrames.StackChanged, resultClass, (uint)row.ExchangeRes, BagItemIds.OnWire(stack.ItemUid), total));
        }
        else
        {
            var minted = _itemManager.GrantRewardItem(character.CharacterId, row.ExchangeRes, resultCount, itemClassOverride: resultClass);
            operations.Add(new BagOperation(QuestResultFrames.Created, resultClass, (uint)row.ExchangeRes, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot));
        }
        await SendAsync(player, Opcode.S2C_RECYCLE_COUPON_EXCHANGED, RecycleFrames.Exchanged(operations)).ConfigureAwait(false);
        Log(player, $"exchanges {request.SourceCount} x item {request.SourceItemId} for {resultCount} x item {row.ExchangeRes}");
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [recycle] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
