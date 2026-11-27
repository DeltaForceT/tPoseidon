using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Drilling;

public static class DrillQuestItems
{
    public static Func<int, bool> Required(IReadOnlyList<Quest> active, IQuestInfoLookup quests)
    {
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(quests);
        var needed = new HashSet<int>();
        foreach (var quest in active)
        {
            if (quest.QuestType is not (QuestTypes.Normal or QuestTypes.TutorialNormal or QuestTypes.Scenario)) continue;
            if (quests.GetById(quest.QuestId) is not { } info) continue;
            Add(needed, info.SrcItem0);
            Add(needed, info.SrcItem1);
            Add(needed, info.SrcItem2);
            Add(needed, info.SrcItem3);
            Add(needed, info.SrcItem4);
            Add(needed, info.SrcItem5);
        }
        return needed.Contains;
    }

    private static void Add(HashSet<int> needed, int itemId)
    {
        if (itemId != 0) needed.Add(itemId);
    }
}
