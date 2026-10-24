using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed class SetItemBook
{
    private readonly Dictionary<int, List<SetItemEntry>> _byItem = new();

    public SetItemBook(IEnumerable<SetItemEntry> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);
        foreach (var set in sets.Where(s => s.Items.Count > 0))
        {
            foreach (var item in set.Items.Distinct())
            {
                if (!_byItem.TryGetValue(item, out var list)) _byItem[item] = list = new List<SetItemEntry>();
                list.Add(set);
            }
        }
    }

    public IReadOnlyList<SetItemEntry> Active(IEnumerable<int> wornItemIds)
    {
        ArgumentNullException.ThrowIfNull(wornItemIds);
        var worn = wornItemIds.ToHashSet();
        return worn.Where(_byItem.ContainsKey)
            .SelectMany(item => _byItem[item])
            .Distinct()
            .Where(set => set.Items.All(worn.Contains))
            .OrderBy(set => set.Id)
            .ToArray();
    }

    public static int[] Bonus(IEnumerable<SetItemEntry> active)
    {
        ArgumentNullException.ThrowIfNull(active);
        var bonus = new int[EquipmentRules.ItemStatCount];
        foreach (var set in active)
        {
            for (var stat = 0; stat < bonus.Length && stat < set.Bonuses.Count; stat++) bonus[stat] += set.Bonuses[stat];
        }
        return bonus;
    }
}
