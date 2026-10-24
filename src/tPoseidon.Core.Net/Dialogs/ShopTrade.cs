using System.Buffers.Binary;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Shop;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record ShopOffer
{
    public required ushort NpcEntityId { get; init; }
    public required IReadOnlyList<ShopGoodsEntry> Goods { get; init; }
    public required float PurchaseRate { get; init; }
    public required float SellingRate { get; init; }
    public required float PriceRate { get; init; }
    public required bool SellsSkillCards { get; init; }
}

public sealed record ShopPlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<StatManager?> Stats { get; init; }
    public required Func<int> MaxWeight { get; init; }
    public required Func<ushort> MapId { get; init; }
}

public sealed class ShopTrade
{
    public const int SellLineSize = 14;
    public const int SellHeaderSize = 4;

    private readonly S2SessionContext _ctx;
    private readonly IItemParamLookup _definitions;
    private readonly ShopPlayer _player;
    private readonly IItemStore _items;
    private readonly ItemManager _itemManager;
    private readonly Random _rng;
    private readonly string _logTag;

    public ShopTrade(
        S2SessionContext ctx,
        IItemParamLookup definitions,
        ShopPlayer player,
        IItemStore items,
        ItemManager itemManager,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public IReadOnlyList<(int ItemId, int UnitPrice)> BuyPrices(ShopOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return offer.Goods
            .Where(goods => !goods.OutOfStock)
            .Select(goods => (goods.ItemId, BuyUnitPrice(offer, goods.ItemId, goods)))
            .ToList();
    }

    public static (float ShopRate, IReadOnlyList<(int ItemId, float Rate)> Rates) SellRates(ShopOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var shopRate = ShopPrices.PriceRateOf(offer.PriceRate) * ShopPrices.SellingRateOf(offer.SellingRate);
        var rates = offer.Goods
            .Where(goods => goods.SellingRate > 0f)
            .Select(goods => (goods.ItemId, goods.SellingRate))
            .ToList();
        return (shopRate, rates);
    }

    public async Task<Opcode?> TryBuyAsync(byte[] payload, Func<ushort, ShopOffer?> offerOf, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(offerOf);
        if (payload.Length < ShopPrices.BuyHeaderSize) return Opcode.S2C_SHOP_BUY_REFUSED;
        var shopEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        var lines = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(2));
        if (offerOf(shopEntityId) is not { } offer) return Opcode.S2C_SHOP_BUY_REFUSED;
        var linesSize = lines * ShopPrices.BuyLineSize;
        var bodySize = payload.Length - ShopPrices.BuyHeaderSize;
        if (offer.SellsSkillCards ? linesSize > bodySize : linesSize != bodySize) return Opcode.S2C_SHOP_BUY_REFUSED;
        if (lines is < 1 or > ShopPrices.MaxLines) return Opcode.S2C_SHOP_BUY_REFUSED;
        if (_player.Character() is not { } character || _player.Stats() is not { } stats) return Opcode.S2C_SHOP_BUY_REFUSED;

        var orders = new List<(int ItemId, int Count)>(lines);
        for (var line = 0; line < lines; line++)
        {
            var at = ShopPrices.BuyHeaderSize + line * ShopPrices.BuyLineSize;
            var itemId = (int)BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(at));
            var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(at + 4));
            if (itemId == 0 || count == 0) return Opcode.S2C_SHOP_BUY_REFUSED;
            orders.Add((itemId, count));
        }

        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var bagWeight = bag.Sum(item => WeightOf(item.ItemIndex) * item.WeightUnits);
        if (orders.Sum(order => WeightOf(order.ItemId) * order.Count) + bagWeight > _player.MaxWeight()) return Opcode.S2C_SHOP_BUY_TOO_HEAVY;
        if (PickupRules.BagCapacity - bag.Count < lines) return Opcode.S2C_SHOP_BUY_REFUSED;

        long total = 0;
        foreach (var (itemId, count) in orders)
        {
            if (offer.Goods.FirstOrDefault(row => row.ItemId == itemId) is not { } row) return Opcode.S2C_SHOP_BUY_REFUSED;
            var cost = (long)BuyUnitPrice(offer, itemId, row) * count;
            total += cost;
            if (cost > int.MaxValue || total > int.MaxValue) return Opcode.S2C_SHOP_BUY_REFUSED;
        }
        if (total > stats.Money) return Opcode.S2C_SHOP_BUY_REFUSED;

        var operations = new List<BagOperation>(orders.Count);
        foreach (var (itemId, count) in orders) operations.Add(Grant(character.CharacterId, itemId, count));
        stats.GrantMoney(-total, StatChangeSource.Item);
        await SendAsync(Opcode.S2C_SHOP_BOUGHT, ShopFrames.Traded(stats.Money, operations), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   shop 0x{shopEntityId:X4}: bought {string.Join(", ", orders.Select(o => $"{o.ItemId} x{o.Count}"))} for {total}, money {stats.Money}");
        return null;
    }

    public async Task<(Opcode, byte[])?> TrySellAsync(byte[] body, Func<ushort, ShopOffer?> offerOf, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(offerOf);
        var refused = (Opcode.S2C_SHOP_SELL_REFUSED, Array.Empty<byte>());
        if (body.Length < SellHeaderSize) return refused;
        var shopEntityId = BinaryPrimitives.ReadUInt16LittleEndian(body);
        var lines = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(2));
        if (offerOf(shopEntityId) is not { } offer) return refused;
        if (lines is < 1 or > ShopPrices.MaxLines || body.Length < SellHeaderSize + lines * SellLineSize) return refused;
        if (_player.Character() is not { } character || _player.Stats() is not { } stats) return refused;

        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var sales = new List<(Item Held, ItemParamEntry Definition, int Count)>(lines);
        var seen = new HashSet<ulong>();
        for (var line = 0; line < lines; line++)
        {
            var at = SellHeaderSize + line * SellLineSize;
            var uid = BagItemIds.Stored(BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(at)));
            var itemId = (int)BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(at + 8));
            int count = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(at + 12));
            if (itemId == 0 || _definitions.GetById(itemId) is not { } named) return refused;
            if ((named.Options & ShopPrices.NotSellableOption) != 0) return refused;
            if (count < 1 || (count >= 2 && !named.IsStackable)) count = 1;
            if (!seen.Add(uid)) return refused;
            if (bag.FirstOrDefault(item => item.ItemUid == uid) is not { } held || held.Stack < count) return refused;
            if (_definitions.GetById(held.ItemIndex) is not { } definition) return refused;
            if (definition.Type == ShopPrices.DrillType)
            {
                Console.WriteLine($"{_logTag} {_ctx.Remote}   shop: selling drills is not implemented (item {itemId}); refused");
                return refused;
            }
            sales.Add((held, definition, count));
        }

        long total = 0;
        foreach (var (held, definition, count) in sales)
        {
            var row = offer.Goods.FirstOrDefault(entry => entry.ItemId == held.ItemIndex);
            total += (long)ShopPrices.SellUnitPrice(offer.PriceRate, definition.Value, row, offer.SellingRate) * count;
        }
        if (!offer.SellsSkillCards && !PickupRules.MoneyFits(stats.Money, total, stats.WalletLimit)) return (Opcode.S2C_MONEY_FULL, ShopFrames.MoneyFull());

        var operations = new List<BagOperation>(sales.Count);
        foreach (var (held, definition, count) in sales)
        {
            var wireUid = BagItemIds.OnWire(held.ItemUid);
            if (held.Stack > count)
            {
                var left = _items.AdjustStack(held.ItemUid, -count);
                operations.Add(new BagOperation(QuestResultFrames.StackChanged, definition.ObjectClass, (uint)held.ItemIndex, wireUid, left));
            }
            else
            {
                _items.Delete(held.ItemUid);
                operations.Add(new BagOperation(QuestResultFrames.Removed, definition.ObjectClass, (uint)held.ItemIndex, wireUid, 0));
            }
        }
        stats.GrantMoney(total, StatChangeSource.Item);
        await SendAsync(Opcode.S2C_SHOP_SOLD, ShopFrames.Traded(stats.Money, operations), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   shop 0x{shopEntityId:X4}: sold {string.Join(", ", sales.Select(s => $"{s.Held.ItemIndex} x{s.Count}"))} for {total}, money {stats.Money}");
        return null;
    }

    private int BuyUnitPrice(ShopOffer offer, int itemId, ShopGoodsEntry goods) =>
        offer.SellsSkillCards
            ? ShopPrices.SkillCardBuyUnitPrice(offer.PriceRate, ValueOf(itemId), goods, offer.PurchaseRate)
            : ShopPrices.BuyUnitPrice(offer.PriceRate, ValueOf(itemId), goods, offer.PurchaseRate);

    private BagOperation Grant(ulong characterId, int itemId, int count)
    {
        var definition = _definitions.GetById(itemId);
        var itemClass = definition?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        var stack = definition is { IsStackable: true }
            ? _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).FirstOrDefault(held => held.ItemIndex == itemId)
            : null;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, count);
            return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)itemId, BagItemIds.OnWire(stack.ItemUid), total);
        }
        var minted = _itemManager.GrantRewardItem(characterId, itemId, (ushort)count, itemClassOverride: itemClass);
        return new BagOperation(QuestResultFrames.Created, itemClass, (uint)itemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot);
    }

    private int ValueOf(int itemId) => _definitions.GetById(itemId)?.Value ?? 0;

    private int WeightOf(int itemId) => _definitions.GetById(itemId)?.Weight ?? 0;

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
