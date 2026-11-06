using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class MonsterTables
{
    private CatalogTable<int, MonsterParamEntry>? _definitions;
    private CatalogTable<int, MonItemDropInfoEntry>? _dropInfoById;

    public CatalogTable<int, MonsterParamEntry> Definitions => _definitions ??= new CatalogTable<int, MonsterParamEntry>("MonsterParamEx2", MonsterParamEx2, row => row.Id);

    public CatalogTable<int, MonItemDropInfoEntry> DropInfoById => _dropInfoById ??= new CatalogTable<int, MonItemDropInfoEntry>("MonItemDropInfo", MonItemDropInfo, row => row.Id);
}
