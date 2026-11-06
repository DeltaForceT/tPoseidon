using tPoseidon.Core.DataCatalog.Maps;
using tPoseidon.Core.DataCatalog.Rules;
using tPoseidon.Core.DataCatalog.Store;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog;

public sealed class LibConfigCatalog
{
    private readonly Lazy<TableIndex> _index;
    private readonly Lazy<TableStore> _store;
    private readonly Lazy<ZoneTables> _zones;
    private readonly Lazy<ItemCatalog> _items;
    private readonly Lazy<CatalogTable<int, QuestInfoEntry>> _quests;
    private readonly Lazy<CatalogTable<int, MonsterParamEntry>> _monsters;
    private readonly Lazy<MapCatalog> _maps;
    private readonly Lazy<CatalogTable<int, SkillParam2Entry>> _skills;
    private readonly Lazy<CatalogTable<int, SkillCardEntry>> _skillCards;
    private readonly Lazy<CatalogTable<int, MonItemDropInfoEntry>> _drops;
    private readonly Lazy<CatalogTable<int, CharacterInfoEntry>> _characters;

    public LibConfigCatalog(LibConfigXmlReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        Reader = reader;
        _index = new Lazy<TableIndex>(() => TableIndex.Build(reader));
        _store = new Lazy<TableStore>(() => TableStore.Load(TableStore.Build(TableIndex.BytesOf(reader), StoreOpener.SchemaNumber), StoreOpener.SchemaNumber));
        _zones = new Lazy<ZoneTables>(() => new ZoneTables(_store.Value, Index));
        _items = new Lazy<ItemCatalog>(() => ItemCatalog.Load(reader));
        _quests = new Lazy<CatalogTable<int, QuestInfoEntry>>(() => new CatalogTable<int, QuestInfoEntry>("QuestInfo+ScenarioQuestInfo", reader.LoadAllQuestTables(), row => row.Id));
        _monsters = new Lazy<CatalogTable<int, MonsterParamEntry>>(() => new CatalogTable<int, MonsterParamEntry>("MonsterParamEx2", reader.LoadMonsterParamEx2(), row => row.Id));
        _maps = new Lazy<MapCatalog>(() => new MapCatalog(_store.Value, reader.Overrides));
        _skills = new Lazy<CatalogTable<int, SkillParam2Entry>>(() => new CatalogTable<int, SkillParam2Entry>("SkillParam2", reader.LoadSkillParam2(), row => row.Id));
        _skillCards = new Lazy<CatalogTable<int, SkillCardEntry>>(() => new CatalogTable<int, SkillCardEntry>("Item_SkillCardParam", reader.LoadSkillCards(), row => row.Id));
        _drops = new Lazy<CatalogTable<int, MonItemDropInfoEntry>>(() => new CatalogTable<int, MonItemDropInfoEntry>("MonItemDropInfo", reader.LoadMonItemDropInfo(), row => row.Id));
        _characters = new Lazy<CatalogTable<int, CharacterInfoEntry>>(() => new CatalogTable<int, CharacterInfoEntry>("CharacterInfo", reader.LoadCharacterInfo(), row => row.Id));
    }

    public static LibConfigCatalog Open(string libconfigPath) => new(new LibConfigXmlReader(libconfigPath));

    public LibConfigXmlReader Reader { get; }

    public TableIndex Index => _index.Value;

    public ZoneTables Zones => _zones.Value;

    public ItemCatalog Items => _items.Value;

    public CatalogTable<int, QuestInfoEntry> Quests => _quests.Value;

    public IEnumerable<QuestInfoEntry> QuestsFrom(string sourceTable) =>
        Quests.Rows.Where(row => row.SourceTable == sourceTable);

    public CatalogTable<int, MonsterParamEntry> Monsters => _monsters.Value;

    public MapCatalog Maps => _maps.Value;

    public CatalogTable<int, SkillParam2Entry> Skills => _skills.Value;

    public CatalogTable<int, SkillCardEntry> SkillCards => _skillCards.Value;

    public CatalogTable<int, MonItemDropInfoEntry> Drops => _drops.Value;

    public CatalogTable<int, CharacterInfoEntry> Characters => _characters.Value;
}
