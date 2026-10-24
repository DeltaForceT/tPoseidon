using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public readonly record struct DropSource(
    ushort EntityId,
    int MonsterTypeId,
    ushort MapId,
    ushort X,
    ushort Y,
    ushort OwnerEntityId,
    DateTimeOffset At);

public static class MonsterDropBatch
{
    public const int FirstHiddenDropper = 16000;
    public const int LastHiddenDropper = 16999;

    public static bool DropsHidden(int monsterTypeId) =>
        monsterTypeId is >= FirstHiddenDropper and <= LastHiddenDropper;

    public static IReadOnlyList<FieldItem> Items(
        DropSource source,
        IReadOnlyList<RolledDrop> drops,
        IItemParamLookup items,
        Func<ushort> newEntityId,
        ZoneGeometry? geometry,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(drops);
        if (drops.Count == 0) return Array.Empty<FieldItem>();
        return Place(
            source, drops, _ => drops[0].Count, DropsHidden(source.MonsterTypeId),
            DropPlacement.ItemFirstRing, DropPlacement.ItemReach, items, newEntityId, geometry, rng);
    }

    public static IReadOnlyList<FieldItem> Money(
        DropSource source,
        int amount,
        IItemParamLookup items,
        Func<ushort> newEntityId,
        ZoneGeometry? geometry,
        Random rng)
    {
        var stacks = MonsterDropRoller.SplitMoney(amount);
        if (stacks.Count == 0) return Array.Empty<FieldItem>();
        return Place(
            source, stacks, stack => stack.Count, hidden: false,
            DropPlacement.MoneyFirstRing, DropPlacement.MoneyReach, items, newEntityId, geometry, rng);
    }

    private static List<FieldItem> Place(
        DropSource source,
        IReadOnlyList<RolledDrop> drops,
        Func<RolledDrop, int> countOf,
        bool hidden,
        int firstRing,
        int reach,
        IItemParamLookup items,
        Func<ushort> newEntityId,
        ZoneGeometry? geometry,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(newEntityId);

        var batch = new List<FieldItem>(drops.Count);
        var takenCells = new HashSet<(int X, int Y)>();
        foreach (var drop in drops)
        {
            if (items.GetById(drop.ItemId) is not { } definition) break;
            var (x, y) = DropPlacement.Place(geometry, source.X, source.Y, firstRing, reach, takenCells, rng);
            batch.Add(new FieldItem(
                newEntityId(), source.MapId, x, y, drop.ItemId, countOf(drop), definition.ObjectClass,
                definition.IsMoney, source.EntityId, source.OwnerEntityId, source.At)
            {
                IsHidden = hidden,
            });
        }
        return batch;
    }
}
