using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Guardian;

public readonly record struct GuardianCardRecord(int ItemId, ulong ItemUid, int Level, int Slot);

public readonly record struct GuardianCardSaveRequest(uint SkillPoint, IReadOnlyList<GuardianCardRecord> Records);

public static class GuardianCardRules
{
    public const byte NoGroup = 0xFF;
    public const int MaxLevel = 10;
    public const int ForcedLevel = 1;

    public static byte GroupOf(int cardType) => cardType switch
    {
        >= 2 and <= 9 => 0,
        >= 12 and <= 20 => 1,
        >= 23 and <= 30 => 2,
        >= 33 and <= 40 => 3,
        _ => NoGroup,
    };

    public static int StoredLevel(int level) => level is < 1 or > MaxLevel ? ForcedLevel : level;

    public static bool Storable(GuardianCardRecord record) => record.Slot is >= byte.MinValue and <= byte.MaxValue;

    public static GuardianSkill? Reserve(SkillCardEntry? card, GuardianCardRecord record)
    {
        if (card is null) return null;
        return new GuardianSkill(card.AffectionSkill, GuardianSkillType.Card, GroupOf(card.CardType), (short)StoredLevel(record.Level), (byte)record.Slot);
    }

    public static GuardianSkill? Raw(SkillCardEntry? card, GuardianCardRecord record)
    {
        if (card is null) return null;
        return new GuardianSkill(card.AffectionSkill, GuardianSkillType.Card, GroupOf(card.CardType), (short)record.Level, (byte)record.Slot);
    }

    public static GuardianState Replace(GuardianState guardian, int skillPoint, IReadOnlyList<GuardianSkill> cards)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(cards);
        var kept = guardian.Skills.Where(skill => skill.Type != GuardianSkillType.Card).Concat(cards).ToArray();
        return guardian with { Skills = kept, SkillPoint = skillPoint };
    }
}
