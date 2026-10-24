namespace tPoseidon.Core.LibConfig;

public sealed record SoulSystemInfoEntry(
    int PotionItemId,
    int GaugeMax,
    int GaugeMinutes,
    int SourceDecreaseCount,
    int GaugeDecrease,
    int PenaltyMinutes,
    int PenaltyCount);

public sealed record SoulSourceEntry(int SourceItemId, int GuardianType, int GaugeUp);

public sealed record SoulPotionCountEntry(int GuardianLevelMin, int GuardianLevelMax, int Count);

public sealed record SoulEquipmentEntry(int ItemId, int SkillId, int DecreaseSoul, int DecreaseMinutes);

public readonly record struct SoulMaterial(int ItemId, int Count);

public sealed record SoulCraftEntry(int Id, int ResultItemId, int ResultCount, IReadOnlyList<SoulMaterial> Materials, float Probability, int Fee);

public sealed record SoulIntensifyEntry(int Id, int SoulLevel, int WeaponLevel, int Cost, IReadOnlyList<SoulMaterial> Materials, int Ratio);

public sealed record SoulSystemTables(
    SoulSystemInfoEntry? Info,
    IReadOnlyList<SoulSourceEntry> Sources,
    IReadOnlyList<SoulPotionCountEntry> PotionCounts,
    IReadOnlyDictionary<int, SoulEquipmentEntry> Equipment,
    IReadOnlyDictionary<int, SoulCraftEntry> CraftsByResult,
    IReadOnlyList<SoulIntensifyEntry> Intensify,
    IReadOnlySet<int> TartarosMaps)
{
    public const string InfoTable = "SoulSystemInfo";
    public const string SourceTable = "SourceInfo";
    public const string PotionTable = "SoulPotionInfo";
    public const string EquipmentTable = "SoulEquipmentInfo";
    public const string CraftTable = "SoulEquipmentCraftInfo";
    public const string IntensifyTable = "SoulLevelIntensifyInfo";
    public const string TartarosTable = "TartarosMapSkillInfo";
    public const int RequirementColumns = 5;

    public static SoulSystemTables Empty { get; } = new(
        null,
        Array.Empty<SoulSourceEntry>(),
        Array.Empty<SoulPotionCountEntry>(),
        new Dictionary<int, SoulEquipmentEntry>(),
        new Dictionary<int, SoulCraftEntry>(),
        Array.Empty<SoulIntensifyEntry>(),
        new HashSet<int>());
}
