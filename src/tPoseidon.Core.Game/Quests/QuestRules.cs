using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quests;

public static class QuestRules
{
    public const int DefaultMaxCount = 25;
    public const int Slots = 30;
    public const int ScenarioRangeFirst = 1000;
    public const int ScenarioRangeLast = 1022;
    public const int SecondScenarioRangeFirst = 1077;
    public const int SecondScenarioRangeLast = 1999;
    public const int UserChoiceOption = 2;
    public const string ScenarioTable = "ScenarioQuestInfo";

    public static bool IsScenario(QuestInfoEntry quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return string.Equals(quest.SourceTable, ScenarioTable, StringComparison.Ordinal);
    }

    public static bool UsesScenarioMessages(int questId) =>
        questId is >= ScenarioRangeFirst and <= ScenarioRangeLast
        || questId is >= SecondScenarioRangeFirst and <= SecondScenarioRangeLast;

    public static int MaxCountOf(QuestInfoEntry quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return quest.MaxCount < 1 ? DefaultMaxCount : quest.MaxCount & 0xFF;
    }

    public static bool IsLevelValid(QuestInfoEntry quest, int level)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var min = (short)quest.MinLevel;
        var max = (short)quest.MaxLevel;
        if (min < 1 && max < 1) return true;
        return level >= min && level <= max;
    }

    public static QuestResultEntry? ResultRow(IReadOnlyList<QuestResultEntry> rows, int count)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0) return null;
        var chosen = 0;
        var best = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Count <= count && best < rows[i].Count)
            {
                best = rows[i].Count;
                chosen = i;
            }
        }
        return rows[chosen];
    }

    public static bool HoldsSourceSkill(QuestInfoEntry quest, IReadOnlyDictionary<int, int> skills)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(skills);
        if (quest.SrcSkill0 == 0) return true;
        return skills.TryGetValue(quest.SrcSkill0, out var level) && level >= quest.SrcSkillLv0;
    }

    public static int SwappedOutSkill(QuestInfoEntry quest, IReadOnlyDictionary<int, int> skills)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(skills);
        if (quest.SrcSkill0 == 0) return 0;
        return skills.TryGetValue(quest.SrcSkill0, out var level) && level == quest.SrcSkillLv0 ? quest.SrcSkill0 : 0;
    }

    public static QuestResultEntry? UserChoiceRow(IReadOnlyList<QuestResultEntry> rows, int recordIndex)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0) return null;
        foreach (var row in rows)
        {
            if (row.Count == recordIndex) return row;
        }
        return rows[0];
    }

    public static IReadOnlyList<RewardItem> RewardItems(IEnumerable<(int ItemIndex, int Count)> rowItems, IItemParamLookup items)
    {
        ArgumentNullException.ThrowIfNull(rowItems);
        ArgumentNullException.ThrowIfNull(items);
        var rewards = new List<RewardItem>();
        foreach (var (itemIndex, rowCount) in rowItems)
        {
            var singleOnly = items.GetById(itemIndex) is { } definition && (definition.Options & QuestTurnIn.SingleOnlyOption) != 0;
            var count = singleOnly ? 1 : rowCount;
            if (count > 0) rewards.Add(new RewardItem(itemIndex, count));
        }
        return rewards;
    }
}

public readonly record struct RewardItem(int ItemIndex, int Count);
