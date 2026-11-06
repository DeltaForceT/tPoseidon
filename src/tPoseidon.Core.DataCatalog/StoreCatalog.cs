using tPoseidon.Core.DataCatalog.Families;
using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.DataCatalog.Maps;
using tPoseidon.Core.DataCatalog.Rules;
using tPoseidon.Core.DataCatalog.Store;

namespace tPoseidon.Core.DataCatalog;

public sealed class StoreCatalog
{
    private readonly Lazy<TableIndex> _index;
    private readonly Lazy<ItemCatalog> _items;
    private readonly Lazy<ZoneTables> _zones;

    private readonly Lazy<MonsterTables> _monsters;
    private readonly Lazy<NpcTables> _npcs;
    private readonly Lazy<ShopTables> _shops;
    private readonly Lazy<QuestTables> _quests;
    private readonly Lazy<SkillTables> _skills;
    private readonly Lazy<PetTables> _pets;
    private readonly Lazy<GuardianSoulTables> _guardianSoul;
    private readonly Lazy<ItemSystemTables> _itemSystems;
    private readonly Lazy<WorldSystemTables> _worldSystems;
    private readonly Lazy<SocialSystemTables> _socialSystems;
    private readonly Lazy<TextTables> _texts;

    public StoreCatalog(OpenedStore opened)
    {
        ArgumentNullException.ThrowIfNull(opened);
        Opened = opened;
        Reader = opened.Reader;
        var store = Reader.Store;
        _index = new Lazy<TableIndex>(() => TableIndex.FromStore(store));
        _items = new Lazy<ItemCatalog>(() => ItemCatalog.Load(Reader));
        _zones = new Lazy<ZoneTables>(() => new ZoneTables(store, Index));
        _monsters = new Lazy<MonsterTables>(() => new MonsterTables(Reader));
        _npcs = new Lazy<NpcTables>(() => new NpcTables(Reader));
        _shops = new Lazy<ShopTables>(() => new ShopTables(Reader));
        _quests = new Lazy<QuestTables>(() => new QuestTables(Reader));
        _skills = new Lazy<SkillTables>(() => new SkillTables(Reader));
        _pets = new Lazy<PetTables>(() => new PetTables(Reader));
        _guardianSoul = new Lazy<GuardianSoulTables>(() => new GuardianSoulTables(Reader));
        _itemSystems = new Lazy<ItemSystemTables>(() => new ItemSystemTables(Reader));
        _worldSystems = new Lazy<WorldSystemTables>(() => new WorldSystemTables(Reader));
        _socialSystems = new Lazy<SocialSystemTables>(() => new SocialSystemTables(Reader));
        _texts = new Lazy<TextTables>(() => new TextTables(Reader));
    }

    public static StoreCatalog Open(string libconfigPath, string? cacheDirectory = null, CatalogSupplement? supplement = null)
    {
        var opened = StoreOpener.Open(libconfigPath, cacheDirectory, supplement);
        return new StoreCatalog(opened);
    }

    public OpenedStore Opened { get; }

    public FastLibConfigReader Reader { get; }

    public TableIndex Index => _index.Value;

    public ItemCatalog Items => _items.Value;

    public MapCatalog Maps => Reader.Maps;

    public ZoneTables Zones => _zones.Value;

    public MonsterTables Monsters => _monsters.Value;

    public NpcTables Npcs => _npcs.Value;

    public ShopTables Shops => _shops.Value;

    public QuestTables Quests => _quests.Value;

    public SkillTables Skills => _skills.Value;

    public PetTables Pets => _pets.Value;

    public GuardianSoulTables GuardianSoul => _guardianSoul.Value;

    public ItemSystemTables ItemSystems => _itemSystems.Value;

    public WorldSystemTables WorldSystems => _worldSystems.Value;

    public SocialSystemTables SocialSystems => _socialSystems.Value;

    public TextTables Texts => _texts.Value;
}
