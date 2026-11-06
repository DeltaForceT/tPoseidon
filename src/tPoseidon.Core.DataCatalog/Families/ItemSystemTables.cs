using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class ItemSystemTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<IReadOnlyList<MoveSpeedParamEntry>> _moveSpeedParam;
    private readonly Lazy<IReadOnlyList<SpeedItemParamEntry>> _speedItemParam;
    private readonly Lazy<IReadOnlyList<PresentItemEntry>> _presentItems;
    private readonly Lazy<IReadOnlyDictionary<int, ExpBoostEntry>> _expBoosts;
    private readonly Lazy<BoxTables> _boxTables;
    private readonly Lazy<SpecialPortTables> _specialPortTables;
    private readonly Lazy<FortuneTables> _fortuneTables;
    private readonly Lazy<MeltingTables> _meltingTables;
    private readonly Lazy<EquipReformTables> _equipReformTables;
    private readonly Lazy<IReadOnlyList<RecycleChangeEntry>> _recycleChanges;
    private readonly Lazy<IReadOnlySet<int>> _recycleExceptItems;
    private readonly Lazy<IReadOnlySet<int>> _recycleExceptMyCampItems;
    private readonly Lazy<IReadOnlyList<TransScrollEntry>> _transScrolls;
    private readonly Lazy<IReadOnlyList<RebirthItemEntry>> _rebirthItems;
    private readonly Lazy<IReadOnlyList<SetItemEntry>> _setItems;
    private readonly Lazy<IReadOnlyList<MyCampItemEntry>> _myCampItems;
    private readonly Lazy<IReadOnlyDictionary<int, ItemStatCheckEntry>> _itemStatChecks;
    private readonly Lazy<GrowthTables> _growthTables;
    private readonly Lazy<RefineTables> _refineTables;
    private readonly Lazy<ItemPropertyTables> _itemPropertyTables;
    private readonly Lazy<IReadOnlyDictionary<int, int>> _generalTimerItems;
    private readonly Lazy<IReadOnlyList<UniqueItemCountEntry>> _uniqueItemCountTable;
    private readonly Lazy<IReadOnlyList<LegendItemGroupEntry>> _legendItemGroups;
    private readonly Lazy<IReadOnlyList<LegendItemListEntry>> _legendItemList;
    private readonly Lazy<IReadOnlyList<RandomStatValueEntry>> _randomStatValueTable;

    public ItemSystemTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _moveSpeedParam = new Lazy<IReadOnlyList<MoveSpeedParamEntry>>(reader.LoadMoveSpeedParam);
        _speedItemParam = new Lazy<IReadOnlyList<SpeedItemParamEntry>>(reader.LoadSpeedItemParam);
        _presentItems = new Lazy<IReadOnlyList<PresentItemEntry>>(reader.LoadPresentItems);
        _expBoosts = new Lazy<IReadOnlyDictionary<int, ExpBoostEntry>>(reader.LoadExpBoosts);
        _boxTables = new Lazy<BoxTables>(reader.LoadBoxTables);
        _specialPortTables = new Lazy<SpecialPortTables>(reader.LoadSpecialPortTables);
        _fortuneTables = new Lazy<FortuneTables>(reader.LoadFortuneTables);
        _meltingTables = new Lazy<MeltingTables>(reader.LoadMeltingTables);
        _equipReformTables = new Lazy<EquipReformTables>(reader.LoadEquipReformTables);
        _recycleChanges = new Lazy<IReadOnlyList<RecycleChangeEntry>>(reader.LoadRecycleChanges);
        _recycleExceptItems = new Lazy<IReadOnlySet<int>>(reader.LoadRecycleExceptItems);
        _recycleExceptMyCampItems = new Lazy<IReadOnlySet<int>>(reader.LoadRecycleExceptMyCampItems);
        _transScrolls = new Lazy<IReadOnlyList<TransScrollEntry>>(reader.LoadTransScrolls);
        _rebirthItems = new Lazy<IReadOnlyList<RebirthItemEntry>>(reader.LoadRebirthItems);
        _setItems = new Lazy<IReadOnlyList<SetItemEntry>>(reader.LoadSetItems);
        _myCampItems = new Lazy<IReadOnlyList<MyCampItemEntry>>(reader.LoadMyCampItems);
        _itemStatChecks = new Lazy<IReadOnlyDictionary<int, ItemStatCheckEntry>>(reader.LoadItemStatChecks);
        _growthTables = new Lazy<GrowthTables>(reader.LoadGrowthTables);
        _refineTables = new Lazy<RefineTables>(reader.LoadRefineTables);
        _itemPropertyTables = new Lazy<ItemPropertyTables>(reader.LoadItemPropertyTables);
        _generalTimerItems = new Lazy<IReadOnlyDictionary<int, int>>(reader.LoadGeneralTimerItems);
        _uniqueItemCountTable = new Lazy<IReadOnlyList<UniqueItemCountEntry>>(reader.LoadUniqueItemCountTable);
        _legendItemGroups = new Lazy<IReadOnlyList<LegendItemGroupEntry>>(reader.LoadLegendItemGroups);
        _legendItemList = new Lazy<IReadOnlyList<LegendItemListEntry>>(reader.LoadLegendItemList);
        _randomStatValueTable = new Lazy<IReadOnlyList<RandomStatValueEntry>>(reader.LoadRandomStatValueTable);
    }

    public IReadOnlyList<MoveSpeedParamEntry> MoveSpeedParam => _moveSpeedParam.Value;

    public IReadOnlyList<SpeedItemParamEntry> SpeedItemParam => _speedItemParam.Value;

    public IReadOnlyList<PresentItemEntry> PresentItems => _presentItems.Value;

    public IReadOnlyDictionary<int, ExpBoostEntry> ExpBoosts => _expBoosts.Value;

    public BoxTables BoxTables => _boxTables.Value;

    public SpecialPortTables SpecialPortTables => _specialPortTables.Value;

    public FortuneTables FortuneTables => _fortuneTables.Value;

    public MeltingTables MeltingTables => _meltingTables.Value;

    public EquipReformTables EquipReformTables => _equipReformTables.Value;

    public IReadOnlyList<RecycleChangeEntry> RecycleChanges => _recycleChanges.Value;

    public IReadOnlySet<int> RecycleExceptItems => _recycleExceptItems.Value;

    public IReadOnlySet<int> RecycleExceptMyCampItems => _recycleExceptMyCampItems.Value;

    public IReadOnlyList<TransScrollEntry> TransScrolls => _transScrolls.Value;

    public IReadOnlyList<RebirthItemEntry> RebirthItems => _rebirthItems.Value;

    public IReadOnlyList<SetItemEntry> SetItems => _setItems.Value;

    public IReadOnlyList<MyCampItemEntry> MyCampItems => _myCampItems.Value;

    public IReadOnlyDictionary<int, ItemStatCheckEntry> ItemStatChecks => _itemStatChecks.Value;

    public GrowthTables GrowthTables => _growthTables.Value;

    public RefineTables RefineTables => _refineTables.Value;

    public ItemPropertyTables ItemPropertyTables => _itemPropertyTables.Value;

    public IReadOnlyDictionary<int, int> GeneralTimerItems => _generalTimerItems.Value;

    public IReadOnlyList<UniqueItemCountEntry> UniqueItemCountTable => _uniqueItemCountTable.Value;

    public IReadOnlyList<LegendItemGroupEntry> LegendItemGroups => _legendItemGroups.Value;

    public IReadOnlyList<LegendItemListEntry> LegendItemList => _legendItemList.Value;

    public IReadOnlyList<RandomStatValueEntry> RandomStatValueTable => _randomStatValueTable.Value;

    public IReadOnlyDictionary<int, int> KeyedItems(string tableName, string keyColumn, string itemColumn) => _reader.LoadKeyedItems(tableName, keyColumn, itemColumn);
}
