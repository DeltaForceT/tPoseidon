using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public sealed record ReleasePlan(IReadOnlyList<WornItem> Items, string? Refusal);

public static class ReleaseListRules
{
    public static ReleasePlan Plan(IReadOnlyList<ReleaseEntry> entries, IReadOnlyList<WornItem> worn, int level)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(worn);
        var items = new List<WornItem>(entries.Count);
        foreach (var entry in entries)
        {
            var uid = BagItemIds.Stored(entry.WireItemUid);
            var refusal = entry.ItemClass switch
            {
                EquipmentFrames.RareItemClass => CheckRare(worn, uid, entry.Slot, level, items),
                EquipmentFrames.CommonItemClass => CheckCommon(worn, uid, entry.Slot, items),
                _ => $"class {entry.ItemClass} is neither common nor rare",
            };
            if (refusal is not null) return new ReleasePlan(Array.Empty<WornItem>(), $"item 0x{uid:X}: {refusal}");
        }
        return new ReleasePlan(items, null);
    }

    private static string? CheckRare(IReadOnlyList<WornItem> worn, ulong uid, byte slot, int level, List<WornItem> items)
    {
        if (worn.FirstOrDefault(held => held.Item.ItemUid == uid) is not { } target) return "the item is not worn";
        if (items.Any(planned => planned.Item.ItemUid == uid)) return "the item is listed twice";
        if (EquipmentRules.CheckRelease(target, slot, worn, level) is { } refusal) return refusal.ToString();
        items.Add(target);
        return null;
    }

    private static string? CheckCommon(IReadOnlyList<WornItem> worn, ulong uid, byte slot, List<WornItem> items)
    {
        if (worn.FirstOrDefault(held => held.Item.ItemUid == uid && held.Slot == EquipmentSlot.Common) is not { } target) return "the item is not loaded";
        if (items.Any(planned => planned.Item.ItemUid == uid)) return "the item is listed twice";
        if (EquipmentRules.CheckReleaseCommon(target.Definition, target.Item, slot, worn) is { } refusal) return refusal.ToString();
        items.Add(target);
        return null;
    }
}
