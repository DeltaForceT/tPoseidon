namespace tPoseidon.Core.Game.Quests;

public static class SkillQuestRules
{
    public const byte CheckFunction = 24;
    public const int QuestParameter = 0;
    public const int SkillParameter = 1;
    public const int NotListed = 1;
    public const int ItemsHeld = 2;
    public const int ItemsMissing = 3;
    private const int UnlearnedLevel = 0;

    public static int Check(int questId, int? skillLevel, bool listed, Func<bool?> requiredItemsHeld)
    {
        ArgumentNullException.ThrowIfNull(requiredItemsHeld);
        if (skillLevel is not { } level || level > UnlearnedLevel || questId < 1) return NpcScript.Refused;
        if (!listed) return NotListed;
        return requiredItemsHeld() switch
        {
            true => ItemsHeld,
            false => ItemsMissing,
            null => NpcScript.Refused,
        };
    }
}
