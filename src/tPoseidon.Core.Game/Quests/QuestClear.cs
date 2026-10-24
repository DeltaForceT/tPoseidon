namespace tPoseidon.Core.Game.Quests;

public readonly record struct QuestClear(byte QuestType, int QuestId, int ClearCount, int ElapsedSeconds, int HuntCount)
{
    public const int MillisecondsPerSecond = 1000;

    public static QuestClear Counted(byte questType, int questId, int clearCount) => new(questType, questId, clearCount, 0, 0);

    public static QuestClear Hunted(byte questType, int questId, int clearCount, int clearTimeMs, int kills) =>
        new(questType, questId, clearCount, Math.Max(0, clearTimeMs) / MillisecondsPerSecond, kills);

    public static QuestClear Uncounted(byte questType, int questId) => new(questType, questId, 0, 0, 0);
}
