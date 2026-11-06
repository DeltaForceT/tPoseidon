using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class QuestTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<IReadOnlyList<QuestInfoEntry>> _quests;
    private readonly Lazy<IReadOnlyList<QuestResultEntry>> _questResultTables;
    private readonly Lazy<IReadOnlyList<RandomResultTable>> _randomResultTables;
    private readonly Lazy<IReadOnlyList<PreCheckInfoEntry>> _preCheckInfo;
    private readonly Lazy<IReadOnlyList<PartyQuestInfoEntry>> _partyQuestInfo;
    private readonly Lazy<IReadOnlyList<float>> _partyQuestExpRatios;
    private readonly Lazy<IReadOnlyList<MonsterQuestResultEntry>> _partyQuestResultTables;
    private readonly Lazy<IReadOnlyList<QuestItemInfoEntry>> _questItemInfo;

    public QuestTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _quests = new Lazy<IReadOnlyList<QuestInfoEntry>>(reader.LoadAllQuestTables);
        _questResultTables = new Lazy<IReadOnlyList<QuestResultEntry>>(reader.LoadAllQuestResultTables);
        _randomResultTables = new Lazy<IReadOnlyList<RandomResultTable>>(reader.LoadRandomResultTables);
        _preCheckInfo = new Lazy<IReadOnlyList<PreCheckInfoEntry>>(reader.LoadPreCheckInfo);
        _partyQuestInfo = new Lazy<IReadOnlyList<PartyQuestInfoEntry>>(reader.LoadPartyQuestInfo);
        _partyQuestExpRatios = new Lazy<IReadOnlyList<float>>(reader.LoadPartyQuestExpRatios);
        _partyQuestResultTables = new Lazy<IReadOnlyList<MonsterQuestResultEntry>>(reader.LoadPartyQuestResultTables);
        _questItemInfo = new Lazy<IReadOnlyList<QuestItemInfoEntry>>(reader.LoadQuestItemInfo);
    }

    public IReadOnlyList<QuestInfoEntry> Quests => _quests.Value;

    public IReadOnlyList<QuestResultEntry> QuestResultTables => _questResultTables.Value;

    public IReadOnlyList<RandomResultTable> RandomResultTables => _randomResultTables.Value;

    public IReadOnlyList<PreCheckInfoEntry> PreCheckInfo => _preCheckInfo.Value;

    public IReadOnlyList<PartyQuestInfoEntry> PartyQuestInfo => _partyQuestInfo.Value;

    public IReadOnlyList<float> PartyQuestExpRatios => _partyQuestExpRatios.Value;

    public IReadOnlyList<MonsterQuestResultEntry> PartyQuestResultTables => _partyQuestResultTables.Value;

    public IReadOnlyList<QuestItemInfoEntry> QuestItemInfo => _questItemInfo.Value;
}
