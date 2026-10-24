namespace tPoseidon.Core.LibConfig;

public sealed record ItemReformEntry(int MinLevel, int MaxLevel, int ItemId, int ItemCount);

public sealed record ReformElixirEntry(int ItemId, int MinShift, int MaxShift);

public sealed record EquipReformTables(
    IReadOnlyList<ItemReformEntry> Materials,
    IReadOnlyDictionary<string, IReadOnlyList<int>> Curves,
    int RandomStatMinPercent,
    int RandomStatMaxPercent,
    IReadOnlyDictionary<int, ReformElixirEntry> Elixirs,
    float CostDiscount)
{
    public const string MaterialTable = "ItemReformTable";
    public const string CurveTable = "EquipReformRandomTable";
    public const string RatioTable = "EquipReformRandomStatRatio";
    public const string ElixirTable = "EquipReform_HelpItem";
    public const string DiscountTable = "EquipReformCostDiscount";

    public static EquipReformTables Empty { get; } = new(
        Array.Empty<ItemReformEntry>(),
        new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal),
        0,
        0,
        new Dictionary<int, ReformElixirEntry>(),
        0f);

    public ItemReformEntry? MaterialFor(int itemLevel) =>
        Materials.FirstOrDefault(row => row.MinLevel <= itemLevel && itemLevel <= row.MaxLevel);
}
