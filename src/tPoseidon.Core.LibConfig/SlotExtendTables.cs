namespace tPoseidon.Core.LibConfig;

public sealed record SlotExtendMaterialEntry(int Id, IReadOnlyList<float> SlotRatio, int UsableFlag);

public sealed record SlotExtendSubItemEntry(int Id, int SubItemType, IReadOnlyList<float> SlotRatioAdd);

public sealed record SlotExtendTables(
    IReadOnlyDictionary<int, SlotExtendMaterialEntry> Materials,
    IReadOnlyDictionary<int, SlotExtendSubItemEntry> SubItems)
{
    public const string MaterialTable = "Slot_Extend_Item";
    public const string SubItemTable = "Slot_Extend_SubItem";
    public const int SlotColumns = 5;

    public static SlotExtendTables Empty { get; } = new(
        new Dictionary<int, SlotExtendMaterialEntry>(),
        new Dictionary<int, SlotExtendSubItemEntry>());
}
