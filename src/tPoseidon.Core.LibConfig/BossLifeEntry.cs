namespace tPoseidon.Core.LibConfig;

public readonly record struct BossHealEntry(int LeftLife, int Level, int TimeMs);

public readonly record struct BossSkillEntry(int SkillId, int Level, int LeftLife, int TimeMs);

public sealed record BossLifeEntry
{
    public const string TableName = "BossMonsterparamEx";
    public const int SummonSlotCount = 4;
    public const int HealSlotCount = 2;
    public const int SkillSlotCount = 5;

    public required int MonsterId { get; init; }
    public required int LifeCount { get; init; }
    public required int ResistGrowthMin { get; init; }
    public required int ResistGrowthMax { get; init; }
    public required bool NotEarthQuake { get; init; }
    public required int SummonIntervalMs { get; init; }
    public required int SummonLeftLife { get; init; }
    public required int SummonTotalCount { get; init; }
    public required int SummonMin { get; init; }
    public required int SummonMax { get; init; }
    public required int SummonBType { get; init; }
    public required IReadOnlyList<int> SummonMonsters { get; init; }
    public required IReadOnlyList<BossHealEntry> Heals { get; init; }
    public required IReadOnlyList<BossSkillEntry> Skills { get; init; }
    public required string SummonSpeech { get; init; }
    public required string ResistGrowthSpeech { get; init; }
}
