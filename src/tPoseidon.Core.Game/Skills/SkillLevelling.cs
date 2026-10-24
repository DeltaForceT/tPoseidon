using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public enum SkillLevelRefusal
{
    NotOwned,
    NoCard,
    NotLearned,
    NotLevelling,
    AboveMaxLevel,
    NotNextLevel,
    NotEnoughTmForMaster,
    NoTmPoints,
    AboveMasterLevel,
}

public static class SkillLevelling
{
    public const int MasterLevel = 11;
    public const int HighestTrainedLevel = 10;
    public const byte RegistrationNotice = 5;
    public const ushort AboveMaxLevelNotice = 5;

    public static bool HasCard(SkillParam2Entry? row, IReadOnlyDictionary<int, SkillCardEntry> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        return row is { SkillCardId: > 0 } && cards.ContainsKey(row.SkillCardId);
    }

    public static SkillLevelRefusal? Check(SkillParam2Entry? row, bool hasCard, int? currentLevel, int level, int tmPoints)
    {
        if (currentLevel is not { } current) return SkillLevelRefusal.NotOwned;
        if (row is null || !hasCard) return SkillLevelRefusal.NoCard;
        if (current == 0) return SkillLevelRefusal.NotLearned;
        if (!row.LevelUp) return SkillLevelRefusal.NotLevelling;
        if (level > row.MaxSkillLevel) return SkillLevelRefusal.AboveMaxLevel;
        if (level != current + 1) return SkillLevelRefusal.NotNextLevel;
        if (level == MasterLevel) return tmPoints >= row.MasterTmPoint ? null : SkillLevelRefusal.NotEnoughTmForMaster;
        if (level <= HighestTrainedLevel) return tmPoints > 0 ? null : SkillLevelRefusal.NoTmPoints;
        return SkillLevelRefusal.AboveMasterLevel;
    }
}
