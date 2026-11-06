using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quests;

public enum NpcSeason
{
    Spring,
    Summer,
    Autumn,
    Winter,
}

public static class SeasonalNpcRules
{
    private const string TableMarker = "_On";

    public static NpcSeason SeasonOf(DateTime local) => local.Month switch
    {
        >= 3 and <= 5 => NpcSeason.Spring,
        >= 6 and <= 8 => NpcSeason.Summer,
        >= 9 and <= 11 => NpcSeason.Autumn,
        _ => NpcSeason.Winter,
    };

    public static string SuffixOf(NpcSeason season) => season switch
    {
        NpcSeason.Spring => "Sp",
        NpcSeason.Summer => "Su",
        NpcSeason.Autumn => "Au",
        _ => "Wi",
    };

    public static bool TryParse(string table, out string baseName, out NpcSeason season)
    {
        baseName = string.Empty;
        season = NpcSeason.Winter;
        if (string.IsNullOrEmpty(table)) return false;
        var cut = table.LastIndexOf(TableMarker, StringComparison.Ordinal);
        if (cut <= 0) return false;
        var suffix = table[(cut + TableMarker.Length)..];
        foreach (var candidate in Enum.GetValues<NpcSeason>())
        {
            if (!string.Equals(SuffixOf(candidate), suffix, StringComparison.Ordinal)) continue;
            baseName = table[..cut];
            season = candidate;
            return true;
        }
        return false;
    }

    public static IReadOnlyList<NpcTalkEntry> Apply(IReadOnlyList<NpcTalkEntry> rows, IReadOnlyList<QuestNpcMsgEntry> questTables, DateTime local)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(questTables);
        var current = SeasonOf(local);
        var tableByQuest = questTables
            .GroupBy(entry => entry.QuestId)
            .ToDictionary(group => group.Key, group => group.First().NpcTable);
        var questsByTable = questTables
            .GroupBy(entry => entry.NpcTable, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.QuestId).Distinct().ToArray(), StringComparer.Ordinal);
        var result = new List<NpcTalkEntry>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(Rewrite(row, current, tableByQuest, questsByTable));
        }
        return result;
    }

    private static NpcTalkEntry Rewrite(
        NpcTalkEntry row, NpcSeason current, Dictionary<int, string> tableByQuest, Dictionary<string, int[]> questsByTable)
    {
        if (!TryParse(row.MsgTable, out var baseName, out var season) || season == current) return row;
        var target = baseName + TableMarker + SuffixOf(current);
        if (!questsByTable.TryGetValue(target, out var targetQuests) || targetQuests.Length != 1) return row;
        var slots = new[] { row.QuestId0, row.QuestId1, row.QuestId2, row.QuestId3, row.QuestId4 };
        var slot = Array.FindIndex(slots, quest => tableByQuest.TryGetValue(quest, out var table) && string.Equals(table, row.MsgTable, StringComparison.Ordinal));
        if (slot < 0) return row;
        slots[slot] = targetQuests[0];
        return row with
        {
            MsgTable = target,
            QuestId0 = slots[0],
            QuestId1 = slots[1],
            QuestId2 = slots[2],
            QuestId3 = slots[3],
            QuestId4 = slots[4],
        };
    }
}
