using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public enum UpgradeWindow
{
    None,
    SlotExtend,
    Melting,
    GhostStone,
    GhostNeo,
    EquipComposing,
    SoulCharge,
    SoulReinforce,
    SoulCraft,
    EquipReform,
    ReformReset,
    PetRetrain,
    PetLevelUp,
    PetReinforce,
    PetReinforceReset,
    PetComposing,
    Decompose,
    EquipMerge,
    ChargeTimer,
    HarconShop,
}

public readonly record struct SlotExtendRequest(ulong EquipmentWireUid, ulong MaterialWireUid, IReadOnlyList<ulong> SubItemWireUids);

public sealed record SlotExtendCandidate(
    ItemParamEntry? Equipment,
    byte Slots,
    SlotExtendMaterialEntry? Material,
    IReadOnlyList<SlotExtendSubItemEntry?> SubItems);

public static class SlotExtendRules
{
    public const byte Ok = 0;
    public const byte InvalidState = 1;
    public const byte NoEquipment = 2;
    public const byte InvalidEquipment = 3;
    public const byte AtMaxSlots = 4;
    public const byte NoFifthSlot = 5;
    public const byte NoMaterial = 6;
    public const byte MaterialNoFifthSlot = 8;
    public const byte NoFirstSubItem = 9;
    public const byte NoSecondSubItem = 11;
    public const byte SameSubItemKind = 13;
    public const byte NoLuckElixir = 14;
    public const byte NoGuard = 15;
    public const byte Unknown = 17;

    public const int EquipmentClass = 2;
    public const int FifthSlotOption = 2048;
    public const int FourSlotCap = 4;
    public const int FiveSlotCap = 5;
    public const int MaterialFifthSlotFlag = 1;
    public const int MaterialLuckElixirFlag = 2;
    public const int MaterialGuardFlag = 4;
    public const int LuckElixirType = 0;
    public const int GuardType = 1;
    public const int MaxSubItems = 2;
    public const int FixedUids = 2;
    public const double PercentRange = 100.0;

    public static int CapOf(ItemParamEntry equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        return (equipment.ExtendedOptions & FifthSlotOption) != 0 ? FiveSlotCap : FourSlotCap;
    }

    public static byte Check(SlotExtendCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Equipment is not { } equipment) return NoEquipment;
        if (equipment.Class != EquipmentClass || equipment.CompoundSlot == 0) return InvalidEquipment;
        if (candidate.Slots >= FiveSlotCap) return AtMaxSlots;
        if (candidate.Slots >= CapOf(equipment)) return NoFifthSlot;
        var target = candidate.Slots + 1;
        if (candidate.Material is not { } material) return NoMaterial;
        if (target == FiveSlotCap && (material.UsableFlag & MaterialFifthSlotFlag) == 0) return MaterialNoFifthSlot;
        if (candidate.SubItems.Count > MaxSubItems) return SameSubItemKind;
        if (candidate.SubItems.Count > 0 && candidate.SubItems[0] is null) return NoFirstSubItem;
        if (candidate.SubItems.Count > 1 && candidate.SubItems[1] is null) return NoSecondSubItem;
        var subs = candidate.SubItems.OfType<SlotExtendSubItemEntry>().ToList();
        if (subs.Select(sub => sub.SubItemType).Distinct().Count() != subs.Count) return SameSubItemKind;
        if (subs.Any(sub => sub.SubItemType == LuckElixirType) && (material.UsableFlag & MaterialLuckElixirFlag) == 0) return NoLuckElixir;
        if (subs.Any(sub => sub.SubItemType == GuardType) && (material.UsableFlag & MaterialGuardFlag) == 0) return NoGuard;
        return Ok;
    }

    public static double Chance(SlotExtendMaterialEntry material, IEnumerable<SlotExtendSubItemEntry> subItems, int targetSlot)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(subItems);
        var column = targetSlot - 1;
        if (column < 0 || column >= material.SlotRatio.Count) return 0;
        var add = subItems.Where(sub => sub.SubItemType == LuckElixirType && column < sub.SlotRatioAdd.Count).Sum(sub => (double)sub.SlotRatioAdd[column]);
        return material.SlotRatio[column] + add;
    }

    public static bool Succeeds(double chance, double roll) => roll * PercentRange < chance;

    public static bool Guarded(IEnumerable<SlotExtendSubItemEntry> subItems)
    {
        ArgumentNullException.ThrowIfNull(subItems);
        return subItems.Any(sub => sub.SubItemType == GuardType);
    }
}
