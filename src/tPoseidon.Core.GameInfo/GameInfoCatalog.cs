using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.GameInfo;

public sealed class GameInfoCatalog
{
        public required IQuestInfoLookup Quests { get; init; }

        public required IQuestResultLookup QuestResults { get; init; }

        public required INpcTalkLookup NpcTalk { get; init; }

        public required INpcMsgLookup NpcMsg { get; init; }

        public required ICharacterInfoLookup Characters { get; init; }

        public required ITutorialSystemLookup TutorialSystem { get; init; }

        public required IMonsterQuestInfoLookup MonsterQuests { get; init; }

        public required IQuestItemInfoLookup QuestItems { get; init; }

        public required IItemParamLookup Items { get; init; }

        public required IReadOnlyDictionary<string, int> TableRowCounts { get; init; }

        public static GameInfoCatalog LoadFromXml(string xmlPath, Action<string>? logCallback = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(xmlPath);
        var log = logCallback ?? (_ => { });

        var reader = new LibConfigXmlReader(xmlPath);
        var rowCounts = new Dictionary<string, int>();

        log("GameInfoCatalog: loading QuestInfo + ScenarioQuestInfo");
        var quests = reader.LoadAllQuestTables();
        rowCounts["Quest"] = quests.Count;

        log($"GameInfoCatalog: loaded {quests.Count} quest entries; loading QuestResult_*");
        var questResults = reader.LoadAllQuestResultTables();
        rowCounts["QuestResult"] = questResults.Count;

        log($"GameInfoCatalog: loaded {questResults.Count} quest-result entries; loading NpcTalk_*");
        var npcTalk = reader.LoadAllNpcTalkTables();
        rowCounts["NpcTalk"] = npcTalk.Count;

        log($"GameInfoCatalog: loaded {npcTalk.Count} NpcTalk entries; loading NpcMsg_*");
        var npcMsg = reader.LoadAllNpcMsgTables();
        rowCounts["NpcMsg"] = npcMsg.Count;

        log($"GameInfoCatalog: loaded {npcMsg.Count} NpcMsg entries; loading CharacterInfo");
        var chars = reader.LoadCharacterInfo();
        rowCounts["CharacterInfo"] = chars.Count;

        log($"GameInfoCatalog: loaded {chars.Count} CharacterInfo entries; loading TutorialSystem");
        var tutorial = reader.LoadTutorialSystem();
        rowCounts["TutorialSystem"] = tutorial.Count;

        log($"GameInfoCatalog: loaded {tutorial.Count} TutorialSystem entries; loading MonsterQuestInfo");
        var monsterQuests = reader.LoadMonsterQuestInfo();
        rowCounts["MonsterQuestInfo"] = monsterQuests.Count;

        log($"GameInfoCatalog: loaded {monsterQuests.Count} MonsterQuestInfo entries; loading QuestItemInfo");
        var questItems = reader.LoadQuestItemInfo();
        rowCounts["QuestItemInfo"] = questItems.Count;

        log($"GameInfoCatalog: loaded {questItems.Count} QuestItemInfo entries; loading ItemParamEx + ItemParamEx2 + ItemParam2 + ItemParamCM2");
        var itemsEx = reader.LoadItemParamExtended();
        var itemsEx2 = reader.LoadItemParamEx2();
        var itemsBase = reader.LoadItemParam2();
        var itemsCm2 = reader.LoadItemParamCm2();
        rowCounts["ItemParamEx"] = itemsEx.Count;
        rowCounts["ItemParamEx2"] = itemsEx2.Count;
        rowCounts["ItemParam2"] = itemsBase.Count;
        rowCounts["ItemParamCM2"] = itemsCm2.Count;

        

        

        var items = new List<ItemParamEntry>(itemsEx.Count + itemsEx2.Count + itemsBase.Count + itemsCm2.Count);
        items.AddRange(itemsEx);
        items.AddRange(itemsEx2);
        items.AddRange(itemsBase);
        items.AddRange(itemsCm2);

        log($"GameInfoCatalog: ready ({string.Join(", ", rowCounts.Select(kv => $"{kv.Key}={kv.Value}"))})");

        return new GameInfoCatalog
        {
            Quests = new InMemoryQuestInfoLookup(quests),
            QuestResults = new InMemoryQuestResultLookup(questResults),
            NpcTalk = new InMemoryNpcTalkLookup(npcTalk),
            NpcMsg = new InMemoryNpcMsgLookup(npcMsg),
            Characters = new InMemoryCharacterInfoLookup(chars),
            TutorialSystem = new InMemoryTutorialSystemLookup(tutorial),
            MonsterQuests = new InMemoryMonsterQuestInfoLookup(monsterQuests),
            QuestItems = new InMemoryQuestItemInfoLookup(questItems),
            Items = new InMemoryItemParamLookup(items),
            TableRowCounts = rowCounts,
        };
    }
}
