using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace tPoseidon.Core.LibConfig;

public sealed record RechargeOption(int LimitCode, int LimitUseHours, int BonusHours, int RequiredCoupons);

public sealed class ItemRechargeCatalog
{
    public const string GoodsFile = "libcmgds.dat";
    public const string RechargeFile = "libcmgds_rch.dat";
    public const string Key = "#dyddydwnrrpTwl~";

    private readonly IReadOnlyDictionary<int, IReadOnlyList<RechargeOption>> _byItem;

    public ItemRechargeCatalog(IReadOnlyDictionary<int, IReadOnlyList<RechargeOption>> byItem)
    {
        _byItem = byItem ?? throw new ArgumentNullException(nameof(byItem));
    }

    public static ItemRechargeCatalog Empty { get; } = new(new Dictionary<int, IReadOnlyList<RechargeOption>>());

    public int ItemCount => _byItem.Count;

    public IReadOnlyList<RechargeOption> For(int itemIndex) =>
        _byItem.TryGetValue(itemIndex, out var options) ? options : Array.Empty<RechargeOption>();

    public static ItemRechargeCatalog Load(string goodsDatPath, string rechargeDatPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(goodsDatPath);
        ArgumentException.ThrowIfNullOrEmpty(rechargeDatPath);
        return Parse(Decode(goodsDatPath), Decode(rechargeDatPath));
    }

    public static ItemRechargeCatalog Parse(string goodsXml, string rechargeXml)
    {
        ArgumentNullException.ThrowIfNull(goodsXml);
        ArgumentNullException.ThrowIfNull(rechargeXml);
        var itemsOfGoods = new Dictionary<int, List<int>>();
        foreach (var goods in XDocument.Parse(goodsXml).Descendants("GOODS"))
        {
            var code = Number(goods, "goods_code");
            foreach (var listed in goods.Elements("GOODS_LIST"))
            {
                if (!itemsOfGoods.TryGetValue(code, out var items)) itemsOfGoods[code] = items = new List<int>();
                var item = Number(listed, "item_index");
                if (!items.Contains(item)) items.Add(item);
            }
        }
        var byItem = new Dictionary<int, List<RechargeOption>>();
        foreach (var goods in XDocument.Parse(rechargeXml).Descendants("GOODS"))
        {
            if (!itemsOfGoods.TryGetValue(Number(goods, "goods_code"), out var items)) continue;
            foreach (var recharge in goods.Elements("RECHARGE_LIST"))
            {
                var option = new RechargeOption(
                    Number(recharge, "limit_code"),
                    Number(recharge, "limit_use"),
                    Number(recharge, "bonus_time"),
                    Number(recharge, "required_item"));
                foreach (var item in items)
                {
                    if (!byItem.TryGetValue(item, out var options)) byItem[item] = options = new List<RechargeOption>();
                    if (!options.Contains(option)) options.Add(option);
                }
            }
        }
        return new ItemRechargeCatalog(byItem.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<RechargeOption>)pair.Value));
    }

    private static string Decode(string datPath)
    {
        var text = Encoding.UTF8.GetString(LibConfigDatDecryptor.DecryptBody(File.ReadAllBytes(datPath), Key));
        var end = text.LastIndexOf('>');
        return end < 0 ? string.Empty : text[..(end + 1)];
    }

    private static int Number(XElement element, string attribute) =>
        int.TryParse((string?)element.Attribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
