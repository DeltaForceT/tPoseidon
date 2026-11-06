namespace tPoseidon.Core.DataCatalog;

public static class TableFamilies
{
    public const string Other = "Other";

    private static readonly (string Prefix, string Family)[] Prefixes =
    {
        ("ItemParam", "Items"),
        ("ItemRefineTable", "Items"),
        ("Item_", "Items"),
        ("MapItem_", "MapItems"),
        ("MonsterParam", "Monsters"),
        ("MonsterQuest", "MonsterQuests"),
        ("Monster_", "Monsters"),
        ("MonItem", "Drops"),
        ("MonAI_", "Monsters"),
        ("Mon_", "MonsterPools"),
        ("Tactics_", "Monsters"),
        ("NpcTalk_", "NpcTalk"),
        ("NpcMsg_", "NpcTalk"),
        ("Npc", "Npc"),
        ("RegionInfo_", "Regions"),
        ("MapInfo", "Maps"),
        ("Map", "Maps"),
        ("SkillShop", "Shops"),
        ("Shop", "Shops"),
        ("Skill", "Skills"),
        ("ESAII_", "Skills"),
        ("ESAll_", "Skills"),
        ("ESActoin_", "Skills"),
        ("ESAction_", "Skills"),
        ("ScenarioQuest", "Quests"),
        ("Quest", "Quests"),
        ("Scenario", "Scenario"),
        ("Pet", "Pets"),
        ("Soul", "Soul"),
        ("Guild", "Guild"),
        ("Guardian", "Guardian"),
    };

    public static string Of(string tableName)
    {
        ArgumentNullException.ThrowIfNull(tableName);
        foreach (var (prefix, family) in Prefixes)
        {
            if (tableName.StartsWith(prefix, StringComparison.Ordinal)) return family;
        }
        return Other;
    }
}
