namespace tPoseidon.Core.DataCatalog.Rules;

public static class TableKinds
{
    public const string MonsterPool = "MonsterPool";
    public const string MonsterTactics = "MonsterTactics";
    public const string MonsterLeaderTactics = "MonsterLeaderTactics";
    public const string MapItemZone = "MapItemZone";
    public const string DrillZoneItems = "DrillZoneItems";
    public const string DrillItemTable = "DrillItemTable";
    public const string DrillEventRate = "DrillEventRate";
    public const string SkillAction = "SkillAction";
    public const string CompoundMasterRatio = "CompoundMasterRatio";
    public const string ShopZone = "ShopZone";
    public const string ShopGoods = "ShopGoods";
    public const string Empty = "Empty";
    public const string Unclassified = "Unclassified";
}

public static class TableRules
{
    public const string MonsterPrefix = "Mon_";
    public const string MapItemPrefix = "MapItem_";
    public const string SkillActionPrefix = "ESAction_";
    public const string ShopZonePrefix = "Shop_";
    public const string ShopGoodsPrefix = "ShopItem_";

    public const string DrillZoneItemsTable = "MapItem_All";
    public const string DrillItemTableName = "MapItem_ETC_Table";
    public const string DrillEventRateTable = "MapItem_Event_Add";
    public const string CompoundMasterTable = "ESAction_CompoundMaster";

    private const string PoolColumn = "MonsterID";
    private const string TacticsColumn = "FactorType";
    private const string LeaderColumn = "LeaderTactics";

    public static string KindOf(TableDescriptor table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var name = table.Name;
        if (name.StartsWith(MonsterPrefix, StringComparison.Ordinal)) return MonsterKind(table);
        if (name.StartsWith(MapItemPrefix, StringComparison.Ordinal)) return MapItemKind(name);
        if (name.StartsWith(SkillActionPrefix, StringComparison.Ordinal)) return name == CompoundMasterTable ? TableKinds.CompoundMasterRatio : TableKinds.SkillAction;
        if (name.StartsWith(ShopGoodsPrefix, StringComparison.Ordinal)) return TableKinds.ShopGoods;
        if (name.StartsWith(ShopZonePrefix, StringComparison.Ordinal)) return table.Columns.Count == 0 ? TableKinds.Empty : TableKinds.ShopZone;
        return TableKinds.Unclassified;
    }

    public static IReadOnlyList<TableDescriptor> TablesOfKind(TableIndex index, string kind)
    {
        ArgumentNullException.ThrowIfNull(index);
        return index.Tables.Where(table => KindOf(table) == kind).ToArray();
    }

    private static string MonsterKind(TableDescriptor table)
    {
        if (table.Columns.Count == 0) return TableKinds.Empty;
        if (table.Columns.Contains(PoolColumn)) return TableKinds.MonsterPool;
        if (table.Columns.Contains(TacticsColumn)) return TableKinds.MonsterTactics;
        if (table.Columns.Contains(LeaderColumn)) return TableKinds.MonsterLeaderTactics;
        return TableKinds.Unclassified;
    }

    private static string MapItemKind(string name) => name switch
    {
        DrillZoneItemsTable => TableKinds.DrillZoneItems,
        DrillItemTableName => TableKinds.DrillItemTable,
        DrillEventRateTable => TableKinds.DrillEventRate,
        _ => TableKinds.MapItemZone,
    };
}
