namespace tPoseidon.Core.Game.Quests;

public sealed record MonsterQuestEntry(int QuestId, int Count, int Grade, int Monster, int Kills, int ClearTimeMs, bool Success);
