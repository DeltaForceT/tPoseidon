using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class SkillTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<IReadOnlyList<SkillParam2Entry>> _skillParam2;
    private readonly Lazy<IReadOnlyList<SkillParam2Entry>> _runeSkills;
    private readonly Lazy<IReadOnlyList<SkillCardEntry>> _skillCards;
    private readonly Lazy<IReadOnlyList<SkillScrollEntry>> _skillScrolls;
    private readonly Lazy<IReadOnlyList<SkillMasterEntry>> _skillMasterInfo;
    private readonly Lazy<IReadOnlyDictionary<int, IReadOnlyList<(int SkillId, int Level)>>> _skillChecks;
    private readonly Lazy<IReadOnlyList<SkillTableRow>> _skillActionRows;
    private readonly Lazy<IReadOnlyList<SkillTableRow>> _skillInvocationRows;
    private readonly Lazy<IReadOnlyDictionary<int, ImprintEntry>> _imprints;

    public SkillTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _skillParam2 = new Lazy<IReadOnlyList<SkillParam2Entry>>(reader.LoadSkillParam2);
        _runeSkills = new Lazy<IReadOnlyList<SkillParam2Entry>>(reader.LoadRuneSkills);
        _skillCards = new Lazy<IReadOnlyList<SkillCardEntry>>(reader.LoadSkillCards);
        _skillScrolls = new Lazy<IReadOnlyList<SkillScrollEntry>>(reader.LoadSkillScrolls);
        _skillMasterInfo = new Lazy<IReadOnlyList<SkillMasterEntry>>(reader.LoadSkillMasterInfo);
        _skillChecks = new Lazy<IReadOnlyDictionary<int, IReadOnlyList<(int SkillId, int Level)>>>(reader.LoadSkillChecks);
        _skillActionRows = new Lazy<IReadOnlyList<SkillTableRow>>(reader.LoadSkillActionRows);
        _skillInvocationRows = new Lazy<IReadOnlyList<SkillTableRow>>(reader.LoadSkillInvocationRows);
        _imprints = new Lazy<IReadOnlyDictionary<int, ImprintEntry>>(reader.LoadImprints);
    }

    public IReadOnlyList<SkillParam2Entry> SkillParam2 => _skillParam2.Value;

    public IReadOnlyList<SkillParam2Entry> RuneSkills => _runeSkills.Value;

    public IReadOnlyList<SkillCardEntry> SkillCards => _skillCards.Value;

    public IReadOnlyList<SkillScrollEntry> SkillScrolls => _skillScrolls.Value;

    public IReadOnlyList<SkillMasterEntry> SkillMasterInfo => _skillMasterInfo.Value;

    public IReadOnlyDictionary<int, IReadOnlyList<(int SkillId, int Level)>> SkillChecks => _skillChecks.Value;

    public IReadOnlyList<SkillTableRow> SkillActionRows => _skillActionRows.Value;

    public IReadOnlyList<SkillTableRow> SkillInvocationRows => _skillInvocationRows.Value;

    public IReadOnlyDictionary<string, IReadOnlyList<int>> SkillIdLists(IReadOnlySet<string> tableNames) => _reader.LoadSkillIdLists(tableNames);

    public IReadOnlyDictionary<int, ImprintEntry> Imprints => _imprints.Value;
}
