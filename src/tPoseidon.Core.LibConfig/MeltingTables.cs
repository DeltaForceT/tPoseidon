namespace tPoseidon.Core.LibConfig;

public sealed record MeltingInfoEntry(
    float ItemGradeRate,
    float CompoundSlotRate,
    float CompoundValueRate,
    float ItemValueRate,
    float CraftGrade,
    int CmOption);

public sealed record MeltingDropEntry(int Id, int DropItem, int MeltingCost, int MinValue, int MaxValue);

public sealed record MeltingTables(MeltingInfoEntry? Info, IReadOnlyList<MeltingDropEntry> Drops, int RandomStatMinPercent, int RandomStatMaxPercent)
{
    public const string InfoTable = "MeltingInfo";
    public const string DropTable = "MeltingDropInfo";
    public const string RandomStatRatioTable = "EquipReformRandomStatRatio";

    public static MeltingTables Empty { get; } = new(null, Array.Empty<MeltingDropEntry>(), 0, 0);
}
