using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class QuestTables
{
    private CatalogTable<int, QuestInfoEntry>? _info;

    public CatalogTable<int, QuestInfoEntry> Info => _info ??= new CatalogTable<int, QuestInfoEntry>("QuestInfo+ScenarioQuestInfo", Quests, row => row.Id);

    public IEnumerable<QuestInfoEntry> From(string sourceTable) => Quests.Where(row => row.SourceTable == sourceTable);
}
