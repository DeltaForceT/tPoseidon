using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Shop;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record ShopCatalogs
{
    public required ShopManager Shops { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<ShopGoodsEntry>> GoodsByTable { get; init; }
    public required IReadOnlyDictionary<int, float> PriceRateByMap { get; init; }
    public required IItemParamLookup Items { get; init; }
}

public sealed class NpcShopService
{
    private readonly S2SessionContext _ctx;
    private readonly ShopCatalogs _catalogs;
    private readonly ShopPlayer _player;
    private readonly ShopTrade _trade;
    private readonly Encoding _textEncoding;
    private readonly Random _rng;
    private readonly string _logTag;

    public NpcShopService(
        S2SessionContext ctx,
        ShopCatalogs catalogs,
        ShopPlayer player,
        IItemStore items,
        ItemManager itemManager,
        Encoding textEncoding,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _textEncoding = textEncoding ?? throw new ArgumentNullException(nameof(textEncoding));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _trade = new ShopTrade(ctx, catalogs.Items, player, items, itemManager, rng, logTag);
    }

    public bool IsOpen { get; private set; }

    public async Task<bool> OpenAsync(ushort shopEntityId, CancellationToken ct)
    {
        IsOpen = true;
        if (FindShop(shopEntityId) is not { } shop)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   shop 0x{shopEntityId:X4} not on this map");
            return false;
        }
        await SendAsync(Opcode.S2C_SHOP_OPENED, ShopFrames.Opened(shopEntityId, shop.Script, _textEncoding), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   shop 0x{shopEntityId:X4} (NPC {shop.NpcType}, goods {shop.ItemTable}) opened");
        return true;
    }

    public void Leave() => IsOpen = false;

    public async Task SendBuyPricesAsync(ushort shopEntityId, CancellationToken ct)
    {
        if (!IsOpen) return;
        if (OfferOf(shopEntityId) is not { } offer)
        {
            await SendAsync(Opcode.S2C_SHOP_BUY_PRICES_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(Opcode.S2C_SHOP_BUY_PRICES, ShopFrames.BuyPrices(_trade.BuyPrices(offer)), ct).ConfigureAwait(false);
    }

    public async Task SendSellRatesAsync(ushort shopEntityId, CancellationToken ct)
    {
        if (!IsOpen) return;
        if (OfferOf(shopEntityId) is not { } offer)
        {
            await SendAsync(Opcode.S2C_SHOP_SELL_RATES_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        var (shopRate, rates) = ShopTrade.SellRates(offer);
        await SendAsync(Opcode.S2C_SHOP_SELL_RATES, ShopFrames.SellRates(shopRate, rates), ct).ConfigureAwait(false);
    }

    public async Task BuyAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen) return;
        var refusal = await _trade.TryBuyAsync(payload.ToArray(), OfferOf, ct).ConfigureAwait(false);
        if (refusal is Opcode refused) await SendAsync(refused, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    public async Task SellAsync(byte[] body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!IsOpen) return;
        var refusal = await _trade.TrySellAsync(body, OfferOf, ct).ConfigureAwait(false);
        if (refusal is (Opcode opcode, byte[] refusalPayload)) await SendAsync(opcode, refusalPayload, ct).ConfigureAwait(false);
    }

    private ShopOffer? OfferOf(ushort shopEntityId) => FindShop(shopEntityId) is { } shop
        ? new ShopOffer
        {
            NpcEntityId = shopEntityId,
            Goods = _catalogs.GoodsByTable.TryGetValue(shop.ItemTable, out var goods)
                ? goods.Take(ShopPrices.GoodsLimit).ToList()
                : Array.Empty<ShopGoodsEntry>(),
            PurchaseRate = shop.PurchaseRate,
            SellingRate = shop.SellingRate,
            PriceRate = _catalogs.PriceRateByMap.TryGetValue(_player.MapId(), out var rate) ? rate : 0f,
            SellsSkillCards = false,
        }
        : null;

    private ShopEntity? FindShop(ushort shopEntityId) =>
        _catalogs.Shops.Get(shopEntityId) is { } shop && shop.MapId == _player.MapId() ? shop : null;

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
