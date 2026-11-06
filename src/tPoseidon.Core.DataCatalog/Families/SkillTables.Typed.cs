using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class SkillTables
{
    private CatalogTable<int, SkillParam2Entry>? _definitions;
    private CatalogTable<int, SkillCardEntry>? _cardsById;

    public CatalogTable<int, SkillParam2Entry> Definitions => _definitions ??= new CatalogTable<int, SkillParam2Entry>("SkillParam2", SkillParam2, row => row.Id);

    public CatalogTable<int, SkillCardEntry> CardsById => _cardsById ??= new CatalogTable<int, SkillCardEntry>("Item_SkillCardParam", SkillCards, row => row.Id);
}
