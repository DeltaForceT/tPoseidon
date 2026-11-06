using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class ShopTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<IReadOnlyList<ShopZoneEntry>> _shopZoneTables;
    private readonly Lazy<IReadOnlyList<SkillShopZoneEntry>> _skillShopZoneTables;
    private readonly Lazy<IReadOnlyList<VipShopOffer>> _vipShopOffers;
    private readonly Lazy<IReadOnlyList<ExchangeOffer>> _exchangeOffers;
    private readonly Lazy<IReadOnlyList<CompounderList>> _compounderLists;
    private readonly Lazy<IReadOnlyList<CompoundRecipe>> _compoundRecipes;
    private readonly Lazy<GuildShopTables> _guildShopTables;
    private readonly Lazy<IReadOnlyList<RecycleShopEntry>> _recycleShop;

    public ShopTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _shopZoneTables = new Lazy<IReadOnlyList<ShopZoneEntry>>(reader.LoadAllShopZoneTables);
        _skillShopZoneTables = new Lazy<IReadOnlyList<SkillShopZoneEntry>>(reader.LoadAllSkillShopZoneTables);
        _vipShopOffers = new Lazy<IReadOnlyList<VipShopOffer>>(reader.LoadVipShopOffers);
        _exchangeOffers = new Lazy<IReadOnlyList<ExchangeOffer>>(reader.LoadExchangeOffers);
        _compounderLists = new Lazy<IReadOnlyList<CompounderList>>(reader.LoadCompounderLists);
        _compoundRecipes = new Lazy<IReadOnlyList<CompoundRecipe>>(reader.LoadCompoundRecipes);
        _guildShopTables = new Lazy<GuildShopTables>(reader.LoadGuildShopTables);
        _recycleShop = new Lazy<IReadOnlyList<RecycleShopEntry>>(reader.LoadRecycleShop);
    }

    public IReadOnlyList<ShopZoneEntry> ShopZoneTables => _shopZoneTables.Value;

    public IReadOnlyList<ShopGoodsEntry> ShopGoods(IReadOnlySet<string> tableNames) => _reader.LoadShopGoods(tableNames);

    public IReadOnlyList<SkillShopZoneEntry> SkillShopZoneTables => _skillShopZoneTables.Value;

    public IReadOnlyList<VipShopOffer> VipShopOffers => _vipShopOffers.Value;

    public IReadOnlyDictionary<string, IReadOnlyList<int>> VipShopLists(IReadOnlySet<string> tableNames) => _reader.LoadVipShopLists(tableNames);

    public IReadOnlyList<ExchangeOffer> ExchangeOffers => _exchangeOffers.Value;

    public IReadOnlyDictionary<string, IReadOnlyList<int>> ExchangeLists(IReadOnlySet<string> tableNames) => _reader.LoadExchangeLists(tableNames);

    public IReadOnlyList<CompounderList> CompounderLists => _compounderLists.Value;

    public IReadOnlyList<CompoundRecipe> CompoundRecipes => _compoundRecipes.Value;

    public GuildShopTables GuildShopTables => _guildShopTables.Value;

    public IReadOnlyList<RecycleShopEntry> RecycleShop => _recycleShop.Value;
}
