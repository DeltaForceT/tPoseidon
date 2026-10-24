using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.PlayerShop;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class PlayerShopController
{
    private readonly MasterTrade _master;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly Func<Item, bool> _exchangeable;
    private readonly Func<ushort, ushort, InGameSessionState?> _playerOnMap;
    private readonly Func<ushort, int> _zoneType;
    private readonly Func<InGameSessionState, Opcode, byte[], Task> _sendToArea;
    private readonly Action<InGameSessionState> _stopMover;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _players;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public PlayerShopController(
        MasterTrade master,
        IItemStore items,
        IItemParamLookup catalog,
        IItemPropertyStore properties,
        Func<Item, bool> exchangeable,
        Func<ushort, ushort, InGameSessionState?> playerOnMap,
        Func<ushort, int> zoneType,
        Func<InGameSessionState, Opcode, byte[], Task> sendToArea,
        Action<InGameSessionState> stopMover,
        Func<IReadOnlyCollection<InGameSessionState>> players,
        Encoding text,
        string logTag)
    {
        _players = players ?? throw new ArgumentNullException(nameof(players));
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _exchangeable = exchangeable ?? throw new ArgumentNullException(nameof(exchangeable));
        _playerOnMap = playerOnMap ?? throw new ArgumentNullException(nameof(playerOnMap));
        _zoneType = zoneType ?? throw new ArgumentNullException(nameof(zoneType));
        _sendToArea = sendToArea ?? throw new ArgumentNullException(nameof(sendToArea));
        _stopMover = stopMover ?? throw new ArgumentNullException(nameof(stopMover));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public static bool RefusesSkills(InGameSessionState player) => player.Stall is not null;

    public async Task OpenAsync(InGameSessionState keeper)
    {
        ArgumentNullException.ThrowIfNull(keeper);
        if (keeper.Stall is not null || keeper.VisitingKeeper is not null) return;
        if (!PlayerShopRules.OpensInZone(_zoneType(keeper.MapId)))
        {
            Log(keeper, $"shop refused on map {keeper.MapId} (zone type {_zoneType(keeper.MapId)})");
            await SendAsync(keeper, Opcode.S2C_PLAYER_SHOP_OPEN_REFUSED, PlayerShopFrames.Code(PlayerShopRules.WrongZone)).ConfigureAwait(false);
            return;
        }
        keeper.Stall = new PlayerShopStall(keeper.PlayerEntityId);
        await SendAsync(keeper, Opcode.S2C_PLAYER_SHOP_OPENED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(keeper, "opens a shop");
    }

    public async Task RegisterAsync(InGameSessionState keeper, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(keeper);
        if (keeper.Stall is not { } stall || keeper.Character is not { } character) return;
        if (!PlayerShopFrames.TryReadRegister(payload.Span, _text, out var request) || !PlayerShopRules.SlotCountAllowed(request.Slots.Count))
        {
            await RefuseRegisterAsync(keeper, $"{payload.Length} bytes or slot count not allowed").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        foreach (var slot in request.Slots)
        {
            if (!PlayerShopRules.PriceAllowed(slot.UnitPrice))
            {
                await RefuseRegisterAsync(keeper, $"price {slot.UnitPrice} for item {slot.ItemId}").ConfigureAwait(false);
                return;
            }
            var held = bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(slot.WireUid));
            if (held is null || held.ItemIndex != (int)slot.ItemId || !Holds(held, slot.Count) || !_exchangeable(held))
            {
                await RefuseRegisterAsync(keeper, $"item {slot.ItemId} uid 0x{slot.WireUid:X} x{slot.Count} not held or not exchangeable").ConfigureAwait(false);
                return;
            }
        }
        stall.Register(request.ShopType, request.Title, request.Slots);
        await _sendToArea(keeper, Opcode.S2C_PLAYER_SHOP_SIGN, PlayerShopFrames.Sign(keeper.PlayerEntityId, request.Title, request.ShopType, _text)).ConfigureAwait(false);
        await SendAsync(keeper, Opcode.S2C_PLAYER_SHOP_REGISTERED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(keeper, $"registers {request.Slots.Count} items, type {request.ShopType}, title '{request.Title}'");
    }

    public Task StartAsync(InGameSessionState keeper)
    {
        ArgumentNullException.ThrowIfNull(keeper);
        return keeper.Stall is null ? Task.CompletedTask : SendAsync(keeper, Opcode.S2C_PLAYER_SHOP_STARTED, Array.Empty<byte>());
    }

    public async Task CloseAsync(InGameSessionState keeper)
    {
        ArgumentNullException.ThrowIfNull(keeper);
        if (keeper.Stall is not { } stall) return;
        keeper.Stall = null;
        foreach (var visitorId in stall.Visitors)
        {
            if (_playerOnMap(keeper.MapId, visitorId) is { VisitingKeeper: var visiting } visitor && visiting == keeper.PlayerEntityId) visitor.VisitingKeeper = null;
        }
        await SendAsync(keeper, Opcode.S2C_PLAYER_SHOP_CLOSED, Array.Empty<byte>()).ConfigureAwait(false);
        await _sendToArea(keeper, Opcode.S2C_PLAYER_SHOP_SIGN_REMOVED, PlayerShopFrames.Word(keeper.PlayerEntityId)).ConfigureAwait(false);
        Log(keeper, "closes the shop");
    }

    public async Task VisitAsync(InGameSessionState buyer, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        if (buyer.Stall is not null || buyer.VisitingKeeper is not null || !PlayerShopFrames.TryReadVisit(payload.Span, out var keeperId)) return;
        var keeper = keeperId == 0 ? null : _playerOnMap(buyer.MapId, keeperId);
        if (keeper is not { Stall: { } stall, Character: { } keeperCharacter })
        {
            await SendAsync(buyer, Opcode.S2C_PLAYER_SHOP_VISIT_REFUSED, PlayerShopFrames.Code(PlayerShopRules.VisitRefused)).ConfigureAwait(false);
            return;
        }
        stall.PruneVisitors(visitorId => _playerOnMap(keeper.MapId, visitorId) is { VisitingKeeper: var visiting } && visiting == keeperId);
        if (!stall.AddVisitor(buyer.PlayerEntityId))
        {
            await SendAsync(buyer, Opcode.S2C_PLAYER_SHOP_VISIT_REFUSED, PlayerShopFrames.Code(PlayerShopRules.VisitorsFull)).ConfigureAwait(false);
            return;
        }
        _stopMover(buyer);
        buyer.VisitingKeeper = keeperId;
        await SendAsync(buyer, Opcode.S2C_PLAYER_SHOP_LIST, PlayerShopFrames.List(Listed(keeperCharacter.CharacterId, stall))).ConfigureAwait(false);
        Log(buyer, $"visits the shop of 0x{keeperId:X4} ({stall.Slots.Count} slots)");
    }

    public async Task BuyAsync(InGameSessionState buyer, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        if (buyer.VisitingKeeper is null || buyer.Character is not { } buyerCharacter || buyer.Stats is not { } buyerStats) return;
        if (!PlayerShopFrames.TryReadBuy(payload.Span, out var purchase)) return;
        var keeper = _playerOnMap(buyer.MapId, purchase.KeeperId);
        if (keeper is not { Stall: { } stall, Character: { } keeperCharacter, Stats: { } keeperStats })
        {
            await SendAsync(buyer, Opcode.S2C_PLAYER_SHOP_KEEPER_MISSING, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        if (stall.Reserve(purchase) is { } refusal)
        {
            Log(buyer, $"buy of item {purchase.ItemId} from 0x{purchase.KeeperId:X4} refused: {refusal}");
            await SendAsync(buyer, refusal switch
            {
                PlayerShopBuyRefusal.InvalidItem => Opcode.S2C_PLAYER_SHOP_INVALID_ITEM,
                PlayerShopBuyRefusal.ItemInBusiness => Opcode.S2C_PLAYER_SHOP_ITEM_IN_BUSINESS,
                _ => Opcode.S2C_PLAYER_SHOP_KEEPER_BUSY,
            }, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        var sold = false;
        try
        {
            byte? visitRefusal = null;
            var buyerBag = _items.GetForCharacterBySlotKind(buyerCharacter.CharacterId, SlotKind.Inventory);
            var joins = _catalog.GetById((int)purchase.ItemId) is { IsStackable: true } && buyerBag.Any(item => item.ItemIndex == (int)purchase.ItemId);
            if (buyer.Trade is not null || keeper.Trade is not null) visitRefusal = PlayerShopRules.VisitRefused;
            else if (!joins && buyerBag.Count >= PickupRules.BagCapacity) visitRefusal = PlayerShopRules.BagFull;
            else if (!PlayerShopRules.MoneySettles(buyerStats.Money, keeperStats.Money, purchase.UnitPrice, purchase.Count)) visitRefusal = PlayerShopRules.MoneyOutOfRange;
            if (visitRefusal is { } code)
            {
                Log(buyer, $"buy of item {purchase.ItemId} x{purchase.Count} refused with 0x015A code {code}");
                await SendAsync(buyer, Opcode.S2C_PLAYER_SHOP_VISIT_REFUSED, PlayerShopFrames.Code(code)).ConfigureAwait(false);
                return;
            }
            var total = (uint)(purchase.UnitPrice * purchase.Count);
            var result = await _master.TradeAsync(
                new TradeOffer(buyerCharacter.CharacterId, total, Array.Empty<TradeSlot>()),
                new TradeOffer(keeperCharacter.CharacterId, 0, [new TradeSlot(BagItemIds.Stored(purchase.WireUid), (int)purchase.ItemId, purchase.Count)]),
                PickupRules.BagCapacity,
                default).ConfigureAwait(false);
            if (!result.Succeeded || result.First is not { } bought || result.Second is not { } paid
                || bought.Changes.Count == 0 || paid.Changes.Count == 0)
            {
                Log(buyer, $"buy of item {purchase.ItemId} x{purchase.Count} from 0x{purchase.KeeperId:X4}: store refused with code {result.Error}");
                await SendAsync(buyer, Opcode.S2C_PLAYER_SHOP_BUY_FAILED, PlayerShopFrames.Word(PlayerShopRules.BuyerResultFailed)).ConfigureAwait(false);
                Leave(buyer);
                return;
            }
            sold = true;
            Settle(buyer, bought);
            Settle(keeper, paid);
            await SendAsync(buyer, Opcode.S2C_PLAYER_SHOP_BOUGHT, PlayerShopFrames.Bought(bought.Money, Operation(bought.Changes[0]))).ConfigureAwait(false);
            await SendAsync(keeper, Opcode.S2C_PLAYER_SHOP_SOLD, PlayerShopFrames.Sold(paid.Money, purchase.Count, Operation(paid.Changes[0]))).ConfigureAwait(false);
            Log(buyer, $"buys item {purchase.ItemId} x{purchase.Count} from 0x{purchase.KeeperId:X4} for {total}");
            Leave(buyer);
        }
        finally
        {
            stall.Release(purchase, sold);
        }
    }

    public async Task OpenSearchAsync(InGameSessionState searcher)
    {
        ArgumentNullException.ThrowIfNull(searcher);
        if (searcher.Stall is not null || searcher.VisitingKeeper is not null || searcher.Trade is not null)
        {
            Log(searcher, "search window refused: in a shop or a trade");
            await SendAsync(searcher, Opcode.S2C_PLAYER_SHOP_SEARCH_OPENED, PlayerShopSearchFrames.Code(PlayerShopSearchRules.OpenRefused)).ConfigureAwait(false);
            return;
        }
        searcher.ShopSearching = true;
        searcher.ShopSearch = null;
        await SendAsync(searcher, Opcode.S2C_PLAYER_SHOP_SEARCH_OPENED, PlayerShopSearchFrames.Code(PlayerShopSearchRules.OpenOk)).ConfigureAwait(false);
        Log(searcher, "search window opened");
    }

    public async Task SearchAsync(InGameSessionState searcher, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(searcher);
        if (!searcher.ShopSearching || !PlayerShopSearchFrames.TryReadQuery(payload.Span, _text, out var query)) return;
        var rows = new List<PlayerShopSearchRow>();
        foreach (var keeper in _players())
        {
            if (keeper.Stall is not { Registered: true } stall || keeper.Character is not { } keeperCharacter) continue;
            var bag = _items.GetForCharacterBySlotKind(keeperCharacter.CharacterId, SlotKind.Inventory);
            foreach (var slot in stall.Slots)
            {
                var held = bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(slot.WireUid));
                if (held is null || held.ItemClass != TradeRules.CommonClass || slot.Count == 0) continue;
                if (_catalog.GetById(held.ItemIndex) is not { } definition || !PlayerShopSearchRules.Matches(definition, query)) continue;
                rows.Add(new PlayerShopSearchRow(keeper.PlayerEntityId, (byte)Math.Clamp(keeper.PlayerCharacterType, 0, byte.MaxValue), keeperCharacter.Name,
                    held.ItemClass, (uint)held.ItemIndex, slot.WireUid, held.Stack, (uint)slot.UnitPrice, slot.Count));
            }
        }
        var result = new PlayerShopSearchResult(rows);
        searcher.ShopSearch = result;
        await SendAsync(searcher, Opcode.S2C_PLAYER_SHOP_SEARCH_RESULT, PlayerShopSearchFrames.Page(result.TakePage(), _text)).ConfigureAwait(false);
        Log(searcher, $"searches category {query.Category}, level {query.MinLevel}..{query.MaxLevel}, text '{query.Text}': {result.Count} rows");
    }

    public async Task NextSearchPageAsync(InGameSessionState searcher)
    {
        ArgumentNullException.ThrowIfNull(searcher);
        if (!searcher.ShopSearching || searcher.ShopSearch is not { } result) return;
        await SendAsync(searcher, Opcode.S2C_PLAYER_SHOP_SEARCH_PAGE, PlayerShopSearchFrames.Page(result.TakePage(), _text)).ConfigureAwait(false);
    }

    public async Task CloseSearchAsync(InGameSessionState searcher)
    {
        ArgumentNullException.ThrowIfNull(searcher);
        searcher.ShopSearching = false;
        searcher.ShopSearch = null;
        await SendAsync(searcher, Opcode.S2C_PLAYER_SHOP_SEARCH_CLOSED, Array.Empty<byte>()).ConfigureAwait(false);
    }

    public async Task SearchBuyAsync(InGameSessionState buyer, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        if (!buyer.ShopSearching || !PlayerShopSearchFrames.TryReadBuy(payload.Span, out var buy)) return;
        if (buyer.Character is not { } buyerCharacter || buyer.Stats is not { } buyerStats
            || buyer.Stall is not null || buyer.VisitingKeeper is not null || buyer.Trade is not null)
        {
            await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.InvalidBuyerState, "buyer in a shop or a trade").ConfigureAwait(false);
            return;
        }
        if (!PlayerShopRules.OpensInZone(_zoneType(buyer.MapId)))
        {
            await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.InvalidZone, $"zone type {_zoneType(buyer.MapId)}").ConfigureAwait(false);
            return;
        }
        var keeper = _players().FirstOrDefault(player => player.PlayerEntityId == buy.KeeperId && player.Character is not null);
        if (keeper is not { Character: { } keeperCharacter, Stats: { } keeperStats })
        {
            await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.InvalidSeller, $"keeper 0x{buy.KeeperId:X4} gone").ConfigureAwait(false);
            return;
        }
        if (keeper.Stall is not { Registered: true } stall || keeper.Trade is not null)
        {
            await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.InvalidSellerState, $"keeper 0x{buy.KeeperId:X4} is not selling").ConfigureAwait(false);
            return;
        }
        if (stall.Slots.FirstOrDefault(slot => slot.WireUid == buy.WireUid) is not { Count: > 0 } listed)
        {
            await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.InvalidSellItem, $"item 0x{buy.WireUid:X} not listed").ConfigureAwait(false);
            return;
        }
        if (buy.Count == 0 || buy.Count > listed.Count)
        {
            await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.NotEnoughItemCount, $"{buy.Count} asked, {listed.Count} listed").ConfigureAwait(false);
            return;
        }
        if (!PlayerShopSearchRules.PriceHolds((uint)listed.UnitPrice, buy))
        {
            await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.PriceChanged, $"total {buy.TotalPrice} for {buy.Count} x {listed.UnitPrice}").ConfigureAwait(false);
            return;
        }
        if (!PlayerShopRules.MoneySettles(buyerStats.Money, keeperStats.Money, (uint)listed.UnitPrice, buy.Count))
        {
            await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.NotEnoughMoney, $"total {buy.TotalPrice}, money {buyerStats.Money}").ConfigureAwait(false);
            return;
        }
        var buyerBag = _items.GetForCharacterBySlotKind(buyerCharacter.CharacterId, SlotKind.Inventory);
        var definition = _catalog.GetById((int)listed.ItemId);
        var weight = buyerBag.Sum(held => (_catalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits) + (definition?.Weight ?? 0) * buy.Count;
        if (weight > buyer.PlayerMaxWt)
        {
            await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.OverflowWeight, $"weight {weight} over {buyer.PlayerMaxWt}").ConfigureAwait(false);
            return;
        }
        var joins = definition is { IsStackable: true } && buyerBag.Any(item => item.ItemIndex == (int)listed.ItemId);
        if (!joins && buyerBag.Count >= PickupRules.BagCapacity)
        {
            await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.FullInventory, $"{buyerBag.Count} items in the bag").ConfigureAwait(false);
            return;
        }
        var purchase = new PlayerShopPurchase(buy.KeeperId, buy.WireUid, listed.ItemId, buy.Count, (uint)listed.UnitPrice);
        if (stall.Reserve(purchase) is { } refusal)
        {
            await RefuseSearchBuyAsync(buyer, refusal == PlayerShopBuyRefusal.InvalidItem ? PlayerShopSearchRules.InvalidSellItem : PlayerShopSearchRules.BuyProcessingItem,
                $"slot reserve refused: {refusal}").ConfigureAwait(false);
            return;
        }
        var sold = false;
        try
        {
            var result = await _master.TradeAsync(
                new TradeOffer(buyerCharacter.CharacterId, buy.TotalPrice, Array.Empty<TradeSlot>()),
                new TradeOffer(keeperCharacter.CharacterId, 0, [new TradeSlot(BagItemIds.Stored(buy.WireUid), (int)listed.ItemId, buy.Count)]),
                PickupRules.BagCapacity,
                default).ConfigureAwait(false);
            if (!result.Succeeded || result.First is not { } bought || result.Second is not { } paid || bought.Changes.Count == 0 || paid.Changes.Count == 0)
            {
                await RefuseSearchBuyAsync(buyer, PlayerShopSearchRules.EtcError, $"store refused with code {result.Error}").ConfigureAwait(false);
                return;
            }
            sold = true;
            Settle(buyer, bought);
            Settle(keeper, paid);
            await SendAsync(buyer, Opcode.S2C_PLAYER_SHOP_SEARCH_BOUGHT,
                PlayerShopSearchFrames.Bought(bought.Money, Operation(bought.Changes[0]), buy.WireUid, (ushort)(listed.Count - buy.Count))).ConfigureAwait(false);
            await SendAsync(keeper, Opcode.S2C_PLAYER_SHOP_SOLD, PlayerShopFrames.Sold(paid.Money, buy.Count, Operation(paid.Changes[0]))).ConfigureAwait(false);
            Log(buyer, $"buys item {listed.ItemId} x{buy.Count} from 0x{buy.KeeperId:X4} through the search for {buy.TotalPrice}");
        }
        finally
        {
            stall.Release(purchase, sold);
        }
    }

    private async Task RefuseSearchBuyAsync(InGameSessionState buyer, byte code, string reason)
    {
        Log(buyer, $"search buy refused with code {code}: {reason}");
        await SendAsync(buyer, Opcode.S2C_PLAYER_SHOP_SEARCH_REFUSED, PlayerShopSearchFrames.Code(code)).ConfigureAwait(false);
    }

    public async Task LeaveAsync(InGameSessionState buyer)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        if (buyer.VisitingKeeper is null) return;
        Leave(buyer);
        await SendAsync(buyer, Opcode.S2C_PLAYER_SHOP_LEFT, Array.Empty<byte>()).ConfigureAwait(false);
    }

    private void Leave(InGameSessionState buyer)
    {
        if (buyer.VisitingKeeper is not { } keeperId) return;
        buyer.VisitingKeeper = null;
        _playerOnMap(buyer.MapId, keeperId)?.Stall?.RemoveVisitor(buyer.PlayerEntityId);
        Log(buyer, $"Ends to shop of 0x{keeperId:X4}");
    }

    private List<PlayerShopListedSlot> Listed(ulong keeperCharacterId, PlayerShopStall stall)
    {
        var bag = _items.GetForCharacterBySlotKind(keeperCharacterId, SlotKind.Inventory);
        return stall.Slots.Select(slot =>
        {
            var held = bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(slot.WireUid));
            if (held is null) return new PlayerShopListedSlot(slot, null, Array.Empty<ItemProperty>());
            var view = new TradeItemView(held.ItemClass, (uint)held.ItemIndex, slot.WireUid, held.Stack,
                (byte)held.RefineType, held.RefineLevel, held.RefineState, held.ItemSlot);
            return new PlayerShopListedSlot(slot, view, _properties.ForItem(held.ItemUid));
        }).ToList();
    }

    private static bool Holds(Item held, ushort count) =>
        count > 0 && (held.ItemClass == TradeRules.CommonClass ? count <= held.Stack : count == 1);

    private void Settle(InGameSessionState player, TradeSideResult result)
    {
        if (player.Stats is { } stats) stats.GrantMoney((int)((long)result.Money - stats.Money), tPoseidon.Core.Game.Stats.StatChangeSource.Trade);
        foreach (var change in result.Changes)
        {
            if (change.Kind == TradeChangeKind.Removed)
            {
                player.TimedItems.Forget(change.ItemUid);
            }
            else if (change.Kind == TradeChangeKind.Created && _items.FindByUid(change.ItemUid) is { } arrived && arrived.BreakedAt > Item.NeverBreaks)
            {
                player.TimedItems.Track(new tPoseidon.Core.Game.Items.TimedItem(arrived.ItemUid, arrived.ItemIndex, arrived.BreakedAt));
            }
        }
    }

    private static BagOperation Operation(TradeItemChange change) => new(
        change.Kind switch
        {
            TradeChangeKind.StackChanged => QuestResultFrames.StackChanged,
            TradeChangeKind.Removed => QuestResultFrames.Removed,
            _ => QuestResultFrames.Created,
        },
        change.ItemClass, (uint)change.ItemIndex, BagItemIds.OnWire(change.ItemUid), change.Count, change.MixSlot);

    private async Task RefuseRegisterAsync(InGameSessionState keeper, string reason)
    {
        Log(keeper, $"register refused: {reason}");
        await SendAsync(keeper, Opcode.S2C_PLAYER_SHOP_REGISTER_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [player shop] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
