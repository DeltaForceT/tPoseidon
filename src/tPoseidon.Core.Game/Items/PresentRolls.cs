using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public readonly record struct PresentGrant(int ItemId, int Count);

public static class PresentRolls
{
    public const int WeightedType = 0;
    public const int DistinctType = 1;
    public const int InOrderType = 2;
    public const int DistinctTries = 32;
    public const int GiftBoxEffect = 22;
    public const int SmallestCount = 1;

    public static IReadOnlyList<PresentGrant> Roll(PresentItemEntry entry, Func<int, int> randomBelow)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(randomBelow);
        var drops = entry.Drops.Where(drop => drop.ItemId > 0).ToList();
        if (drops.Count == 0 || entry.DropCount <= 0) return [];
        var picked = new List<PresentDrop>(entry.DropCount);
        switch (entry.Type)
        {
            case InOrderType:
                picked.AddRange(drops.Take(entry.DropCount));
                break;
            case DistinctType:
                for (var i = 0; i < entry.DropCount; i++)
                {
                    for (var attempt = 0; attempt < DistinctTries; attempt++)
                    {
                        var candidate = Pick(drops, randomBelow);
                        if (candidate is { } drop && picked.All(held => held.ItemId != drop.ItemId))
                        {
                            picked.Add(drop);
                            break;
                        }
                    }
                }
                break;
            default:
                for (var i = 0; i < entry.DropCount; i++)
                {
                    if (Pick(drops, randomBelow) is { } drop) picked.Add(drop);
                }
                break;
        }
        return picked.Select(drop => new PresentGrant(drop.ItemId, Math.Max(SmallestCount, drop.Count))).ToList();
    }

    public static IReadOnlyList<PresentGrant> Merge(IEnumerable<PresentGrant> grants, Func<int, bool> isStackable)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(isStackable);
        var merged = new List<PresentGrant>();
        foreach (var grant in grants)
        {
            var index = isStackable(grant.ItemId) ? merged.FindIndex(held => held.ItemId == grant.ItemId) : -1;
            if (index >= 0) merged[index] = merged[index] with { Count = merged[index].Count + grant.Count };
            else merged.Add(grant);
        }
        return merged;
    }

    private static PresentDrop? Pick(List<PresentDrop> drops, Func<int, int> randomBelow)
    {
        var total = drops.Sum(drop => Math.Max(0, drop.Rate));
        if (total <= 0) return null;
        var roll = randomBelow(total);
        foreach (var drop in drops)
        {
            var rate = Math.Max(0, drop.Rate);
            if (roll < rate) return drop;
            roll -= rate;
        }
        return null;
    }
}
