using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public sealed class ItemRechargeCatalogTests
{
    private const string Goods = """
        <?xml version="1.0" encoding="utf-8"?><ROOT><CHARACTER count="2">
        <GOODS goods_code="1956"><GOODS_LIST item_index="1956" item_count="1"/></GOODS>
        <GOODS goods_code="433383"><GOODS_LIST item_index="433383" item_count="1"/></GOODS>
        </CHARACTER></ROOT>
        """;

    private const string Recharges = """
        <?xml version="1.0" encoding="utf-8"?><ROOT><CHARACTER count="1">
        <GOODS goods_code="1956" limit_count="2">
        <RECHARGE_LIST limit_code="1005" limit_use="72" bonus_time="3" required_item="12"/>
        <RECHARGE_LIST limit_code="1009" limit_use="1440" bonus_time="60" required_item="40"/>
        </GOODS>
        <GOODS goods_code="7777" limit_count="1"><RECHARGE_LIST limit_code="1005" limit_use="72" bonus_time="3" required_item="1"/></GOODS>
        </CHARACTER><MYCAMP count="0"/></ROOT>
        """;

    [Fact]
    public void TheRechargeOptionsFollowTheGoodsItems()
    {
        var catalog = ItemRechargeCatalog.Parse(Goods, Recharges);
        Assert.Equal(1, catalog.ItemCount);
        Assert.Equal(new[] { new RechargeOption(1005, 72, 3, 12), new RechargeOption(1009, 1440, 60, 40) }, catalog.For(1956));
        Assert.Empty(catalog.For(433383));
        Assert.Empty(catalog.For(7777));
    }
}
