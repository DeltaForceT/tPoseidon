namespace tPoseidon.Core.Game.Quests;

public static class NpcMessages
{
    public const int QuestBase = 0x80;
    public const int MonsterBase = 0x100;
    public const int SkillShopBase = 0x200;
}

public static class NpcGreeting
{
    public const int Always = 0;
    public const int QuestDone = 1;
    public const int QuestActive = 2;
    public const int ItemHeld = 3;
    public const int ScenarioQuestDone = 6;
    public const int ScenarioQuestActive = 7;
}
