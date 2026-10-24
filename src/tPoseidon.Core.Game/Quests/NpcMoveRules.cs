using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quests;

public static class NpcMoveRules
{
    public const int Passed = 0;
    public const int ItemsMissing = 1;
    public const int LevelOutside = 2;
    public const int FTypeMismatch = 3;
    public const int CharacterTypeMismatch = 4;
    public const byte ChangeZoneType = 4;
    public const int DestinationPointType = 9;
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(2000);

    public static IReadOnlyList<RequiredItem> Required(NpcMoveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Items.Select(item => new RequiredItem(item.ItemId, item.Count)).ToList();
    }

    public static int Check(NpcMoveEntry entry, IReadOnlyList<Item> bag, int level, int fTypeIndex, int characterType)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(bag);
        if (entry.Items.Count > 0 && !QuestTurnIn.HoldsAll(bag, Required(entry))) return ItemsMissing;
        if (!(entry.MinLevel == 0 && entry.MaxLevel == 0) && (level < entry.MinLevel || level > entry.MaxLevel)) return LevelOutside;
        if (entry.FType != NpcMoveEntry.AnyFType && entry.FType != fTypeIndex) return FTypeMismatch;
        if (entry.CharacterType != NpcMoveEntry.AnyCharacterType && entry.CharacterType != characterType) return CharacterTypeMismatch;
        return Passed;
    }
}
