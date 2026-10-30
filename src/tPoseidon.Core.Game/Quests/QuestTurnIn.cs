using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quests;

public readonly record struct RequiredItem(int ItemId, int Count);

public readonly record struct UserChoice(int Index, RequiredItem Item);

public static class QuestTurnIn
{
    public const int SingleOnlyOption = 0x1;

    public static IReadOnlyList<RequiredItem> Required(QuestInfoEntry quest, IItemParamLookup items)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(items);
        var required = new List<RequiredItem>();
        Add(required, quest.SrcItem0, quest.Cnt0, items);
        Add(required, quest.SrcItem1, quest.Cnt1, items);
        Add(required, quest.SrcItem2, quest.Cnt2, items);
        Add(required, quest.SrcItem3, quest.Cnt3, items);
        Add(required, quest.SrcItem4, quest.Cnt4, items);
        Add(required, quest.SrcItem5, quest.Cnt5, items);
        return required;
    }

    public static Item? Holding(IReadOnlyList<Item> bag, RequiredItem required)
    {
        ArgumentNullException.ThrowIfNull(bag);
        if (required.Count < 1) return null;
        foreach (var held in bag)
        {
            if (held.ItemIndex == required.ItemId && held.Stack >= required.Count) return held;
        }
        return null;
    }

    public static UserChoice? UserChoiceOf(IReadOnlyList<Item> bag, IReadOnlyList<RequiredItem> required)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(required);
        if (required.Count == 0) return null;
        var carried = bag.Where(held => held.ItemIndex == required[0].ItemId).Sum(held => held.Stack);
        UserChoice? chosen = null;
        for (var index = 0; index < required.Count; index++)
        {
            var record = required[index];
            if (record.Count < 1 || record.Count > carried) continue;
            if (chosen is null || record.Count > chosen.Value.Item.Count) chosen = new UserChoice(index, record);
        }
        return chosen;
    }

    public static bool HoldsAll(IReadOnlyList<Item> bag, IReadOnlyList<RequiredItem> required)
    {
        ArgumentNullException.ThrowIfNull(required);
        foreach (var item in required)
        {
            if (Holding(bag, item) is null) return false;
        }
        return true;
    }

    private static void Add(List<RequiredItem> required, int itemId, int count, IItemParamLookup items)
    {
        if (itemId == 0) return;
        var singleOnly = items.GetById(itemId) is { } definition && (definition.Options & SingleOnlyOption) != 0;
        required.Add(new RequiredItem(itemId, singleOnly ? 1 : count));
    }
}
