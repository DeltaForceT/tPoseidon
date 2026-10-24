using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public sealed class SkillDefinition
{
    public const int PassiveType = 1;
    public const int NotUsableOption = 0x10;
    public const int CancelledByDamageOption = 0x4;
    public const int NeedsHolderOption = 0x80;
    public const int ForbiddenWhileFlaggedOption = 0x100;
    public const int NotInTownOption = 0x200;
    public const int LowestLevel = 1;
    public const int HighestLevel = 11;
    public const int NoTargetInput = 0;
    public const int ObjectTargetInput = 1;
    public const int ChaseCondition = 2;
    public const int NormalForm = 1;

    public const string DelayColumn = "Delay";
    public const string RangeColumn = "Range";
    public const string CoolTimeColumn = "CoolTime";
    public const string HpCostColumn = "HpCon";
    public const string MpCostColumn = "MpCon";
    public const string MinLevelColumn = "MinLv";
    public const string MaxLevelColumn = "MaxLv";
    public const string ProbabilityColumn = "Probability";

    private readonly SkillTableRow?[] _conditions;
    private readonly SkillTableRow?[] _actions;

    public SkillDefinition(
        SkillParam2Entry row,
        IReadOnlyList<SkillTableRow> conditionRows,
        IReadOnlyList<SkillTableRow> actionRows)
    {
        ArgumentNullException.ThrowIfNull(row);
        Row = row;
        var entries = Math.Max(0, row.MaxSkillLevel) + 1;
        _conditions = PerLevel(conditionRows, entries);
        _actions = PerLevel(actionRows, entries);
    }

    public SkillParam2Entry Row { get; }

    public int Id => Row.Id;

    public int ActionId => Row.EsAction;

    public bool IsPassive => Row.Type == PassiveType;

    public bool AsksForTarget => Row.TargetInput != NoTargetInput;

    public bool Has(int option) => (Row.SkillOption & option) != 0;

    public bool HasLevel(int level) => level >= 0 && level < _conditions.Length;

    public SkillTableRow? ConditionsAt(int level) => HasLevel(level) ? _conditions[level] : null;

    public SkillTableRow? ActionAt(int level) => HasLevel(level) ? _actions[level] : null;

    private static SkillTableRow?[] PerLevel(IReadOnlyList<SkillTableRow> rows, int entries)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var perLevel = new SkillTableRow?[entries];
        SkillTableRow? previous = null;
        for (var level = LowestLevel; level < entries; level++)
        {
            SkillTableRow? own = null;
            foreach (var row in rows)
            {
                if (row.Level == level) own = row;
            }
            previous = own ?? previous;
            perLevel[level] = previous;
        }
        return perLevel;
    }
}

public sealed class SkillBook
{
    private readonly Dictionary<int, SkillDefinition> _byId = new();

    public SkillBook(
        IEnumerable<SkillParam2Entry> skills,
        IEnumerable<SkillTableRow> conditionRows,
        IEnumerable<SkillTableRow> actionRows)
    {
        ArgumentNullException.ThrowIfNull(skills);
        var conditionsByTable = ByTable(conditionRows);
        var actionsByTable = ByTable(actionRows);
        foreach (var skill in skills)
        {
            _byId[skill.Id] = new SkillDefinition(
                skill,
                conditionsByTable.TryGetValue(skill.EsaiiTable, out var conditions) ? conditions : Array.Empty<SkillTableRow>(),
                actionsByTable.TryGetValue(skill.EsaTable, out var actions) ? actions : Array.Empty<SkillTableRow>());
        }
    }

    public int Count => _byId.Count;

    public SkillDefinition? Find(int skillId) => _byId.TryGetValue(skillId, out var skill) ? skill : null;

    public const int InvincibleCastingAbility = 3;

    public bool GrantsAbility(IReadOnlyDictionary<int, int> ownedSkills, int ability)
    {
        ArgumentNullException.ThrowIfNull(ownedSkills);
        return ownedSkills.Keys.Any(skillId => Find(skillId) is { IsPassive: true } owned && owned.Row.PSAbility == ability);
    }

    private static Dictionary<string, List<SkillTableRow>> ByTable(IEnumerable<SkillTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var byTable = new Dictionary<string, List<SkillTableRow>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!byTable.TryGetValue(row.Table, out var list))
            {
                list = new List<SkillTableRow>();
                byTable[row.Table] = list;
            }
            list.Add(row);
        }
        return byTable;
    }
}
