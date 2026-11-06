namespace tPoseidon.Core.DataCatalog.References;

public static class ReferenceRules
{
    private const string NpcTalk = "(?:R_)?NpcTalk_.+";
    private const string SkillShopTalk = "SkillShopTalk_.+";
    private const string QuestResult = "(?:Monster)?QuestResult_\\d+";
    private const string QuestInfo = "QuestInfo|MonsterQuestInfo|ScenarioQuestInfo|PartyMonQuestInfo";
    private const string MonsterParam = "MonsterParamEx2";
    private const string MapItems = "MapItem_.+";
    private const string BossMonsters = "BossMon_.+";
    private const string HarconScenarios = "Sc_HarconDef_.+";
    private const string ShopItems = "ShopItem_.+|SkillShopItem_.+";

    public static IReadOnlyList<ReferenceRule> All { get; } = new[]
    {
        ReferenceRule.Of("MapInfoEx", "MonsterTable|BossMonsterTable|ItemTable|ShopTable|NpcTable|SkillShopTable|MonsterTactics", ReferenceTarget.Table),
        ReferenceRule.Of("SkillParam|SkillParam2|SkillParam_Item|RuneStoneSkillParam", "ESAIITable|ESATable|ESAUITable|Item_SkillCardParam", ReferenceTarget.Table),
        ReferenceRule.Of(NpcTalk, "MsgTable|EventMsgTable|EventSceneTable|ExcShopTable|HarconShopTable|CompounderTable|TutorialTable|VIPShopItemTable", ReferenceTarget.Table),
        ReferenceRule.Of("Quest_NpcMsg", "NPCTable", ReferenceTarget.Table),
        ReferenceRule.Of(QuestInfo, "ResultTable", ReferenceTarget.Table),
        ReferenceRule.Of(MonsterParam, "ProtectTable|Speech", ReferenceTarget.Table),
        ReferenceRule.Of(MonsterParam, "TacticsTable", ReferenceTarget.TacticsGroup),
        ReferenceRule.Of("PetParam", "Speech", ReferenceTarget.Table),
        ReferenceRule.Of("MapSound", "MapSound", ReferenceTarget.Table),
        ReferenceRule.Of("Compounder_Info", "ListName", ReferenceTarget.Table),
        ReferenceRule.Of("HarconDefInfo", "LinkSrc", ReferenceTarget.Table),
        ReferenceRule.Of("JumpingChrInfo", "JumpItemTable", ReferenceTarget.Table),
        ReferenceRule.Of("GuildGameTypeInfo", "ParamTable", ReferenceTarget.Table),
        ReferenceRule.Of("MemoryPuzzleParam", "ObjectTable", ReferenceTarget.Table),
        ReferenceRule.Of("MissionInfo", "MissionMonsterTable", ReferenceTarget.Table),
        ReferenceRule.Of("MonsterScenario", "ScenarioTable[0-2]", ReferenceTarget.Table),
        ReferenceRule.Of("NpcEventScene_.+", "ActionLinkTable", ReferenceTarget.Table),
        ReferenceRule.Of("Npc_TimeTable", "NpcTalkTable", ReferenceTarget.Table),
        ReferenceRule.Of("QuestRef_.+", "MapDetailPosTable", ReferenceTarget.Table),
        ReferenceRule.Of("ShadowResult", "Scenario", ReferenceTarget.Table),
        ReferenceRule.Of("SlidePuzzleParam", "BlockTable", ReferenceTarget.Table),
        ReferenceRule.Of("SoulGuardianParam", "LevelGrowthTable", ReferenceTarget.Table),
        ReferenceRule.Of(SkillShopTalk, "LimitSkillMaster|ItemTable", ReferenceTarget.Table),
        ReferenceRule.Of("Shop_.+", "ItemTable", ReferenceTarget.Table),
        ReferenceRule.Of("SpotGroupMultiMap_.+", "MapGroupTable1", ReferenceTarget.Table),
        ReferenceRule.Of("SpotGroupInfo", "MultiMapGroupTable1", ReferenceTarget.Table),
        ReferenceRule.Of("TutorialSystem", "DlgTable", ReferenceTarget.Table),
        ReferenceRule.Of("WorldMap_RegionInfo", "RegionTable", ReferenceTarget.Table),

        ReferenceRule.Of(QuestResult + "|MonItemDropInfo|MonItemTable_DropInfo|RandomResultItemInfo|" + MapItems, "Item\\d+", ReferenceTarget.Item),
        ReferenceRule.Of(MapItems, "ItemID", ReferenceTarget.Item),
        ReferenceRule.Of(ShopItems, "ItemId", ReferenceTarget.Item),
        ReferenceRule.Of(NpcTalk, "CheckItemId[01]|GiveItemID", ReferenceTarget.Item),
        ReferenceRule.Of("BossMonsterparamEx", "GiveItemID_\\d+", ReferenceTarget.Item),
        ReferenceRule.Of("ItemRefineTable\\d+", "ItemIdLv\\d+", ReferenceTarget.Item),
        ReferenceRule.Of("GhostExchange_Group", "ItemId\\d+", ReferenceTarget.Item),
        ReferenceRule.Of("SetItemParam", "ItemID\\d", ReferenceTarget.Item),
        ReferenceRule.Of("PostBoxLevelUpGiftInfo|JumpRewardItems", "ItemId_\\d+", ReferenceTarget.Item),
        ReferenceRule.Of("GuildGameRewardInfo", "ItemID_\\d+", ReferenceTarget.Item),
        ReferenceRule.Of("PresentItemParam2", "DropId_\\d+", ReferenceTarget.Item),
        ReferenceRule.Of("Card_SpecialSet", "CardId\\d", ReferenceTarget.Item),
        ReferenceRule.Of("MeltingDropInfo", "DropItem", ReferenceTarget.Item),
        ReferenceRule.Of("VIPShop|HarconShop_\\d+", "ChangeItemId", ReferenceTarget.Item),
        ReferenceRule.Of("KeyboxItemInfo", "KeyBoxID", ReferenceTarget.Item),
        ReferenceRule.Of("ItemStatInfo", "ItemIndex", ReferenceTarget.Item),
        ReferenceRule.Of("SkillMasterInfo", "ItemId", ReferenceTarget.Item),
        ReferenceRule.Of(QuestInfo, "SrcItem\\d", ReferenceTarget.Item),
        ReferenceRule.Of("NpcMoveInfo", "SrcItem\\d", ReferenceTarget.Item),
        ReferenceRule.Of("PortalInfo", "ItemID", ReferenceTarget.Item),

        ReferenceRule.Of(BossMonsters, "BossMonID", ReferenceTarget.Monster),
        ReferenceRule.Of(HarconScenarios, "MonId\\d+", ReferenceTarget.Monster),
        ReferenceRule.Of("ShadowAI|MonsterRegionInfo", "MonID", ReferenceTarget.Monster),
        ReferenceRule.Of("ScMon_Shadow\\d+", "CheckMonster", ReferenceTarget.Monster),
        ReferenceRule.Of("Mon_.+", "MonsterID", ReferenceTarget.Monster),
        ReferenceRule.Of("MonsterQuestInfo", "SrcMonster\\d", ReferenceTarget.Monster),

        ReferenceRule.Of("MonsterRegionInfo", "MapID\\d", ReferenceTarget.Map),
        ReferenceRule.Of("GuildGameTypeInfo", "Map0", ReferenceTarget.Map),
        ReferenceRule.Of("PortalInfo", "Map|DestZone", ReferenceTarget.Map),
        ReferenceRule.Of("Mapboard", "MapIndex", ReferenceTarget.Map),
        ReferenceRule.Of("PuzzleMapParam|SpotGroupMap_Test1", "MapID", ReferenceTarget.Map),
        ReferenceRule.Of("TutorialSystem", "EndMapID", ReferenceTarget.Map),
        ReferenceRule.Of("HarconDefMapIndex", "MapNoriIndex", ReferenceTarget.Map),
        ReferenceRule.Of("MonsterQuestInfo", "MapId\\d", ReferenceTarget.Map),
        ReferenceRule.Of("NpcMoveInfo", "Zone", ReferenceTarget.Map),

        ReferenceRule.Of(NpcTalk, "CheckQuestId[12]?|ActiveQuestID[01]", ReferenceTarget.Quest),
        ReferenceRule.Of(SkillShopTalk, "JobQuestID", ReferenceTarget.Quest),
        ReferenceRule.Of("PortalInfo", "QuestID", ReferenceTarget.Quest),

        ReferenceRule.Of("ScMon_Shadow\\d+", "AllMapSkill", ReferenceTarget.Skill),
        ReferenceRule.Of("Item_SkillCardParam", "AffectionSkill|PreSkill_\\d", ReferenceTarget.Skill),
        ReferenceRule.Of("SkillMasterInfo", "SkillId", ReferenceTarget.Skill),
        ReferenceRule.Of(QuestResult, "Skill", ReferenceTarget.Skill),
        ReferenceRule.Of(MonsterParam, "DamageSkill", ReferenceTarget.Skill),

        ReferenceRule.Of(MonsterParam, "ItemDropIdx", ReferenceTarget.MonsterDropRow),
    };
}
