using System.Globalization;
using System.Numerics;
using tPoseidon.Core.Data;

namespace tPoseidon.Hosting;

public sealed record ShopGoodsRequest(int GoodsCode, string Name, string Items, int Price, int Hours, byte Category, string Description);

public static class ShopGoodsAdmin
{
    public const byte DefaultItemClass = ChargedItemRules.RareClass;

    public static IReadOnlyList<ShopGoodsItem> ParseItems(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var items = new List<ShopGoodsItem>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var itemClass = DefaultItemClass;
            var body = part;
            var at = body.IndexOf('@');
            if (at >= 0)
            {
                itemClass = ParseNumber<byte>(body[(at + 1)..], "an item class (1 normal, 2 shop item)");
                body = body[..at];
            }

            short count = 1;
            var times = body.IndexOf('x');
            if (times >= 0)
            {
                count = ParseNumber<short>(body[(times + 1)..], "an item count");
                body = body[..times];
            }

            if (itemClass is not (ChargedItemRules.CommonClass or ChargedItemRules.RareClass) || count < 1)
            {
                throw new HostingException($"'{part}' is not a valid item. Use <item id>[x<count>][@<class>], class 1 or 2.");
            }

            items.Add(new ShopGoodsItem(itemClass, ParseNumber<int>(body, "an item id"), count));
        }

        return items.Count == 0 ? throw new HostingException("Give at least one item, for example --items 38001,64321.") : items;
    }

    public static ShopGoods Add(ICashShopStore store, ShopGoodsRequest request)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(request);
        if (request.GoodsCode <= 0) throw new HostingException("The goods code must be a positive number.");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new HostingException("Give the goods a name with --name.");
        if (request.Price < 0) throw new HostingException("The price cannot be negative.");
        if (request.Hours is < 0 or > short.MaxValue) throw new HostingException("The duration must be 0 (permanent) or a number of hours up to 32767.");
        if (request.Category > 1) throw new HostingException("The category is 0 (character items) or 1 (My Camp items).");
        var option = new ShopGoodsOption((short)request.Hours, request.Price, request.Hours > 0 ? ChargedItemRules.TimedFlag : (byte)0, (short)request.Hours, true);
        var goods = new ShopGoods(request.GoodsCode, request.Name.Trim(), request.Description.Trim(), request.Category, 1, true, ParseItems(request.Items), [option]);
        store.SaveGoods(goods);
        return goods;
    }

    public static IReadOnlyList<string> Describe(IReadOnlyList<ShopGoods> goods)
    {
        ArgumentNullException.ThrowIfNull(goods);
        if (goods.Count == 0) return ["The shop has no goods yet. Add one with: tposeidon shop --add <code> --name <text> --items <ids> --price <cash>"];
        return goods.Select(entry =>
        {
            var option = entry.Options.Count > 0 ? entry.Options[0] : null;
            var price = option is null ? "no price" : string.Create(CultureInfo.InvariantCulture, $"{option.Price} cash");
            var period = option is { UseHours: > 0 } ? string.Create(CultureInfo.InvariantCulture, $", {option.UseHours} hours") : ", permanent";
            var items = string.Join(", ", entry.Items.Select(item => string.Create(CultureInfo.InvariantCulture, $"{item.ItemIndex}x{item.ItemCount}@{item.ItemClass}")));
            var kind = entry.Category == 1 ? "My Camp" : "character";
            var state = entry.OnSale ? string.Empty : "  (off sale)";
            return string.Create(CultureInfo.InvariantCulture, $"{entry.GoodsCode}  {entry.Name}  [{kind}]  {price}{period}  items: {items}{state}");
        }).ToList();
    }

    private static T ParseNumber<T>(string text, string what) where T : struct, INumber<T> =>
        T.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new HostingException($"'{text}' is not {what}.");
}
