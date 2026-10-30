namespace tPoseidon.Core.Data;

public sealed record BossLifeProfile(
    int LifeCount,
    int ResistGrowthMin,
    int ResistGrowthMax,
    bool NotEarthQuake,
    int SummonIntervalMs,
    int SummonLeftLife,
    int SummonTotalCount,
    string SummonSpeech,
    string ResistGrowthSpeech,
    int SummonMin = 0,
    int SummonMax = 0,
    int SummonBType = 0,
    IReadOnlyList<int>? SummonMonsters = null,
    IReadOnlyList<BossHealSlot>? Heals = null,
    IReadOnlyList<BossSkillSlot>? Skills = null);

public readonly record struct BossHealSlot(int LeftLife, int Level, int TimeMs);

public readonly record struct BossSkillSlot(int SkillId, int Level, int LeftLife, int TimeMs);
