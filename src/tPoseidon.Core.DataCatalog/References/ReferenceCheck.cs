using System.Globalization;
using tPoseidon.Core.DataCatalog.Store;

namespace tPoseidon.Core.DataCatalog.References;

public static class ReferenceCheck
{
    private const string IdColumn = "ID";
    private const string QuestIdColumn = "Id";
    private const string SkillTable = "SkillParam2";
    private const string MapTable = "MapInfoEx";
    private const string MonsterTable = "MonsterParamEx2";
    private const string TacticsTable = "Monster_TacticsEX";
    private const string TacticsNameColumn = "TacticsName";
    private static readonly string[] QuestTables = ["QuestInfo", "MonsterQuestInfo", "ScenarioQuestInfo", "PartyMonQuestInfo"];
    private static readonly string[] MonsterDropTables = ["MonItemDropInfo", "MonItemTable_DropInfo"];

    public static ReferenceReport Run(TableStore store) => Run(store, ReferenceRules.All);

    public static ReferenceReport Run(TableStore store, IReadOnlyList<ReferenceRule> rules)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rules);
        var tableNames = new HashSet<string>(store.Tables.Select(table => table.Name), StringComparer.Ordinal);
        var tableNamesIgnoringCase = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);
        var tacticsNames = new Lazy<HashSet<string>>(() =>
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (store.TryFind(TacticsTable, out var tactics))
            {
                store.ForEachRow(tactics, row =>
                {
                    if (row.TryGetValue(TacticsNameColumn, out var name)) names.Add(name.Trim());
                });
            }
            return names;
        });
        var idSets = new Dictionary<ReferenceTarget, HashSet<int>>();
        HashSet<int> IdsOf(ReferenceTarget target)
        {
            if (idSets.TryGetValue(target, out var existing)) return existing;
            var ids = new HashSet<int>();
            switch (target)
            {
                case ReferenceTarget.Item:
                    foreach (var table in ItemTables.All) CollectIds(store, ItemTables.NameOf(table), IdColumn, ids);
                    break;
                case ReferenceTarget.Monster:
                    CollectIds(store, MonsterTable, IdColumn, ids);
                    break;
                case ReferenceTarget.Skill:
                    CollectIds(store, SkillTable, IdColumn, ids);
                    break;
                case ReferenceTarget.Map:
                    CollectIds(store, MapTable, IdColumn, ids);
                    break;
                case ReferenceTarget.Quest:
                    foreach (var questTable in QuestTables) CollectIds(store, questTable, QuestIdColumn, ids);
                    break;
                case ReferenceTarget.MonsterDropRow:
                    foreach (var dropTable in MonsterDropTables) CollectIds(store, dropTable, IdColumn, ids);
                    break;
                case ReferenceTarget.Table:
                case ReferenceTarget.TacticsGroup:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target));
            }
            idSets[target] = ids;
            return ids;
        }

        var unresolved = new List<UnresolvedReference>();
        var caseOnly = new List<UnresolvedReference>();
        var checkedCount = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in store.Tables)
        {
            if (!seen.Add(table.Name)) continue;
            var active = new List<(string Column, ReferenceTarget Target)>();
            foreach (var rule in rules)
            {
                if (!rule.Tables.IsMatch(table.Name)) continue;
                foreach (var column in table.Columns)
                {
                    if (rule.Columns.IsMatch(column)) active.Add((column, rule.Target));
                }
            }
            if (active.Count == 0) continue;
            var rowNumber = 0;
            store.ForEachRow(table, row =>
            {
                rowNumber++;
                foreach (var (column, target) in active)
                {
                    if (!row.TryGetValue(column, out var raw)) continue;
                    var value = raw.Trim();
                    if (value.Length == 0) continue;
                    if (target is ReferenceTarget.Table or ReferenceTarget.TacticsGroup)
                    {
                        checkedCount++;
                        if (tableNames.Contains(value) || (target == ReferenceTarget.TacticsGroup && tacticsNames.Value.Contains(value))) continue;
                        (tableNamesIgnoringCase.Contains(value) ? caseOnly : unresolved).Add(new UnresolvedReference(table.Name, rowNumber, column, value, target));
                        continue;
                    }
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    {
                        checkedCount++;
                        unresolved.Add(new UnresolvedReference(table.Name, rowNumber, column, value, target));
                        continue;
                    }
                    if (id <= 0) continue;
                    checkedCount++;
                    if (!IdsOf(target).Contains(id)) unresolved.Add(new UnresolvedReference(table.Name, rowNumber, column, value, target));
                }
            });
        }
        return new ReferenceReport(checkedCount, unresolved, caseOnly);
    }

    private static void CollectIds(TableStore store, string tableName, string column, HashSet<int> ids)
    {
        if (!store.TryFind(tableName, out var table)) return;
        store.ForEachRow(table, row =>
        {
            if (row.TryGetValue(column, out var raw) && int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) ids.Add(id);
        });
    }
}
