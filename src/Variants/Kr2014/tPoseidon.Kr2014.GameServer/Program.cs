using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.SoulSeed;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;
using tPoseidon.Kr2014.Common;
using tPoseidon.Kr2014.GameServer;
using EnterWorldBatchSynthesizer = tPoseidon.Kr2014.GameServer.EnterWorldBatchSynthesizer;

#pragma warning disable CA1859

ConsoleFileLog.EnableIfConfigured();
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
const int ClientTextCodePage = 949;
var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : ServerEndpointConfig.GamePort;
const string LogTag = "[game]";
const string MonsterTablePrefix = "Mon_";
const double RegenPassGapMs = 5.0;
const string LevelExpChartFile = "gulim14.fnt2";
const string StatusChartFile = "gulim13.fnt2";
const int AttackRequestMoveTypeOffset = 2;
Console.WriteLine($"{LogTag} {ServerEndpointConfig.Summary()}");

var kr2014GameInstance = port == ServerEndpointConfig.GameAltPort ? 2 : 1;
var selfIdentity = kr2014GameInstance == 2
    ? tPoseidon.Core.InterServer.ServerIdentity.Game2
    : tPoseidon.Core.InterServer.ServerIdentity.Game1;
var meshTag = $"[{selfIdentity.Name}]";
var kr2014GameIni = ServerIniLocator.TryLoad(
    ServerIniLocator.ServerKind.Game,
    ServerIniLocator.Variant.Kr2014,
    kr2014GameInstance,
    msg => Console.WriteLine($"{LogTag} {msg}"));
if (kr2014GameIni is not null)
{
    var iniMaxClient = kr2014GameIni.GetInt("MaxClient", -1);
    var iniLingualId = kr2014GameIni.GetInt("LingualID", -1);
    var iniId = kr2014GameIni.GetString("ID") ?? "?";
    var iniVerbose0 = kr2014GameIni.GetInt("Verbose0", -1);
    var iniVerbose1 = kr2014GameIni.GetInt("Verbose1", -1);
    var iniVerbose2 = kr2014GameIni.GetInt("Verbose2", -1);
    Console.WriteLine($"{LogTag} GameServer.ini knobs: ID={iniId}, MaxClient={iniMaxClient}, LingualID={iniLingualId}, Verbose0/1/2={iniVerbose0}/{iniVerbose1}/{iniVerbose2}");
}

var pg = await PostgresStoreBootstrap.InitializeAsync();
var accountStore = pg.Store;
var brokeredJoins = new tPoseidon.Kr2014.GameServer.BrokeredJoinRegistry(TimeSpan.FromSeconds(30));
tPoseidon.Core.InterServer.MasterServerClient? masterClient = null;
Console.WriteLine($"{LogTag} account store ready (Postgres-backed); GAME_INIT bridges to account via source-IP + acc_game_state=3");

var dialogService = new NpcDialogService();

var variantDataRoot = Environment.GetEnvironmentVariable("MT_VARIANT_DATA_ROOT")
    ?? Path.GetFullPath(RepoRoot.Data("variants", "Kr2014"));
Console.WriteLine($"{LogTag} variant data root: {LogPaths.Scrub(variantDataRoot)} (exists={Directory.Exists(variantDataRoot)})");

var libconfigPath = Environment.GetEnvironmentVariable("MT_LIBCONFIG_PATH")
    ?? PickFirstExisting(
        Path.Combine(variantDataRoot, "libconfig.dat"),
        Path.Combine(variantDataRoot, "libconfig_eng.dat"),
        RepoRoot.Data("krn-2014.01.28", "libconfig_decrypted.xml"));
Console.WriteLine($"{LogTag} libconfig path: {LogPaths.Scrub(libconfigPath)} (exists={File.Exists(libconfigPath)})");

var libconfigIdxPath = Path.ChangeExtension(libconfigPath, ".idx");
if (tPoseidon.Core.LibConfig.LibConfigIdxReader.TryLoad(libconfigIdxPath, out var libconfigIdxReader)
    && libconfigIdxReader is not null)
{
    Console.WriteLine($"{LogTag} libconfig idx loaded: {libconfigIdxReader.Count} table entries from {LogPaths.Scrub(libconfigIdxPath)} (fast-seek available)");
}
else
{
    Console.WriteLine($"{LogTag} libconfig idx not loaded (fast-seek disabled; full .dat scan will be used)");
}

static string PickFirstExisting(params string[] candidates)
{
    foreach (var c in candidates)
    {
        if (File.Exists(c)) return c;
    }
    return candidates[^1];
}
IQuestInfoLookup questCatalog;
try
{
    questCatalog = InMemoryQuestInfoLookup.LoadFromXml(libconfigPath);
    Console.WriteLine($"{LogTag} loaded {questCatalog.Count} quests from libconfig");
    if (questCatalog.Count == 0)
    {
        Console.Error.WriteLine($"{LogTag} WARNING: libconfig loaded 0 quests — file may be malformed or have unexpected schema");
    }
    else if (questCatalog.GetById(1233) is null)
    {
        Console.Error.WriteLine($"{LogTag} WARNING: quest 1233 (Heidi welcome) NOT in libconfig — auto-accept will fail");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} libconfig load FAILED: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine($"{LogTag}   path tried: {LogPaths.Scrub(libconfigPath)}");
    Console.Error.WriteLine($"{LogTag}   set MT_LIBCONFIG_PATH env var to override the default repo-root path");
    questCatalog = new InMemoryQuestInfoLookup(Array.Empty<QuestInfoEntry>());
}

InMemoryQuestResultLookup questResults;
try
{
    questResults = InMemoryQuestResultLookup.LoadFromXml(libconfigPath);
    Console.WriteLine($"{LogTag} loaded {questResults.Count} QuestResult entries from libconfig");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} QuestResult load FAILED: {ex.GetType().Name}: {ex.Message}");
    questResults = new InMemoryQuestResultLookup(Array.Empty<QuestResultEntry>());
}

IMonsterQuestInfoLookup monsterQuestCatalog;
InMemoryMonsterQuestResultLookup monsterQuestResults;
try
{
    var monsterQuestReader = new LibConfigXmlReader(libconfigPath);
    var monsterQuests = monsterQuestReader.LoadMonsterQuestInfo();
    monsterQuestCatalog = new InMemoryMonsterQuestInfoLookup(monsterQuests);
    monsterQuestResults = new InMemoryMonsterQuestResultLookup(monsterQuestReader.LoadMonsterQuestResultTables());
    Console.WriteLine($"{LogTag} loaded {monsterQuests.Count} monster quests and {monsterQuestResults.Count} monster quest result tables from libconfig");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} monster quest load FAILED: {ex.GetType().Name}: {ex.Message}");
    monsterQuestCatalog = new InMemoryMonsterQuestInfoLookup(Array.Empty<MonsterQuestInfoEntry>());
    monsterQuestResults = new InMemoryMonsterQuestResultLookup(Array.Empty<MonsterQuestResultEntry>());
}

tPoseidon.Core.LibConfig.PartyQuestCatalog partyQuestCatalog;
InMemoryMonsterQuestResultLookup partyQuestResults;
try
{
    var partyQuestReader = new LibConfigXmlReader(libconfigPath);
    partyQuestCatalog = new tPoseidon.Core.LibConfig.PartyQuestCatalog(partyQuestReader.LoadPartyQuestInfo(), partyQuestReader.LoadPartyQuestExpRatios());
    partyQuestResults = new InMemoryMonsterQuestResultLookup(partyQuestReader.LoadPartyQuestResultTables());
    Console.WriteLine($"{LogTag} loaded {partyQuestCatalog.Count} party quests and {partyQuestResults.Count} party quest result tables from libconfig");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} party quest load FAILED: {ex.GetType().Name}: {ex.Message}");
    partyQuestCatalog = tPoseidon.Core.LibConfig.PartyQuestCatalog.Empty;
    partyQuestResults = new InMemoryMonsterQuestResultLookup(Array.Empty<MonsterQuestResultEntry>());
}

IQuestStore questStore = new PostgresQuestStore(pg.DataSource);

IQuestHistoryStore questHistoryStore = new PostgresQuestHistoryStore(pg.DataSource);

IGuideHistoryStore guideHistoryStore = new PostgresGuideHistoryStore(pg.DataSource);

IItemStore itemStore = new PostgresItemStore(pg.DataSource);

ISkillStore skillStore = new PostgresSkillStore(pg.DataSource);

IMonsterHuntStore monsterHuntStore = new PostgresMonsterHuntStore(pg.DataSource);

ISafetyPasswordStore safetyPasswordStore = new PostgresSafetyPasswordStore(pg.DataSource);

IBankStore bankStore = new PostgresBankStore(pg.DataSource);

var itemPropertyStore = new PostgresItemPropertyStore(pg.DataSource);

IMyCampStore myCampStore = new PostgresMyCampStore(pg.DataSource);

IDeletedCharStore deletedCharStore = new PostgresDeletedCharStore(pg.DataSource);

IMemoryPortStore memoryPortStore = new PostgresMemoryPortStore(pg.DataSource);

IPkInfoStore pkInfoStore = new PostgresPkInfoStore(pg.DataSource);

IUniqueCharDutyInfoStore uniqueCharDutyStore = new PostgresUniqueCharDutyInfoStore(pg.DataSource);

ICharProfileStore charProfileStore = new PostgresCharProfileStore(pg.DataSource);

IGuildStore guildStore = new PostgresGuildStore(pg.DataSource);

ICoupleStore coupleStore = new PostgresCoupleStore(pg.DataSource);

IAwakeningStore awakeningStore = new PostgresAwakeningStore(pg.DataSource);

ISoulGuardianStore soulGuardianStore = new PostgresSoulGuardianStore(pg.DataSource);

ISoulSeedStore soulSeedStore = new PostgresSoulSeedStore(pg.DataSource);

IPvpStore pvpStore = new PostgresPvpStore(pg.DataSource);

IPostStore postStore = new PostgresPostStore(pg.DataSource);

IFriendStore friendStore = new PostgresFriendStore(pg.DataSource);

IAccountBlockStore accountBlockStore = new PostgresAccountBlockStore(pg.DataSource);

IAccountLogoutStore accountLogoutStore = new PostgresAccountLogoutStore(pg.DataSource);

IFortuneStore fortuneStore = new PostgresFortuneStore(pg.DataSource);

IBroadcastStore broadcastStore = new PostgresBroadcastStore(pg.DataSource);

INpcTalkLookup npcTalkCatalog;
INpcMsgLookup npcMsgCatalog;
ITutorialSystemLookup tutorialSystemCatalog;
ICharacterInfoLookup characterInfoCatalog;
try
{
    npcTalkCatalog = InMemoryNpcTalkLookup.LoadFromXml(libconfigPath);
    npcMsgCatalog = InMemoryNpcMsgLookup.LoadFromXml(libconfigPath);
    tutorialSystemCatalog = InMemoryTutorialSystemLookup.LoadFromXml(libconfigPath);
    characterInfoCatalog = InMemoryCharacterInfoLookup.LoadFromXml(libconfigPath);
    Console.WriteLine($"{LogTag} loaded {npcTalkCatalog.Count} NPC bindings + {npcMsgCatalog.Count} dialog rows + {tutorialSystemCatalog.Count} tutorial steps + {characterInfoCatalog.Count} character info rows from libconfig");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} NpcTalk/NpcMsg/TutorialSystem/CharacterInfo load FAILED: {ex.GetType().Name}: {ex.Message} — NPC dialog DISABLED");
    npcTalkCatalog = new InMemoryNpcTalkLookup(Array.Empty<NpcTalkEntry>());
    npcMsgCatalog = new InMemoryNpcMsgLookup(Array.Empty<NpcMsgEntry>());
    tutorialSystemCatalog = new InMemoryTutorialSystemLookup(Array.Empty<TutorialSystemEntry>());
    characterInfoCatalog = new InMemoryCharacterInfoLookup(Array.Empty<CharacterInfoEntry>());
}

IItemParamLookup itemCatalog;
IReadOnlyList<int> fortuneBasicCards = Array.Empty<int>();
try
{
    var reader = new LibConfigXmlReader(libconfigPath);
    var ex = reader.LoadItemParamExtended();
    var ex2 = reader.LoadItemParamEx2();
    var p2 = reader.LoadItemParam2();
    var cm2 = reader.LoadItemParamCm2();
    var merged = new List<ItemParamEntry>(ex.Count + ex2.Count + p2.Count + cm2.Count);
    merged.AddRange(ex);
    merged.AddRange(ex2);
    merged.AddRange(p2);
    merged.AddRange(cm2);
    itemCatalog = new InMemoryItemParamLookup(merged);
    fortuneBasicCards = tPoseidon.Core.Game.Fortune.FortuneHouse.BasicCards(p2);
    Console.WriteLine($"{LogTag} fortune basic cards: {fortuneBasicCards.Count} items (Type 35, SubType 70, CardNum 0..3)");
    Console.WriteLine($"{LogTag} loaded {itemCatalog.Count} item definitions from libconfig (ItemParamEx + ItemParamEx2 + ItemParam2 + ItemParamCM2)");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} ItemParam load FAILED: {ex.GetType().Name}: {ex.Message}");
    itemCatalog = new InMemoryItemParamLookup(Array.Empty<ItemParamEntry>());
}

IMonsterParamLookup monsterCatalog;
IMonItemDropInfoLookup dropCatalog;
IRegionInfoLookup regionInfoLookup;
IMonZoneLookup monZoneLookup;
IMonsterTacticsLookup monsterTacticsLookup;
IReadOnlyDictionary<string, IReadOnlyList<MonsterTacticEntry>> monsterSkillTactics = new Dictionary<string, IReadOnlyList<MonsterTacticEntry>>();
IReadOnlyDictionary<int, IReadOnlyList<int>> monsterDebuffResists = new Dictionary<int, IReadOnlyList<int>>();
IReadOnlyDictionary<int, GunParamEntry> gunParams = new Dictionary<int, GunParamEntry>();
IReadOnlyDictionary<int, PresentItemEntry> presentItems = new Dictionary<int, PresentItemEntry>();
IShopZoneLookup shopZoneLookup;
IReadOnlyDictionary<string, IReadOnlyList<ShopGoodsEntry>> shopGoodsByTable;
IReadOnlyDictionary<int, float> shopPriceRateByMap;
IReadOnlyDictionary<int, SkillMasterEntry> skillMasterItems;
IReadOnlyDictionary<string, IReadOnlyList<int>> skillMasterLists;
ISkillShopZoneLookup skillShopZoneLookup;
IMapItemZoneLookup mapItemZoneLookup;
IEsActionLookup esActionLookup;
IItemStatRollLookup itemStatRollLookup;
tPoseidon.Core.Game.Items.UniqueItemManager uniqueItemManager;
var uniqueItemPool = new tPoseidon.Core.Game.Items.UniqueItemPool(Array.Empty<LegendItemGroupEntry>());
IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>> monsterProtections = new Dictionary<string, IReadOnlyDictionary<int, int>>();
var krLibConfigPath = PickFirstExisting(
    Path.Combine(variantDataRoot, "libconfig.dat"),
    Path.Combine(variantDataRoot, "libconfig_eng.dat"),
    RepoRoot.Data("libconfig_kr_en.xml"));
try
{
    if (File.Exists(krLibConfigPath))
    {
        var reader = new LibConfigXmlReader(krLibConfigPath);
        var monsterEntries = reader.LoadMonsterParamEx2();
        monsterCatalog = new InMemoryMonsterParamLookup(monsterEntries);
        monsterProtections = reader.LoadMonsterProtections(monsterEntries.Select(entry => entry.ProtectTable));
        Console.WriteLine($"{LogTag} loaded {monsterProtections.Count} monster protection tables (MonsterParamEx2.ProtectTable)");
        Console.WriteLine($"{LogTag} loaded {monsterCatalog.Count} monster definitions from libconfig (MonsterParamEx2)");

        var dropEntries = reader.LoadMonItemDropInfo();
        dropCatalog = new InMemoryMonItemDropInfoLookup(dropEntries);
        Console.WriteLine($"{LogTag} loaded {dropCatalog.Count} drop tables from libconfig (MonItemDropInfo)");

        var regionEntries = reader.LoadAllRegionInfoTables();
        regionInfoLookup = new InMemoryRegionInfoLookup(regionEntries);
        Console.WriteLine($"{LogTag} loaded {regionInfoLookup.Count} region rects from libconfig (RegionInfo_*)");

        var monZoneEntries = reader.LoadAllMonZoneTables();
        monZoneLookup = new InMemoryMonZoneLookup(monZoneEntries);
        Console.WriteLine($"{LogTag} loaded {monZoneLookup.Count} per-zone monster pool rows from libconfig (Mon_*)");

        var tacticsEntries = reader.LoadAllTacticsMonTables();
        var monAiEntries = reader.LoadAllMonAITacticsTables();
        monsterTacticsLookup = new InMemoryMonsterTacticsLookup(tacticsEntries, monAiEntries);
        Console.WriteLine($"{LogTag} loaded {monsterTacticsLookup.TacticsCount} tactics + {monsterTacticsLookup.AICount} AI rows from libconfig (Tactics_Mon* + MonAI_Tactics_Mon*)");
        monsterSkillTactics = reader.LoadMonsterTactics()
            .GroupBy(tactic => tactic.TacticsName)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<MonsterTacticEntry>)group.OrderBy(tactic => tactic.Id).ToList());
        Console.WriteLine($"{LogTag} loaded {monsterSkillTactics.Count} monster skill tactic groups (Monster_TacticsEX)");
        monsterDebuffResists = reader.LoadMonsterDebuffResists().ToDictionary(entry => entry.Id, entry => entry.Values);
        Console.WriteLine($"{LogTag} loaded {monsterDebuffResists.Count} monster debuff resist rows (MonsterDebuffResist)");
        presentItems = reader.LoadPresentItems().GroupBy(entry => entry.BoxItemId).ToDictionary(group => group.Key, group => group.First());
        Console.WriteLine($"{LogTag} loaded {presentItems.Count} gift box rows ({PresentItemEntry.TableName})");
        gunParams = reader.LoadGunParams().GroupBy(entry => entry.ItemId).ToDictionary(group => group.Key, group => group.First());
        Console.WriteLine($"{LogTag} loaded {gunParams.Count} gun rows (Item_GunParam2)");

        var shopEntries = reader.LoadAllShopZoneTables();
        shopZoneLookup = new InMemoryShopZoneLookup(shopEntries);
        Console.WriteLine($"{LogTag} loaded {shopZoneLookup.Count} shop NPCs from libconfig (Shop_*)");

        var skillShopEntries = reader.LoadAllSkillShopZoneTables();
        skillShopZoneLookup = new InMemorySkillShopZoneLookup(skillShopEntries);
        Console.WriteLine($"{LogTag} loaded {skillShopZoneLookup.Count} skill-shop NPCs from libconfig (SkillShopTalk_*)");

        var shopGoods = reader.LoadShopGoods(shopEntries
            .Select(entry => entry.ItemTable)
            .Concat(skillShopEntries.Select(entry => entry.ItemTable))
            .Where(table => table.Length > 0)
            .ToHashSet(StringComparer.Ordinal));
        shopGoodsByTable = shopGoods
            .GroupBy(goods => goods.Table, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ShopGoodsEntry>)group.ToList(), StringComparer.Ordinal);
        shopPriceRateByMap = reader.LoadShopPriceRates()
            .GroupBy(rate => rate.MapId)
            .ToDictionary(group => group.Key, group => group.First().ShopPriceRate);
        Console.WriteLine($"{LogTag} loaded {shopGoods.Count} shop goods rows in {shopGoodsByTable.Count} goods tables and shop price rates for {shopPriceRateByMap.Count} maps");

        skillMasterItems = reader.LoadSkillMasterInfo()
            .GroupBy(master => master.SkillId)
            .ToDictionary(group => group.Key, group => group.First());
        skillMasterLists = reader.LoadSkillIdLists(skillShopEntries
            .Select(entry => entry.LimitSkillMaster)
            .Where(table => table.Length > 0)
            .ToHashSet(StringComparer.Ordinal));
        Console.WriteLine($"{LogTag} loaded {skillMasterItems.Count} skill mastery rows (SkillMasterInfo) and {skillMasterLists.Count} mastery lists (LimitSkillMaster_*)");

        var mapItemEntries = reader.LoadAllMapItemZoneTables();
        mapItemZoneLookup = new InMemoryMapItemZoneLookup(mapItemEntries);
        Console.WriteLine($"{LogTag} loaded {mapItemZoneLookup.Count} map-item drillable entries from libconfig (MapItem_*)");

        var esActionEntries = reader.LoadAllEsActionTables();
        esActionLookup = new InMemoryEsActionLookup(esActionEntries);
        Console.WriteLine($"{LogTag} loaded {esActionLookup.Count} ES-action rows from libconfig (ESAction_*)");

        var statRollEntries = reader.LoadRandomStatValueTable();
        itemStatRollLookup = new InMemoryItemStatRollLookup(statRollEntries);
        Console.WriteLine($"{LogTag} loaded {itemStatRollLookup.Count} random-stat-roll entries from libconfig (RandomStatValueTable)");

        var uniqueCounts = reader.LoadUniqueItemCountTable();
        var legendItems = reader.LoadLegendItemList();
        uniqueItemManager = new tPoseidon.Core.Game.Items.UniqueItemManager(legendItems);
        uniqueItemPool = new tPoseidon.Core.Game.Items.UniqueItemPool(reader.LoadLegendItemGroups());
        Console.WriteLine($"{LogTag} loaded {uniqueItemPool.GroupCount} legend monster groups (LegendItemGroup)");
        Console.WriteLine($"{LogTag} loaded UniqueItem ({uniqueCounts.Count} count rows, {uniqueItemManager.LegendItemCount} legend items)");
    }
    else
    {
        Console.Error.WriteLine($"{LogTag} KR libconfig not at {krLibConfigPath} — MonsterParam + drop catalogs empty");
        monsterCatalog = new InMemoryMonsterParamLookup(Array.Empty<MonsterParamEntry>());
        dropCatalog = new InMemoryMonItemDropInfoLookup(Array.Empty<MonItemDropInfoEntry>());
        regionInfoLookup = new InMemoryRegionInfoLookup(Array.Empty<RegionInfoEntry>());
        monZoneLookup = new InMemoryMonZoneLookup(Array.Empty<MonZoneEntry>());
        monsterTacticsLookup = new InMemoryMonsterTacticsLookup(Array.Empty<TacticsMonEntry>(), Array.Empty<MonAITacticsEntry>());
        shopZoneLookup = new InMemoryShopZoneLookup(Array.Empty<ShopZoneEntry>());
        shopGoodsByTable = new Dictionary<string, IReadOnlyList<ShopGoodsEntry>>();
        shopPriceRateByMap = new Dictionary<int, float>();
        skillMasterItems = new Dictionary<int, SkillMasterEntry>();
        skillMasterLists = new Dictionary<string, IReadOnlyList<int>>();
        skillShopZoneLookup = new InMemorySkillShopZoneLookup(Array.Empty<SkillShopZoneEntry>());
        mapItemZoneLookup = new InMemoryMapItemZoneLookup(Array.Empty<MapItemZoneEntry>());
        esActionLookup = new InMemoryEsActionLookup(Array.Empty<EsActionEntry>());
        itemStatRollLookup = new InMemoryItemStatRollLookup(Array.Empty<RandomStatValueEntry>());
        uniqueItemManager = new tPoseidon.Core.Game.Items.UniqueItemManager(Array.Empty<LegendItemListEntry>());
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} libconfig load FAILED: {ex.GetType().Name}: {ex.Message}");
    monsterCatalog = new InMemoryMonsterParamLookup(Array.Empty<MonsterParamEntry>());
    dropCatalog = new InMemoryMonItemDropInfoLookup(Array.Empty<MonItemDropInfoEntry>());
    regionInfoLookup = new InMemoryRegionInfoLookup(Array.Empty<RegionInfoEntry>());
    monZoneLookup = new InMemoryMonZoneLookup(Array.Empty<MonZoneEntry>());
    monsterTacticsLookup = new InMemoryMonsterTacticsLookup(Array.Empty<TacticsMonEntry>(), Array.Empty<MonAITacticsEntry>());
    shopZoneLookup = new InMemoryShopZoneLookup(Array.Empty<ShopZoneEntry>());
    shopGoodsByTable = new Dictionary<string, IReadOnlyList<ShopGoodsEntry>>();
    shopPriceRateByMap = new Dictionary<int, float>();
    skillMasterItems = new Dictionary<int, SkillMasterEntry>();
    skillMasterLists = new Dictionary<string, IReadOnlyList<int>>();
    skillShopZoneLookup = new InMemorySkillShopZoneLookup(Array.Empty<SkillShopZoneEntry>());
    mapItemZoneLookup = new InMemoryMapItemZoneLookup(Array.Empty<MapItemZoneEntry>());
    esActionLookup = new InMemoryEsActionLookup(Array.Empty<EsActionEntry>());
    itemStatRollLookup = new InMemoryItemStatRollLookup(Array.Empty<RandomStatValueEntry>());
    uniqueItemManager = new tPoseidon.Core.Game.Items.UniqueItemManager(Array.Empty<LegendItemListEntry>());
}

INpcActionScriptLookup npcMotionCatalog;
INpcActionScriptLookup shopNpcMotionCatalog;
INpcActionScriptLookup skillShopNpcMotionCatalog;
MoveSpeedParamEntry? playerMoveSpeeds = null;
BeginnerRebirthInfo? beginnerRebirth = null;
IReadOnlyList<MapMonsterTableEntry> mapMonsterTables = Array.Empty<MapMonsterTableEntry>();
var partyZoneTables = PartyZoneTables.Empty;
IReadOnlyList<tPoseidon.Core.Game.Items.PetComposingPresent> petComposingPresents = Array.Empty<tPoseidon.Core.Game.Items.PetComposingPresent>();
IReadOnlyDictionary<int, int> decomposeDummySkins = new Dictionary<int, int>();
IReadOnlyDictionary<int, int> equipMergeSupports = new Dictionary<int, int>();
IReadOnlyDictionary<int, IReadOnlyList<(int MonsterId, int Count)>> npcSummonSets = new Dictionary<int, IReadOnlyList<(int MonsterId, int Count)>>();
var tmLevelChart = new TmLevelChart(Array.Empty<(int Level, int NextTmExp)>());
var rebirthItemsById = new Dictionary<int, RebirthItemEntry>();
var reviveKeys = new Random();
var mapKindById = new Dictionary<int, MapKindEntry>();
var honorTitleBook = HonorTitleBook.Empty;
IReadOnlyDictionary<int, MemoryPortEntry> memoryPortRows = new Dictionary<int, MemoryPortEntry>();
IReadOnlyDictionary<int, int> disguiseKits = new Dictionary<int, int>();
var soulSeedTables = SoulSeedTables.Empty;
var fortuneTables = FortuneTables.Empty;
IReadOnlyDictionary<int, RecycleChangeEntry> recycleChanges = new Dictionary<int, RecycleChangeEntry>();
IReadOnlyDictionary<int, RecycleShopEntry> recycleShop = new Dictionary<int, RecycleShopEntry>();
IReadOnlySet<int> recycleExcluded = new HashSet<int>();
IReadOnlySet<int> recycleMyCampExcluded = new HashSet<int>();
var slotExtendTables = SlotExtendTables.Empty;
var meltingTables = MeltingTables.Empty;
var equipReformTables = EquipReformTables.Empty;
var petRetrainTables = PetRetrainTables.Empty;
var petLevelUpTables = PetLevelUpTables.Empty;
var petDigTables = PetDigTables.Empty;
var characterPetReinforceTables = CharacterPetReinforceTables.Empty;
var coupleTables = CoupleTables.Empty;
var pkInfo = PkInfoTables.Empty;
var guildGameTypes = GuildGameTypeTables.Empty;
var fiestaTables = FiestaTables.Empty;
var treasureMapTables = TreasureMapTables.Empty;
var missionTables = MissionTables.Empty;
var harconDefTables = HarconDefTables.Empty;
var dailyCycle = tPoseidon.Core.Game.Quests.DailyCycle.Empty;
tPoseidon.Core.Game.Quests.GuildSetupRequirement? guildSetup = null;
var puzzleTables = PuzzleTables.Empty;
var soulSystemTables = SoulSystemTables.Empty;
var cardIdentifyTables = CardIdentifyTables.Empty;
var ghostExchangeTables = GhostExchangeTables.Empty;
var monsterDropRoller = new MonsterDropRoller(Array.Empty<MonItemDropInfoEntry>(), Array.Empty<MonItemTableDropEntry>());
var skillBook = new SkillBook(Array.Empty<SkillParam2Entry>(), Array.Empty<SkillTableRow>(), Array.Empty<SkillTableRow>());
IReadOnlyDictionary<int, SkillScrollEntry> skillScrolls = new Dictionary<int, SkillScrollEntry>();
var skillCards = new Dictionary<int, SkillCardEntry>();
var teleports = new Dictionary<int, TeleportEntry>();
var npcMoves = new Dictionary<int, NpcMoveEntry>();
IReadOnlyDictionary<int, IReadOnlyList<(int SkillId, int Level)>> skillChecks = new Dictionary<int, IReadOnlyList<(int SkillId, int Level)>>();
IReadOnlyDictionary<int, ItemStatCheckEntry> itemStatChecks = new Dictionary<int, ItemStatCheckEntry>();
var compoundRecipes = new Dictionary<int, CompoundRecipe>();
var drillTables = new DrillTables();
var refineTables = new RefineTables();
var growthTables = new GrowthTables();
var petGrowthTables = new PetGrowthTables();
var setItems = new tPoseidon.Core.Game.Items.SetItemBook(Array.Empty<SetItemEntry>());
IReadOnlyDictionary<int, int> postSetup = new Dictionary<int, int>();
IReadOnlyList<LevelUpGiftEntry> levelUpGifts = Array.Empty<LevelUpGiftEntry>();
var itemPropertyRules = new tPoseidon.Core.Game.Items.ItemPropertyRules(new ItemPropertyTables());
IReadOnlyList<CompounderList> compounderLists = Array.Empty<CompounderList>();
var exchangeOffers = new Dictionary<int, ExchangeOffer>();
IReadOnlyDictionary<string, IReadOnlyList<int>> exchangeLists = new Dictionary<string, IReadOnlyList<int>>();
var vipShopOffers = new Dictionary<int, VipShopOffer>();
IReadOnlyDictionary<string, string> gameMasterKeywords = new Dictionary<string, string>();
IReadOnlyDictionary<string, IReadOnlyList<int>> vipShopLists = new Dictionary<string, IReadOnlyList<int>>();
try
{
    var reader = new LibConfigXmlReader(krLibConfigPath);
    npcMotionCatalog = new InMemoryNpcActionScriptLookup(reader.LoadNpcActionScript());
    shopNpcMotionCatalog = new InMemoryNpcActionScriptLookup(reader.LoadShopNpcActionScript());
    skillShopNpcMotionCatalog = new InMemoryNpcActionScriptLookup(reader.LoadSkillShopNpcActionScript());
    playerMoveSpeeds = reader.LoadMoveSpeedParam()
        .Where(e => e.ObjectType == MoveSpeedParamEntry.PlayerObjectType)
        .Cast<MoveSpeedParamEntry?>()
        .FirstOrDefault();
    Console.WriteLine($"{LogTag} player move speeds from libconfig (MoveSpeedParam): run {playerMoveSpeeds?.RunSpeed}, chase {playerMoveSpeeds?.ChaseSpeed}");
    beginnerRebirth = reader.LoadBeginnerRebirthInfo();
    mapMonsterTables = reader.LoadMapMonsterTables();
    partyZoneTables = reader.LoadPartyZoneTables();
    decomposeDummySkins = reader.LoadKeyedItems(tPoseidon.Core.Game.Items.DecomposeRules.DummyTable, "ItemType", "ItemId");
    equipMergeSupports = reader.LoadKeyedItems(tPoseidon.Core.Game.Items.EquipMergeRules.SupportTable, "ItemId", "SuccesRatio");
    Console.WriteLine($"{LogTag} composing data from libconfig: {petComposingPresents.Count} pet presents, {decomposeDummySkins.Count} dummy skins, {equipMergeSupports.Count} fusion support rows");
    petComposingPresents = reader.LoadPetComposingPresents(tPoseidon.Core.Game.Items.PetComposingPresent.TableName)
        .Select(row => new tPoseidon.Core.Game.Items.PetComposingPresent(row.ItemId, row.MinLevel, row.MaxLevel, row.CashPet)).ToArray();
    Console.WriteLine($"{LogTag} party zones from libconfig: {partyZoneTables.ReEnterMinutes.Count} re-enter limits ({PartyZoneTables.InfoTable}), {partyZoneTables.Bosses.Count} zones with boss tables");
    npcSummonSets = reader.LoadNpcSummonSets(tPoseidon.Core.Game.Quests.MonsterSummonRules.TableName, tPoseidon.Core.Game.Quests.MonsterSummonRules.Columns);
    Console.WriteLine($"{LogTag} NPC monster summons from libconfig: {npcSummonSets.Count} sets ({tPoseidon.Core.Game.Quests.MonsterSummonRules.TableName})");
    tmLevelChart = new TmLevelChart(reader.LoadTmLevels());
    foreach (var rebirthItem in reader.LoadRebirthItems()) rebirthItemsById[rebirthItem.ItemId] = rebirthItem;
    foreach (var mapKind in reader.LoadMapKinds()) mapKindById[mapKind.Id] = mapKind;
    honorTitleBook = new HonorTitleBook(reader.LoadHonorTitles());
    gameMasterKeywords = reader.LoadNameStringTable(tPoseidon.Core.Net.Social.GameMasterChat.KeywordTable);
    Console.WriteLine($"{LogTag} GM chat keywords from libconfig: {gameMasterKeywords.Count} rows ({tPoseidon.Core.Net.Social.GameMasterChat.KeywordTable})");
    memoryPortRows = reader.LoadMemoryPorts().GroupBy(row => row.ItemId).ToDictionary(rows => rows.Key, rows => rows.First());
    Console.WriteLine($"{LogTag} memory ports from libconfig: {memoryPortRows.Count} rows (Item_MemoryPortParam)");
    disguiseKits = reader.LoadTransScrolls().GroupBy(kit => kit.ItemId).ToDictionary(kits => kits.Key, kits => kits.First().TransChrType);
    Console.WriteLine($"{LogTag} disguise kits from libconfig: {disguiseKits.Count} rows (TransScrollInfo)");
    recycleChanges = reader.LoadRecycleChanges().GroupBy(change => change.ExchangeRes).ToDictionary(changes => changes.Key, changes => changes.First());
    Console.WriteLine($"{LogTag} recycle coupon exchange from libconfig: {recycleChanges.Count} rows (RecycleChange)");
    recycleShop = reader.LoadRecycleShop().GroupBy(row => row.RecycleType).ToDictionary(rows => rows.Key, rows => rows.First());
    recycleExcluded = reader.LoadRecycleExceptItems();
    recycleMyCampExcluded = reader.LoadRecycleExceptMyCampItems();
    Console.WriteLine($"{LogTag} item recycle from libconfig: {recycleShop.Count} types ({RecycleShopEntry.TableName}), {recycleExcluded.Count} excluded items ({RecycleShopEntry.ExceptTableName}), {recycleMyCampExcluded.Count} excluded camp items ({RecycleShopEntry.MyCampExceptTableName})");
    ghostExchangeTables = reader.LoadGhostExchangeTables();
    Console.WriteLine($"{LogTag} ghost exchange from libconfig: {ghostExchangeTables.Groups.Count} groups (GhostExchange_Group), {ghostExchangeTables.RefineLevels.Count} refine levels (GhostExchange_RefineLevel)");
    meltingTables = reader.LoadMeltingTables();
    equipReformTables = reader.LoadEquipReformTables();
    petRetrainTables = reader.LoadPetRetrainTables();
    petLevelUpTables = reader.LoadPetLevelUpTables();
    petDigTables = reader.LoadPetDigTables();
    Console.WriteLine($"{LogTag} pet collection from libconfig: {petDigTables.Pets.Count} pets ({PetDigTables.PetTable}), {petDigTables.Foods.Count} foods ({PetDigTables.FoodTable}), {petDigTables.ExItems.Count} EX rows ({PetDigTables.ExTable}), {petDigTables.Areas.Count} areas ({PetDigTables.AreaTable})");
    characterPetReinforceTables = reader.LoadCharacterPetReinforceTables();
    Console.WriteLine($"{LogTag} character pet reinforce from libconfig: {characterPetReinforceTables.Rows.Count} rows ({CharacterPetReinforceTables.TableName})");
    coupleTables = reader.LoadCoupleTables();
    pkInfo = reader.LoadPkInfo();
    guildGameTypes = reader.LoadGuildGameTypeTables();
    fiestaTables = reader.LoadFiestaTables();
    treasureMapTables = reader.LoadTreasureMapTables();
    missionTables = reader.LoadMissionTables();
    harconDefTables = reader.LoadHarconDefTables();
    dailyCycle = new tPoseidon.Core.Game.Quests.DailyCycle(reader.LoadDailyTimeMinutes(tPoseidon.Core.Game.Quests.DailyCycle.TableName));
    Console.WriteLine($"{LogTag} game day from libconfig: {dailyCycle.Phases} phases ({tPoseidon.Core.Game.Quests.DailyCycle.TableName})");
    guildSetup = reader.LoadGuildSetupRow(tPoseidon.Core.Game.Quests.GuildSetupRequirement.TableName, tPoseidon.Core.Game.Quests.GuildSetupRequirement.ServerRow) is { } guildSetupRow
        ? new tPoseidon.Core.Game.Quests.GuildSetupRequirement(guildSetupRow.MinLevel, guildSetupRow.MinGelder)
        : null;
    Console.WriteLine($"{LogTag} guild setup from libconfig: {(guildSetup is null ? "no row" : $"level {guildSetup.MinLevel}, gelder {guildSetup.MinGelder}")} ({tPoseidon.Core.Game.Quests.GuildSetupRequirement.TableName} {tPoseidon.Core.Game.Quests.GuildSetupRequirement.ServerRow})");
    puzzleTables = reader.LoadPuzzleTables();
    Console.WriteLine($"{LogTag} puzzles from libconfig: {puzzleTables.Maps.Count} puzzle maps (PuzzleMapParam), {puzzleTables.Memory.Count} memory puzzles");
    Console.WriteLine($"{LogTag} Harcon Defence from libconfig: {harconDefTables.Stages.Count} stages (HarconDefInfo), {harconDefTables.Waves.Count} wave tables (Sc_HarconDef_*), {harconDefTables.Shop.Count} coin shop rows, {harconDefTables.ShopOfNpc.Count} coin shop NPCs");
    Console.WriteLine($"{LogTag} missions from libconfig: {missionTables.Missions.Count} missions (MissionInfo), {missionTables.Waves.Count} wave tables (MissionMonster_*)");
    Console.WriteLine($"{LogTag} treasure maps from libconfig: {treasureMapTables.Teleports.Count} crevice maps (TreasureMap_Teleport)");
    Console.WriteLine($"{LogTag} Fiesta from libconfig: {fiestaTables.Slots.Count} slots (FiestaInfo), {fiestaTables.LevelPorts.Count} level ports (LevelPortParam)");
    Console.WriteLine($"{LogTag} PK from libconfig: SuperTime {pkInfo.SuperTime}, TodayPKMax {pkInfo.TodayPkMax} (PKInfo)");
    Console.WriteLine($"{LogTag} guild games from libconfig: {guildGameTypes.Types.Count} game types (GuildGameTypeInfo)");
    Console.WriteLine($"{LogTag} couple from libconfig: {coupleTables.Exchange.Count} exchange rows (ExchangeMarriage), {coupleTables.Titles.Count} titles, {coupleTables.Rings.Count} rings, {coupleTables.Dresses.Count} dresses, {coupleTables.OfficiateLines.Count} officiant lines, halls {string.Join("/", coupleTables.HallZones)}");
    Console.WriteLine($"{LogTag} pet level-up from libconfig: {petLevelUpTables.Rows.Count} rows (PetLevelUpInfo)");
    Console.WriteLine($"{LogTag} pet retrain from libconfig: {petRetrainTables.Materials.Count} materials (PetRetrainMaterialInfoEx), {petRetrainTables.LockItems.Count} lock items (PetRetrainMaterialLockItem), curve {(petRetrainTables.Curve.Count == 0 ? "missing" : "retrain_pet")}");
    Console.WriteLine($"{LogTag} equipment reform from libconfig: {equipReformTables.Materials.Count} rows (ItemReformTable), {equipReformTables.Curves.Count} curves (EquipReformRandomTable), {equipReformTables.Elixirs.Count} elixirs (EquipReform_HelpItem), stat range {equipReformTables.RandomStatMinPercent}..{equipReformTables.RandomStatMaxPercent}%, discount {equipReformTables.CostDiscount}");
    soulSystemTables = reader.LoadSoulSystemTables();
    cardIdentifyTables = reader.LoadCardIdentifyTables();
    Console.WriteLine($"{LogTag} card identify from libconfig: {cardIdentifyTables.Grades.Count} grades, {cardIdentifyTables.Results.Count} result tables, {cardIdentifyTables.ComboRatios.Count} combo ratios, exp ratio {cardIdentifyTables.ExpRatio}, TM exp ratio {cardIdentifyTables.TmExpRatio}");
    Console.WriteLine($"{LogTag} soul system from libconfig: {(soulSystemTables.Info is null ? "no" : "1")} SoulSystemInfo row, {soulSystemTables.Sources.Count} sources, {soulSystemTables.PotionCounts.Count} potion rows, {soulSystemTables.Equipment.Count} soul items, {soulSystemTables.CraftsByResult.Count} recipes, {soulSystemTables.Intensify.Count} reinforce rows, {soulSystemTables.TartarosMaps.Count} Tartaros maps");
    Console.WriteLine($"{LogTag} melting from libconfig: {(meltingTables.Info is null ? "no" : "1")} MeltingInfo row, {meltingTables.Drops.Count} bands (MeltingDropInfo), stat range {meltingTables.RandomStatMinPercent}..{meltingTables.RandomStatMaxPercent}% (EquipReformRandomStatRatio)");
    slotExtendTables = reader.LoadSlotExtendTables();
    Console.WriteLine($"{LogTag} slot extend from libconfig: {slotExtendTables.Materials.Count} materials (Slot_Extend_Item), {slotExtendTables.SubItems.Count} sub items (Slot_Extend_SubItem)");
    fortuneTables = reader.LoadFortuneTables();
    Console.WriteLine($"{LogTag} fortune house from libconfig: {fortuneTables.Cards.Count} cards (FortuneCardParam), {fortuneTables.FeeByUse.Count} fees (FortuneGalder), {fortuneTables.Titles.Count} title rules (FortuneRuleTitle)");
    soulSeedTables = reader.LoadSoulSeedTables();
    Console.WriteLine($"{LogTag} soul seeds from libconfig: {soulSeedTables.GuardianTypesBySeed.Count} seeds (SoulSeedParam), {soulSeedTables.EventsByType.Count} events (SoulSeedEventInfo)");
    Console.WriteLine($"{LogTag} honor titles from libconfig: {honorTitleBook.Count} rows (HonorTitleInfo)");
    monsterDropRoller = new MonsterDropRoller(reader.LoadMonsterDropRows(), reader.LoadMonsterDropTables());
    Console.WriteLine($"{LogTag} monster drops from libconfig: {monsterDropRoller.RowCount} rows (MonItemDropInfo), {monsterDropRoller.TableCount} tables (MonItemTable_DropInfo)");
    skillBook = new SkillBook(reader.LoadSkillParam2(), reader.LoadSkillInvocationRows(), reader.LoadSkillActionRows());
    skillScrolls = reader.LoadSkillScrolls().GroupBy(scroll => scroll.ItemId).ToDictionary(scrolls => scrolls.Key, scrolls => scrolls.First());
    Console.WriteLine($"{LogTag} skills from libconfig: {skillBook.Count} rows (SkillParam2 with their ESAII_ and ESAction_ tables)");
    foreach (var card in reader.LoadSkillCards()) skillCards[card.Id] = card;
    foreach (var teleport in reader.LoadTeleportInfo()) teleports[teleport.Id] = teleport;
    foreach (var npcMove in reader.LoadNpcMoves()) npcMoves.TryAdd(npcMove.Id, npcMove);
    skillChecks = reader.LoadSkillChecks();
    itemStatChecks = reader.LoadItemStatChecks();
    Console.WriteLine($"{LogTag} NPC moves from libconfig: {npcMoves.Count} NpcMoveInfo rows, {skillChecks.Count} SkillCheckInfo rows");
    Console.WriteLine($"{LogTag} teleports from libconfig: {teleports.Count} rows (TeleportInfo)");
    foreach (var recipe in reader.LoadCompoundRecipes()) compoundRecipes.TryAdd(recipe.Id, recipe);
    compounderLists = reader.LoadCompounderLists();
    Console.WriteLine($"{LogTag} compound from libconfig: {compoundRecipes.Count} recipes (Compound_Potion/Rare/Throw), {compounderLists.Count} compounder lists (Compounder_Info)");
    foreach (var offer in reader.LoadExchangeOffers()) exchangeOffers.TryAdd(offer.Id, offer);
    drillTables = reader.LoadDrillTables();
    refineTables = reader.LoadRefineTables();
    growthTables = reader.LoadGrowthTables();
    petGrowthTables = reader.LoadPetGrowthTables();
    Console.WriteLine($"{LogTag} pet reinforce from libconfig: {petGrowthTables.PetProperties.Count} {PetGrowthTables.PropertyTable} rows, {petGrowthTables.Materials.Count} {PetGrowthTables.MaterialTable} rows, {petGrowthTables.Aids.Count} {PetGrowthTables.AidTable} rows");
    setItems = new tPoseidon.Core.Game.Items.SetItemBook(reader.LoadSetItems());
    postSetup = reader.LoadPostSetup();
    levelUpGifts = reader.LoadLevelUpGifts();
    Console.WriteLine($"{LogTag} maturing compound from libconfig: {growthTables.EquipProperties.Count} Equip_Property rows, {growthTables.Materials.Count} Maturing_Compound rows, {growthTables.Aids.Count} aid rows");
    var propertyTables = reader.LoadItemPropertyTables();
    itemPropertyRules = new tPoseidon.Core.Game.Items.ItemPropertyRules(propertyTables);
    Console.WriteLine($"{LogTag} refine from libconfig: {refineTables.LevelRows.Count} RefineLevelTable rows, {refineTables.Materials.Sum(rows => rows.Count)} ItemRefineTable rows, {refineTables.Supports.Count} support rows, RefineSeed {refineTables.RefineSeed}; item properties: {propertyTables.RandomStats.Count} RandomStatValueTable rows, {propertyTables.Attributes.Count} Item_Attribute rows");
    Console.WriteLine($"{LogTag} drilling from libconfig: {drillTables.Drills.Count} drills (Item_DrillParam), {drillTables.Zones.Count(zone => zone.Value.HasItems)} zones with items (MapInfoEx), {drillTables.ZoneItems.Count} MapItem_All rows, {drillTables.ItemTables.Count} MapItem_ETC_Table lists, {drillTables.GroundNatures.Count} MapAttr rows");
    var talkRows = reader.LoadAllNpcTalkTables();
    exchangeLists = reader.LoadExchangeLists(talkRows
        .Select(talk => talk.ExchangeShopTable)
        .Where(name => name.Length > 0)
        .ToHashSet(StringComparer.Ordinal));
    foreach (var vipOffer in reader.LoadVipShopOffers()) vipShopOffers.TryAdd(vipOffer.Id, vipOffer);
    vipShopLists = reader.LoadVipShopLists(talkRows
        .Select(talk => talk.VipShopTable)
        .Where(name => name.Length > 0)
        .ToHashSet(StringComparer.Ordinal));
    Console.WriteLine($"{LogTag} egg shops from libconfig: {vipShopOffers.Count} offers (VIPShop), {vipShopLists.Count} NPC lists");
    Console.WriteLine($"{LogTag} exchange shops from libconfig: {exchangeOffers.Count} offers (ExchangeShopContents), {exchangeLists.Count} NPC lists");
    Console.WriteLine($"{LogTag} skill cards from libconfig: {skillCards.Count} rows (Item_SkillCardParam)");
    Console.WriteLine($"{LogTag} beginner revive rule from libconfig (BeginnerRebirthInfo): level {beginnerRebirth?.Level}, HP {beginnerRebirth?.RestoreHpRatio}, MP {beginnerRebirth?.RestoreMpRatio}, exp {beginnerRebirth?.RestoreExpRatio}");
    Console.WriteLine($"{LogTag} loaded NPC motion scripts from libconfig (NPCMotionInfo {npcMotionCatalog.Count}, ShopNPCMotionInfo {shopNpcMotionCatalog.Count}, SkillShopNPCMotionInfo {skillShopNpcMotionCatalog.Count})");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} NPC motion script load FAILED: {ex.GetType().Name}: {ex.Message}");
    npcMotionCatalog = new InMemoryNpcActionScriptLookup(Array.Empty<NpcActionScriptEntry>());
    shopNpcMotionCatalog = new InMemoryNpcActionScriptLookup(Array.Empty<NpcActionScriptEntry>());
    skillShopNpcMotionCatalog = new InMemoryNpcActionScriptLookup(Array.Empty<NpcActionScriptEntry>());
}

var mapInfoJsonPath = RepoRoot.Data("map-info.json");
IMapInfoLookup mapInfoLookup;
try
{
    mapInfoLookup = JsonMapInfoLookup.LoadFromFile(mapInfoJsonPath);
    Console.WriteLine($"{LogTag} loaded {mapInfoLookup.Count} map-info entries from {Path.GetFileName(mapInfoJsonPath)}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} map-info load FAILED: {ex.GetType().Name}: {ex.Message} — falling back to Coral Town stub");
    mapInfoLookup = new JsonMapInfoLookup(new Dictionary<ushort, MapInfoEntry>
    {
        [SpeciesDefaults.CoralTownMapId] = new MapInfoEntry
        {
            Id = SpeciesDefaults.CoralTownMapId,
            Name = "Coral Town - Blooming Cora",
            NpcTable = "NpcTalk_Coral_Town",
        },
    });
}

var worldEntities = new WorldEntityStore();
var npcActivation = new NpcActivation();
var npcIssuedItems = new NpcIssuedItems();
var idAllocator = new IdAllocator();

var serverRatesPath = RepoRoot.Data("server_rates.json");
var serverRatesProvider = new ServerRatesProvider(serverRatesPath, watchForChanges: true);
Console.WriteLine($"{LogTag} server rates: {serverRatesProvider.Current.Summary()} (source: {Path.GetFileName(serverRatesPath)})");
serverRatesProvider.RatesReloaded += r =>
    Console.WriteLine($"{LogTag} server rates reloaded: {r.Summary()}");

var spawnOverridesPath = RepoRoot.Data("spawn_overrides.json");
var spawnOverridesProvider = new SpawnOverridesProvider(spawnOverridesPath, watchForChanges: true);
Console.WriteLine($"{LogTag} spawn overrides: density ×{spawnOverridesProvider.Current.GlobalDensityMultiplier} (source: {Path.GetFileName(spawnOverridesPath)})");

var monsterOverridesPath = RepoRoot.Data("monster_overrides.json");
var monsterOverridesProvider = new MonsterOverridesProvider(monsterOverridesPath, watchForChanges: true);
Console.WriteLine($"{LogTag} monster overrides: {monsterOverridesProvider.Current.ById.Count} entries (source: {Path.GetFileName(monsterOverridesPath)})");

var zonePoolOverridesPath = RepoRoot.Data("zone_pool_overrides.json");
var zonePoolOverridesProvider = new ZonePoolOverridesProvider(zonePoolOverridesPath, watchForChanges: true);
Console.WriteLine($"{LogTag} zone pool overrides: {zonePoolOverridesProvider.Current.ByMap.Count} maps (source: {Path.GetFileName(zonePoolOverridesPath)})");

var spawnPointOverridesPath = RepoRoot.Data("spawn_point_overrides.json");
var spawnPointOverridesProvider = new SpawnPointOverridesProvider(spawnPointOverridesPath, watchForChanges: true);
Console.WriteLine($"{LogTag} spawn point overrides: {spawnPointOverridesProvider.Current.ByMap.Count} maps (source: {Path.GetFileName(spawnPointOverridesPath)})");

var levelExpChart = LevelExpChart.Load(Path.Combine(variantDataRoot, LevelExpChartFile));
var statusChart = StatusValueChart.Load(Path.Combine(variantDataRoot, StatusChartFile));
Console.WriteLine($"{LogTag} charts loaded: {LevelExpChartFile} (level 1 needs {levelExpChart.NextExp(LevelExpChart.FirstLevel)} exp), {StatusChartFile} (stat level 1 gives {statusChart.Value(StatusValueChart.FirstStatLevel, StatusValueChart.Hp)} HP)");

INpcPointProvider npcPointProvider;
var npcPointsCatalogPath = RepoRoot.Data("npc-points.json");
try
{
    var jsonProvider = JsonNpcPointProvider.LoadFromFile(npcPointsCatalogPath);
    Console.WriteLine($"{LogTag} loaded {jsonProvider.Count} NPC spawn points across {jsonProvider.MapCount} maps from {Path.GetFileName(npcPointsCatalogPath)}");
    npcPointProvider = jsonProvider;
}
catch (FileNotFoundException)
{
    Console.Error.WriteLine($"{LogTag} npc-points.json not found at {LogPaths.Scrub(npcPointsCatalogPath)} — falling back to bundled Coral Town defaults");
    npcPointProvider = InMemoryNpcPointProvider.CoralTownDefaults();
}

foreach (var (_, pinnedId) in tPoseidon.Core.Net.World.NpcManager.CoralTownPinnedIds())
{
    idAllocator.Reserve(pinnedId);
}

var npcManager = new tPoseidon.Core.Net.World.NpcManager(
    npcTalkCatalog, npcPointProvider, worldEntities, idAllocator,
    log: msg => Console.WriteLine($"{LogTag} {msg}"),
    pinnedEntityIds: tPoseidon.Core.Net.World.NpcManager.CoralTownPinnedIds());

var monsterManager = new tPoseidon.Core.Game.Monsters.MonsterManager(monsterCatalog, idAllocator, dropCatalog, monsterOverridesProvider, damageLimit: () => ServerRates.CapsSection.Limit(serverRatesProvider.Current.Caps.MaxDamagePerHit));
var groundItems = new GroundItemField();
var globalActionManager = new tPoseidon.Core.Game.Combat.ActionManager();

IPortalLookup? portalLookup = null;
var portalsJsonPath = RepoRoot.Data("portals.json");
try
{
    if (File.Exists(portalsJsonPath))
    {
        portalLookup = JsonPortalLookup.LoadFromPortalsJson(portalsJsonPath);
        Console.WriteLine($"{LogTag} loaded {portalLookup.TotalPortals} portals across {portalLookup.MapCount} maps from {Path.GetFileName(portalsJsonPath)} (used only if the client map files below cannot be read)");
    }
    else
    {
        Console.Error.WriteLine($"{LogTag} portals.json not found at {LogPaths.Scrub(portalsJsonPath)} — warp disabled");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} portal load FAILED: {ex.GetType().Name}: {ex.Message}");
}

var warpManager = new tPoseidon.Core.Game.Warp.WarpManager();

var monsterSpawnsPath = RepoRoot.Data("monster-spawns.json");
var zonePoolsPath = RepoRoot.Data("zone-mob-pools.json");
IMonsterSpawnProvider monsterSpawnProvider;
IZoneMonsterPoolLookup zoneMonsterPools;
try
{
    monsterSpawnProvider = JsonMonsterSpawnProvider.LoadFromFile(monsterSpawnsPath);
    Console.WriteLine($"{LogTag} loaded {((JsonMonsterSpawnProvider)monsterSpawnProvider).TotalPoints} spawn points across {((JsonMonsterSpawnProvider)monsterSpawnProvider).MapCount} maps");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} monster-spawns.json load FAILED: {ex.GetType().Name}: {ex.Message}");
    monsterSpawnProvider = new JsonMonsterSpawnProvider(new Dictionary<ushort, IReadOnlyList<MonsterSpawnPoint>>());
}
try
{
    zoneMonsterPools = JsonZoneMonsterPoolLookup.LoadFromFile(zonePoolsPath);
    Console.WriteLine($"{LogTag} loaded {((JsonZoneMonsterPoolLookup)zoneMonsterPools).TotalPoolRows} zone-mob-pool rows across {((JsonZoneMonsterPoolLookup)zoneMonsterPools).MapCount} maps");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} zone-mob-pools.json load FAILED: {ex.GetType().Name}: {ex.Message}");
    zoneMonsterPools = new JsonZoneMonsterPoolLookup(new Dictionary<ushort, ZoneMonsterPool>());
}

var portalMapDir = Path.Combine(variantDataRoot, "map");
if (Directory.Exists(portalMapDir))
{
    var mapPortals = JsonPortalLookup.LoadFromMapFiles(mapInfoLookup.All, portalMapDir);
    if (mapPortals.MapCount > 0)
    {
        portalLookup = mapPortals;
        Console.WriteLine($"{LogTag} portals from the client map files: {mapPortals.TotalPortals} portals across {mapPortals.MapCount} maps");
    }
}

var clientMapDir = Environment.GetEnvironmentVariable("MT_CLIENT_MAP_DIR");
var mapCreator = string.IsNullOrWhiteSpace(clientMapDir)
    ? new MapCreator(portalLookup)
    : new MapCreator(portalLookup, () => clientMapDir);
var npcCreator = new NpcCreator(npcManager, msg => Console.WriteLine($"{LogTag} {msg}"));
var monsterCreator = new MonsterCreator(
    monsterManager,
    monsterSpawnProvider,
    zoneMonsterPools,
    zonePoolOverridesProvider,
    spawnPointOverridesProvider,
    () => spawnOverridesProvider.Current.GlobalDensityMultiplier);

itemStore = new tPoseidon.Core.Game.Items.MixSlotItemStore(itemStore, () => itemPropertyRules, itemPropertyStore, itemCatalog);
var globalItemManager = new tPoseidon.Core.Game.Items.ItemManager(itemStore, itemCatalog)
{
    DrillLifeOf = itemId => drillTables.Drills.TryGetValue(itemId, out var drill) ? drill.DrillLife : null,
};

var variantMapDir = Path.Combine(variantDataRoot, "map");
var zoneInitializer = new tPoseidon.Core.Net.World.ZoneInitializer(
    globalItemManager,
    monsterManager,
    npcManager,
    globalActionManager,
    regions: regionInfoLookup,
    zonePool: monZoneLookup,
    mapDirectory: Directory.Exists(variantMapDir) ? variantMapDir : null);

var zoneManager = new ZoneManager(
    mapInfoLookup,
    new IZoneCreator[] { mapCreator },
    new IZoneEntityCreator[] { npcCreator, monsterCreator },
    zoneInitializer,
    msg => Console.WriteLine($"{LogTag} [zone] {msg}"));

const string SnapshotVariant = "kr2014";
var snapshotPath = RepoRoot.Data("cache", $"{SnapshotVariant}-world.snapshot");

tPoseidon.Core.WorldSnapshot.WorldSnapshotDto? loadedSnapshot = null;
try
{
    loadedSnapshot = tPoseidon.Core.WorldSnapshot.WorldSnapshotIo.Load(snapshotPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} [snapshot] load failed at {LogPaths.Scrub(snapshotPath)}: {ex.GetType().Name}: {ex.Message} — falling back to inline build");
}

var snapshotReloadListener = new tPoseidon.Core.Data.Postgres.WorldSnapshotReloadListener(
    PostgresAccountStore.ResolveConnectionString(),
    onReload: payload =>
    {
        if (!string.Equals(payload, SnapshotVariant, StringComparison.Ordinal))
        {
            return;
        }
        Console.WriteLine($"{LogTag} [snapshot] hot-reload requested for {SnapshotVariant}; restart this server to apply (atomic swap not yet implemented)");
    },
    logger: msg => Console.WriteLine($"{LogTag} {msg}"));
snapshotReloadListener.Start();

var zoneGeometryByMap = loadedSnapshot is not null && loadedSnapshot.Variant == SnapshotVariant
    ? tPoseidon.Core.WorldSnapshot.ZoneGeometrySnapshot.ReadAll(loadedSnapshot)
    : new Dictionary<ushort, ZoneGeometry>();
Console.WriteLine($"{LogTag} [snapshot] zone geometry for {zoneGeometryByMap.Count} maps (monsters wander only on maps that have it)");

var combatInfoByType = loadedSnapshot is not null && loadedSnapshot.Variant == SnapshotVariant
    ? tPoseidon.Core.WorldSnapshot.CharacterCombatSnapshot.ReadAll(loadedSnapshot)
    : new Dictionary<int, CharacterCombatInfo>();
Console.WriteLine($"{LogTag} [snapshot] combat info for {combatInfoByType.Count} character types (swing timing needs it)");

if (loadedSnapshot is not null && loadedSnapshot.Variant == SnapshotVariant)
{
    Console.WriteLine($"{LogTag} [snapshot] loading {SnapshotVariant} snapshot built {loadedSnapshot.BuildTimestampUtc} ({loadedSnapshot.Zones.Count} zones, {loadedSnapshot.Npcs.Count} NPCs, {loadedSnapshot.Monsters.Count} monsters)");
    zoneManager.BuildWorld();
    var loaded = tPoseidon.Core.WorldSnapshot.WorldSnapshotLoader.Load(
        loadedSnapshot,
        worldEntities,
        idAllocator,
        installNpc: dto =>
        {
            var libName = mapInfoLookup.GetById(dto.MapId)?.NpcTable;
            if (string.IsNullOrEmpty(libName)) return null;
            const string prefix = "NpcTalk_";
            var libNameStripped = libName.StartsWith(prefix, StringComparison.Ordinal)
                ? libName[prefix.Length..] : libName;
            var binding = npcTalkCatalog.GetByTypeOnMap(dto.ChrType, libNameStripped);
            return new NpcEntity
            {
                EntityId = dto.EntityId,
                MapId = dto.MapId,
                ChrType = dto.ChrType,
                PosId = dto.PosId,
                Spawn = tPoseidon.Core.Common.NpcPoint.Xy(dto.SpawnX, dto.SpawnY),
                HeadMarkType = binding?.HeadMarkType ?? 0,
                Source = (object?)binding,
            };
        },
        spawnMonster: _ => false);
    Console.WriteLine($"{LogTag} [snapshot] hydrated {loaded.NpcsInstalled} NPCs + {loaded.MonstersInstalled} monsters from snapshot (boot fast-path)");
}
else
{
    if (loadedSnapshot is not null)
    {
        Console.Error.WriteLine($"{LogTag} [snapshot] variant mismatch: snapshot={loadedSnapshot.Variant} expected={SnapshotVariant} — falling back to inline build");
    }
    zoneManager.BuildWorld();
    zoneManager.SpawnAll();
}
Console.WriteLine($"{LogTag} [zone-init] .lyr loaded={zoneInitializer.LyrLoadCount} missing={zoneInitializer.LyrMissingCount}");

var shopPoints = Md3PointCatalog.Load(
    mapInfoLookup.All.Where(map => map.ShopTable.Length > 0),
    variantMapDir,
    MapPointTypes.Shop);
var shopManager = new tPoseidon.Core.Game.Shop.ShopManager(
    shopZoneLookup, shopPoints, idAllocator,
    log: msg => Console.WriteLine($"{LogTag} {msg}"));
var placedShops = mapInfoLookup.All.Sum(map => shopManager.PreloadShopTable(map.Id, map.ShopTable).Count);
Console.WriteLine($"{LogTag} placed {placedShops} shop NPCs ({shopPoints.Count} shop points read from the map files)");
var skillShopPoints = Md3PointCatalog.Load(
    mapInfoLookup.All.Where(map => map.SkillShopTable.Length > 0),
    variantMapDir,
    MapPointTypes.SkillShop);
var sectorGrids = new System.Collections.Concurrent.ConcurrentDictionary<ushort, tPoseidon.Core.Game.Monsters.SectorGrid?>();
var movingObjectSectors = new System.Collections.Concurrent.ConcurrentDictionary<ushort, Dictionary<ushort, (int Column, int Row)>>();
var skillShopManager = new tPoseidon.Core.Game.Shop.SkillShopManager(
    skillShopZoneLookup, skillShopPoints, idAllocator,
    log: msg => Console.WriteLine($"{LogTag} {msg}"));
var placedSkillShops = mapInfoLookup.All.Sum(map => skillShopManager.PreloadSkillShopTable(map.Id, map.SkillShopTable).Count);
Console.WriteLine($"{LogTag} placed {placedSkillShops} skill shop NPCs ({skillShopPoints.Count} skill shop points read from the map files)");
var skillShopCatalogs = new SkillShopCatalogs
{
    SkillShops = skillShopManager,
    GoodsByTable = shopGoodsByTable,
    PriceRateByMap = shopPriceRateByMap,
    MasterItems = skillMasterItems,
    MasterLists = skillMasterLists,
    Skills = skillBook,
    Cards = skillCards,
    Items = itemCatalog,
};
var shopCatalogs = new ShopCatalogs
{
    Shops = shopManager,
    GoodsByTable = shopGoodsByTable,
    PriceRateByMap = shopPriceRateByMap,
    Items = itemCatalog,
};
var clientTextEncoding = System.Text.Encoding.GetEncoding(ClientTextCodePage);
var monsterRegenCycle = new tPoseidon.Core.Game.Monsters.MonsterRegenCycle(monsterManager, monsterCatalog);
foreach (var mapTable in mapMonsterTables)
{
    if (mapTable.Id < 0 || mapTable.Id > ushort.MaxValue || string.IsNullOrEmpty(mapTable.MonsterTable)) continue;
    var regenMapId = (ushort)mapTable.Id;
    if (mapInfoLookup.GetById(regenMapId) is null) continue;
    if (harconDefTables.Stages.Values.Any(stage => stage.PlayMap == regenMapId)) continue;
    var monsterTableName = mapTable.MonsterTable.StartsWith(MonsterTablePrefix, StringComparison.Ordinal)
        ? mapTable.MonsterTable[MonsterTablePrefix.Length..]
        : mapTable.MonsterTable;
    var regenRows = monZoneLookup.GetByMap(monsterTableName)
        .Select(r => new tPoseidon.Core.Game.Monsters.MonsterRegenRow(r.MonsterId, r.Region, r.Avr, r.Max, r.Ratio))
        .ToArray();
    if (regenRows.Length == 0) continue;
    zoneGeometryByMap.TryGetValue(regenMapId, out var regenGeometry);
    monsterRegenCycle.Configure(regenMapId, mapTable.MonsterAvrNum, regenRows, regenGeometry);
}
if (monsterRegenCycle.MapIds.Count > 0)
{
    foreach (var placed in monsterManager.All.ToArray()) monsterManager.Remove(placed.EntityId);
}
Console.WriteLine($"{LogTag} monster regen configured for {monsterRegenCycle.MapIds.Count} maps from the libconfig Mon_* tables");
Console.WriteLine($"{LogTag} world entity count: {worldEntities.Count} npcs + {monsterManager.Count} monsters");

void DespawnAllMonsters()
{
    var snapshot = monsterManager.All.ToArray();
    foreach (var mob in snapshot)
    {
        monsterManager.Remove(mob.EntityId);
    }
    Console.WriteLine($"{LogTag} despawned {snapshot.Length} monsters (clear-all requested)");
}

void RespawnAllMonsters()
{
    DespawnAllMonsters();
    zoneManager.RespawnAllMonsters();
}

spawnOverridesProvider.OverridesReloaded += overrides =>
{
    Console.WriteLine($"{LogTag} spawn overrides reloaded: density ×{overrides.GlobalDensityMultiplier}" +
        (overrides.ClearAllOnReload ? " [CLEAR]" : "") +
        (overrides.RespawnAllOnReload ? " [RESPAWN]" : ""));

    if (overrides.ClearAllOnReload)
    {
        DespawnAllMonsters();
    }
    if (overrides.RespawnAllOnReload)
    {
        RespawnAllMonsters();
    }

    spawnOverridesProvider.ClearTransientFlags();
};

monsterOverridesProvider.OverridesReloaded += overrides =>
{
    Console.WriteLine($"{LogTag} monster overrides reloaded: {overrides.ById.Count} entries — respawning world");
    RespawnAllMonsters();
};

zonePoolOverridesProvider.OverridesReloaded += overrides =>
{
    Console.WriteLine($"{LogTag} zone pool overrides reloaded: {overrides.ByMap.Count} maps — respawning world");
    RespawnAllMonsters();
};

spawnPointOverridesProvider.OverridesReloaded += overrides =>
{
    Console.WriteLine($"{LogTag} spawn point overrides reloaded: {overrides.ByMap.Count} maps — respawning world");
    RespawnAllMonsters();
};

var commandBlockIds = new CommandBlockIds();
var monsterEffects = new System.Collections.Concurrent.ConcurrentDictionary<ushort, tPoseidon.Core.Game.Skills.Effects.SkillEffectBook>();
var liveSessions = new System.Collections.Concurrent.ConcurrentDictionary<
    tPoseidon.Core.Net.S2.S2SessionContext,
    InGameSessionState>();
var scriptVariables = new tPoseidon.Core.Game.Scripting.Vars.ScriptVariables(new PostgresScriptVariableStore(pg.DataSource));
var scriptEngine = new ScriptEngine(
    Environment.GetEnvironmentVariable("MT_NPC_SCRIPTS_DIR") ?? RepoRoot.Data("scripts", "npc"),
    message => Console.WriteLine($"{LogTag} {message}"));
var scriptWorld = new ScriptWorldServices
{
    Variables = scriptVariables,
    Announce = AnnounceScriptNoticeAsync,
    OnlinePlayers = () => liveSessions.Count,
    TriggerEvent = TriggerScriptEvent,
};
var scriptedNpcs = new Dictionary<string, NpcEntity>(StringComparer.Ordinal);
var scriptReloadGate = new object();
ReloadScripts();
var drillController = new DrillController(
    drillTables,
    zoneGeometryByMap,
    itemStore,
    itemCatalog,
    groundItems,
    idAllocator.NewId,
    SendToMapAsync,
    async digger =>
    {
        foreach (var digSession in liveSessions.ToArray())
        {
            if (!ReferenceEquals(digSession.Value, digger)) continue;
            liveSessions.TryRemove(digSession.Key, out _);
            await digSession.Key.Session.DisposeAsync().ConfigureAwait(false);
        }
    },
    () => treasureMapTables,
    async (monsterId, mapId, x, y) =>
    {
        if (monsterManager.Spawn(monsterId, mapId, x, y) is not { } spotMonster) return false;
        await SendToMapAsync(mapId, Opcode.NPC_SPAWN, MobSpawnFrames.Encode(spotMonster, monsterCatalog.GetById(spotMonster.MonsterParamId))).ConfigureAwait(false);
        return true;
    },
    LogTag);

var itemThrows = new ItemThrowController(itemStore, itemCatalog, groundItems, idAllocator.NewId, SendToMapAsync, LogTag);

var systemLink = new tPoseidon.Core.Net.Social.SystemServerLink(
    tPoseidon.Core.Net.Social.SystemServerLink.InternalAddresses(ServerEndpointConfig.MasterHost),
    msg => Console.WriteLine($"{LogTag} {msg}"));
var partyShares = new tPoseidon.Core.Net.Social.PartyShareBook(systemLink);
var masterOccupation = new tPoseidon.Core.InterServer.MasterOccupation(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}"));
var guildNames = new tPoseidon.Core.Net.Social.GuildNameBook(systemLink, clientTextEncoding);
guildNames.Changed += (characterId, guildName, alias) =>
    liveSessions.Values.FirstOrDefault(s => s.Character?.CharacterId == characterId) is { } guildSt
        ? SendToMapAsync(guildSt.MapId, Opcode.S2C_GUILD_NAME_TAG,
            tPoseidon.Core.Net.Social.GuildFrames.NameTagFor(guildSt.PlayerEntityId, guildName, alias, clientTextEncoding))
        : Task.CompletedTask;
var partyBonuses = new tPoseidon.Core.Net.Social.PartyBonusBook(systemLink);
partyBonuses.Changed += characterId =>
    liveSessions.Values.FirstOrDefault(s => s.Character?.CharacterId == characterId)?.RefreshPartyBonus?.Invoke() ?? Task.CompletedTask;
var partyQuests = new tPoseidon.Core.Net.Social.PartyQuestBook(
    partyQuestCatalog,
    systemLink,
    new tPoseidon.Core.InterServer.MasterPartyQuests(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    characterId => liveSessions.Values.FirstOrDefault(s => s.Character?.CharacterId == characterId)?.Quests?.AsPartyQuestPlayer(),
    clientTextEncoding,
    msg => Console.WriteLine($"{LogTag} {msg}"));

var mail = new MailController(
    new tPoseidon.Core.InterServer.MasterPosts(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    itemStore,
    itemCatalog,
    postSetup,
    clientTextEncoding,
    LogTag);

var cashBox = new CashBoxController(
    new tPoseidon.Core.InterServer.MasterCashBox(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    itemStore,
    itemCatalog,
    LogTag);

var masterGuardian = new tPoseidon.Core.InterServer.MasterGuardian(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}"));

var honorTitles = new HonorTitleController(
    new tPoseidon.Core.InterServer.MasterHonorTitles(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    () => honorTitleBook,
    titled => PartnerNameOf(titled.CouplePartnerId),
    (titled, titleOpcode, titlePayload) =>
    {
        var (titledX, titledY) = PlayerPositionAt(titled, DateTimeOffset.UtcNow);
        return SendToViewersAsync(titled.MapId, titledX, titledY, titleOpcode, titlePayload);
    },
    clientTextEncoding,
    LogTag);

var disguises = new DisguiseController(
    () => disguiseKits,
    formed => formed.Effects.Snapshot().Any(held => held.IsApplied && held.Kind.Morph != tPoseidon.Core.Game.Skills.MorphRule.None),
    (disguised, disguiseOpcode, disguisePayload) =>
    {
        var (disguisedX, disguisedY) = PlayerPositionAt(disguised, DateTimeOffset.UtcNow);
        return SendToViewersAsync(disguised.MapId, disguisedX, disguisedY, disguiseOpcode, disguisePayload);
    },
    LogTag);

var soulSeeds = new SoulSeedController(
    new tPoseidon.Core.InterServer.MasterSoulSeeds(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    masterGuardian,
    itemStore,
    () => soulSeedTables,
    (viewer, _) => ShowGuardiansAsync(viewer),
    LogTag);

var recycle = new RecycleController(() => recycleChanges, () => recycleShop, () => recycleExcluded, () => recycleMyCampExcluded, myCampStore, itemPropertyStore, itemStore, globalItemManager, itemCatalog, LogTag);

var masterUniqueItems = new tPoseidon.Core.InterServer.MasterUniqueItems(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}"));
var masterZoneUserCount = new tPoseidon.Core.InterServer.MasterZoneUserCount(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}"));
var equipUpgrades = new tPoseidon.Core.InterServer.MasterEquipStatusUpgrade(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}"));
var melting = new MeltingController(equipUpgrades, () => meltingTables, itemStore, itemCatalog, itemPropertyStore, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, LogTag);
var cardIdentify = new CardIdentifyController(
    () => cardIdentifyTables,
    itemStore,
    itemCatalog,
    globalItemManager,
    groundItems,
    idAllocator.NewId,
    SendToMapAsync,
    tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity,
    LogTag);
var soulSystem = new SoulController(
    new tPoseidon.Core.InterServer.MasterSoul(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    equipUpgrades,
    () => soulSystemTables,
    itemStore,
    itemCatalog,
    itemPropertyStore,
    tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity,
    LogTag);
var equipReform = new EquipReformController(equipUpgrades, () => equipReformTables, () => refineTables, () => itemPropertyRules, itemStore, itemCatalog, itemPropertyStore, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, LogTag);
var characterPetReinforce = new CharacterPetReinforceController(equipUpgrades, () => characterPetReinforceTables, () => itemPropertyRules, itemStore, itemCatalog, itemPropertyStore, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, LogTag);
var petReinforce = new PetReinforceController(() => petGrowthTables, () => itemPropertyRules, itemStore, itemCatalog, itemPropertyStore, LogTag);
var petLevelUp = new PetLevelUpController(equipUpgrades, () => petLevelUpTables, itemStore, itemCatalog, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, LogTag);
var petEvolveMerge = new PetEvolveMergeController(equipUpgrades, () => petLevelUpTables, () => itemPropertyRules, itemStore, itemCatalog, itemPropertyStore, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, LogTag);
var petCollection = new PetCollectionController(() => petDigTables, () => drillTables, new PostgresPetCollectionStore(pg.DataSource), itemStore, itemCatalog, globalItemManager, LogTag);
var petRetrain = new PetRetrainController(equipUpgrades, () => petRetrainTables, () => itemPropertyRules, itemStore, itemCatalog, itemPropertyStore, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, LogTag);
var itemComposing = new ItemComposingController(equipUpgrades, itemStore, itemCatalog, itemPropertyStore, globalItemManager, petComposingPresents, decomposeDummySkins, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, LogTag);
var itemRecharges = LoadItemRechargeCatalog(variantDataRoot);
var chargeTimer = new ChargeTimerController(
    new tPoseidon.Core.InterServer.MasterItemRecharge(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    itemStore, itemCatalog, itemRecharges, LogTag);
var equipMerge = new EquipMergeController(equipUpgrades, itemStore, itemCatalog, itemPropertyStore, () => itemPropertyRules, () => refineTables, equipMergeSupports, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, LogTag);
var ghostExchange = new GhostExchangeController(equipUpgrades, () => ghostExchangeTables, () => itemPropertyRules, itemStore, itemCatalog, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, LogTag);
var slotExtend = new SlotExtendController(equipUpgrades, () => slotExtendTables, itemStore, itemCatalog, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, LogTag);

var fortune = new FortuneController(
    new tPoseidon.Core.InterServer.MasterFortune(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    () => fortuneTables,
    () => fortuneBasicCards,
    itemStore,
    itemCatalog,
    (seeded, seedEventType) => soulSeeds.EventAsync(seeded, seedEventType, 1),
    LogTag);

var memoryPorts = new MemoryPortController(
    new tPoseidon.Core.InterServer.MasterMemoryPorts(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    itemStore,
    itemCatalog,
    () => memoryPortRows,
    mapId => mapKindById.TryGetValue(mapId, out var portMap) ? portMap.Option : 0,
    porter => PlayerPositionAt(porter, DateTimeOffset.UtcNow),
    clientTextEncoding,
    LogTag);

var trade = new TradeController(
    new tPoseidon.Core.InterServer.MasterTrade(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    itemStore,
    itemCatalog,
    itemPropertyStore,
    clientTextEncoding,
    (mapId, entityId) => liveSessions.Values.FirstOrDefault(traderSt => traderSt.MapId == mapId && traderSt.PlayerEntityId == entityId),
    tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity,
    LogTag);

var harconTactics = new System.Collections.Concurrent.ConcurrentQueue<(ushort EntityId, int TacticId)>();
var harconTacticById = monsterSkillTactics.Values.SelectMany(group => group).GroupBy(tactic => tactic.Id).ToDictionary(group => group.Key, group => group.First());
var harcon = new HarconController(
    () => harconDefTables,
    new PostgresHarconDefStore(pg.DataSource),
    monsterManager,
    zoneGeometryByMap,
    mapId => mapMonsterTables.FirstOrDefault(table => table.Id == mapId).MonsterTable ?? string.Empty,
    () => liveSessions.Values.ToArray(),
    mob => MobSpawnFrames.Encode(mob, monsterCatalog.GetById(mob.MonsterParamId)),
    DespawnOf,
    SendToMapAsync,
    itemStore,
    globalItemManager,
    itemCatalog,
    (entityId, tacticId) => harconTactics.Enqueue((entityId, tacticId)),
    tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity,
    clientTextEncoding,
    LogTag);

var puzzles = new PuzzleController(
    () => puzzleTables,
    monsterManager,
    zoneGeometryByMap,
    () => liveSessions.Values.ToArray(),
    PlayerPositionAt,
    mob => MobSpawnFrames.Encode(mob, monsterCatalog.GetById(mob.MonsterParamId)),
    DespawnOf,
    SendToMapAsync,
    itemStore,
    globalItemManager,
    itemCatalog,
    tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity,
    LogTag);

var myCamps = new MyCampController(
    myCampStore,
    mapId => mapKindById.TryGetValue(mapId, out var campMap) ? campMap.Type : 0,
    zoneGeometryByMap,
    () => liveSessions.Values.ToArray(),
    SendToMapAsync,
    SendToOthersOnMapAsync,
    camper => ShowPlayersAsync(camper, Opcode.NPC_SPAWN),
    idAllocator.NewId,
    clientTextEncoding,
    LogTag);

var missions = new MissionController(
    () => missionTables,
    monsterManager,
    zoneGeometryByMap,
    () => liveSessions.Values.ToArray(),
    mob => MobSpawnFrames.Encode(mob, monsterCatalog.GetById(mob.MonsterParamId)),
    DespawnOf,
    SendToMapAsync,
    LogTag);

var fiesta = new FiestaController(
    () => fiestaTables,
    new PostgresFiestaEntryStore(pg.DataSource),
    mapId => mapKindById.TryGetValue(mapId, out var fiestaMap) ? fiestaMap.Option : 0,
    () => liveSessions.Values.ToArray(),
    SendToMapAsync,
    clientTextEncoding,
    LogTag);

var pk = new PkController(
    mapId => mapKindById.TryGetValue(mapId, out var pkMap) ? pkMap : null,
    () => pkInfo,
    pkInfoStore,
    () => liveSessions.Values.ToArray(),
    SendToMapAsync,
    clientTextEncoding,
    LogTag);

var cardBattles = new CardBattleController(
    systemLink,
    characterId => liveSessions.Values.FirstOrDefault(cardSt => cardSt.Character?.CharacterId == characterId),
    itemStore,
    globalItemManager,
    itemCatalog,
    tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity,
    LogTag);

var guildGames = new GuildGameController(
    systemLink,
    () => guildGameTypes,
    () => liveSessions.Values.ToArray(),
    SendToMapAsync,
    clientTextEncoding,
    LogTag);

using var instantChatSigns = new InstantChatSignController(
    systemLink,
    characterId => liveSessions.Values.FirstOrDefault(signSt => signSt.Character?.CharacterId == characterId),
    () => liveSessions.Values.ToArray(),
    (signSt, signOpcode, signPayload) => SendToViewersAsync(signSt.MapId, signSt.PlayerX, signSt.PlayerY, signOpcode, signPayload),
    LogTag);

var angelLink = new AngelController(
    systemLink,
    characterId => liveSessions.Values.FirstOrDefault(angelSt => angelSt.Character?.CharacterId == characterId),
    new PostgresAngelStore(pg.DataSource),
    SendToMapAsync,
    LogTag);

using var gameMasters = new GameMasterController(
    systemLink,
    characterId => liveSessions.Values.FirstOrDefault(gmSt => gmSt.Character?.CharacterId == characterId),
    gmSt => SendToViewersAsync(gmSt.MapId, gmSt.PlayerX, gmSt.PlayerY, Opcode.S2C_PLAYER_SPAWN_POST_WARP, PlayerSpawnOf(gmSt)),
    new tPoseidon.Core.Net.Social.GameMasterChat(gameMasterKeywords, new Dictionary<string, string>()),
    async kicked =>
    {
        foreach (var kickedSession in liveSessions.ToArray())
        {
            if (!ReferenceEquals(kickedSession.Value, kicked)) continue;
            liveSessions.TryRemove(kickedSession.Key, out _);
            await kickedSession.Key.Session.DisposeAsync().ConfigureAwait(false);
        }
    },
    AnnounceGameMasterSpeedAsync,
    ApplyGameMasterVisibilityAsync,
    PresentGameMasterItemAsync,
    SendToMapAsync,
    clientTextEncoding,
    LogTag)
{
    Players = () => liveSessions.Values.ToArray(),
    FixedBonusRates = () => EnterWorldBatchSynthesizer.FixedBonusRates(serverRatesProvider.Current),
};

var couples = new CoupleController(
    new PostgresCoupleRulesStore(pg.DataSource),
    () => coupleTables,
    itemStore,
    itemCatalog,
    systemLink,
    (mapId, entityId) => liveSessions.Values.FirstOrDefault(loverSt => loverSt.MapId == mapId && loverSt.PlayerEntityId == entityId),
    SendToMapAsync,
    clientTextEncoding,
    LogTag,
    characterId => liveSessions.Values.FirstOrDefault(weddingSt => weddingSt.Character?.CharacterId == characterId),
    mapId => mapKindById.TryGetValue(mapId, out var loverMap) ? loverMap : null,
    disguises);

var playerShops = new PlayerShopController(
    new tPoseidon.Core.InterServer.MasterTrade(() => masterClient, selfIdentity.ServerIndex, msg => Console.WriteLine($"{LogTag} {msg}")),
    itemStore,
    itemCatalog,
    itemPropertyStore,
    trade.IsExchangeable,
    (mapId, entityId) => liveSessions.Values.FirstOrDefault(shopperSt => shopperSt.MapId == mapId && shopperSt.PlayerEntityId == entityId),
    mapId => mapKindById.TryGetValue(mapId, out var shopMap) ? shopMap.Type : 0,
    (shopper, shopOpcode, shopPayload) =>
    {
        var (shopperX, shopperY) = PlayerPositionAt(shopper, DateTimeOffset.UtcNow);
        return SendToViewersAsync(shopper.MapId, shopperX, shopperY, shopOpcode, shopPayload);
    },
    stopped =>
    {
        var (stoppedX, stoppedY) = PlayerPositionAt(stopped, DateTimeOffset.UtcNow);
        stopped.PlayerX = (ushort)stoppedX;
        stopped.PlayerY = (ushort)stoppedY;
        stopped.Walk = null;
    },
    () => liveSessions.Values.ToArray(),
    clientTextEncoding,
    LogTag);

var sessionReleases = new System.Collections.Concurrent.ConcurrentDictionary<S2SessionContext, Func<Task>>();
Func<S2SessionContext, CancellationToken, Task> sessionHandler = (ctx, sessionCt) =>
    {
        systemLink.Register(ctx);

        var actionState = (byte)0x00;

        Character? currentChar = null;
        var returnMapId = tPoseidon.Core.Game.Warp.RestartArrival.FirstReturnMap;
        uint? sessionAccountId = null;
        DateTimeOffset sessionLoginAt = DateTimeOffset.UtcNow;
        tPoseidon.Core.Game.Stats.StatManager? statManager = null;
        ushort moveEntityId = 0;
        var moveRng = new Random();
        var bank = new BankService(
            ctx,
            bankStore,
            itemStore,
            itemCatalog,
            new BankPlayer
            {
                Character = () => currentChar,
                Stats = () => statManager,
                MaxWeight = () => liveSessions.TryGetValue(ctx, out var bankWeightSt) ? bankWeightSt.PlayerMaxWt : 0,
                Properties = StoredItemProperties,
            },
            moveRng,
            LogTag);
        var itemUse = new ItemUseService(
            ctx,
            itemStore,
            itemCatalog,
            new ItemUsePlayer
            {
                Character = () => currentChar,
                Stats = () => statManager,
                EntityId = () => liveSessions.TryGetValue(ctx, out var useSt) ? useSt.PlayerEntityId : (ushort)0,
                MaxHp = () => liveSessions.TryGetValue(ctx, out var useHpSt) ? useHpSt.PlayerMaxHp : 0,
                MaxMp = () => liveSessions.TryGetValue(ctx, out var useMpSt) ? useMpSt.PlayerMaxMp : 0,
                IsDead = () => liveSessions.TryGetValue(ctx, out var useDeadSt) && useDeadSt.IsDead,
                SendToMap = (opcode, payload, blockId) => liveSessions.TryGetValue(ctx, out var useMapSt)
                    ? SendToMapInBlockAsync(useMapSt.MapId, opcode, payload, blockId)
                    : Task.CompletedTask,
                MaxWeight = () => liveSessions.TryGetValue(ctx, out var useWeightSt) ? useWeightSt.PlayerMaxWt : 0,
                StarTears = (tears, tearsCt) => liveSessions.TryGetValue(ctx, out var tearsSt) ? fortune.StarTearsAsync(tearsSt, tears, tearsCt) : Task.FromResult(false),
                LearnSkillCard = (cardDefinition, cardCt) => LearnSkillCardAsync(cardDefinition, cardCt),
                TeleportMap = (mapDefinition, _) => liveSessions.TryGetValue(ctx, out var mapSt)
                    ? drillController.CreateTeleportSpotAsync(mapSt, mapDefinition)
                    : Task.FromResult(false),
                TreasureMap = (treasureDefinition, _) => liveSessions.TryGetValue(ctx, out var treasureSt)
                    ? drillController.CreateTreasureSpotAsync(treasureSt, treasureDefinition)
                    : Task.FromResult(false),
                ReviveTarget = (targetId, rebirthItem, reviveCt) => liveSessions.TryGetValue(ctx, out var reviverSt)
                    ? ReviveWithItemAsync(ctx, reviverSt, targetId, rebirthItem, reviveCt)
                    : Task.FromResult(false),
                FiestaTicket = (ticketDefinition, _) => liveSessions.TryGetValue(ctx, out var ticketSt)
                    ? fiesta.TicketAsync(ticketSt, ticketDefinition)
                    : Task.FromResult<WarpTicketUse?>(null),
                Disguise = (kitId, _) => liveSessions.TryGetValue(ctx, out var kitSt) ? disguises.UseKitAsync(kitSt, kitId) : Task.FromResult(false),
                PlantSeed = (seedItem, seedUid, seedCt) => liveSessions.TryGetValue(ctx, out var seedSt) ? soulSeeds.PlantAsync(seedSt, seedItem, seedUid, seedCt) : Task.FromResult(false),
                SeedEvent = seedEventType => liveSessions.TryGetValue(ctx, out var potionSt) ? soulSeeds.EventAsync(potionSt, seedEventType, 1) : Task.CompletedTask,
                SoulPotion = (soulPotionItem, soulPotionDefinition, _) => liveSessions.TryGetValue(ctx, out var soulPotionSt)
                    ? soulSystem.UsePotionAsync(soulPotionSt, soulPotionItem, soulPotionDefinition)
                    : Task.FromResult(false),
                MonsterCardUsed = usedCard =>
                {
                    if (liveSessions.TryGetValue(ctx, out var cardUseSt)) CardIdentifyController.CardUsed(cardUseSt, usedCard);
                },
            },
            commandBlockIds,
            moveRng,
            LogTag,
            presentItems,
            globalItemManager);
        var compound = new CompoundService(
            ctx,
            itemStore,
            itemCatalog,
            compoundRecipes,
            compounderLists,
            new CompoundPlayer
            {
                Character = () => currentChar,
                Stats = () => statManager,
                Da = () => currentChar is null ? 0 : StatsWithEquipment(currentChar).Da,
            },
            moveRng,
            LogTag);
        var growth = new GrowthService(
            ctx,
            itemStore,
            itemCatalog,
            itemPropertyRules,
            itemPropertyStore,
            growthTables,
            new GrowthPlayer
            {
                Character = () => currentChar,
                Stats = () => statManager,
                Luck = () => liveSessions.TryGetValue(ctx, out var luckSt) ? luckSt.PlayerLk : 0,
                Skills = () => liveSessions.TryGetValue(ctx, out var growthSkillSt) ? growthSkillSt.Skills : new Dictionary<int, int>(),
            },
            moveRng,
            LogTag);
        var refine = new RefineService(
            ctx,
            itemStore,
            itemCatalog,
            itemPropertyRules,
            itemPropertyStore,
            itemPropertyStore,
            refineTables,
            new RefinePlayer
            {
                Character = () => currentChar,
                Stats = () => statManager,
                Fortune = () => liveSessions.TryGetValue(ctx, out var refineSt) ? refineSt.Fortune?.ActiveRefine ?? 0 : 0,
            },
            moveRng,
            LogTag);
        var vipShop = new VipShopService(
            ctx,
            itemStore,
            itemCatalog,
            vipShopOffers,
            vipShopLists,
            new VipShopPlayer
            {
                Character = () => currentChar,
                MaxWeight = () => liveSessions.TryGetValue(ctx, out var vipWeightSt) ? vipWeightSt.PlayerMaxWt : 0,
            },
            moveRng,
            LogTag);
        var exchangeShop = new ExchangeShopService(
            ctx,
            itemStore,
            itemCatalog,
            exchangeOffers,
            exchangeLists,
            new ExchangePlayer
            {
                Character = () => currentChar,
                Stats = () => statManager,
            },
            moveRng,
            LogTag);
        var warehouse = new WarehouseService(
            ctx,
            itemStore,
            itemCatalog,
            new WarehousePlayer
            {
                Character = () => currentChar,
                Stats = () => statManager,
                MaxWeight = () => liveSessions.TryGetValue(ctx, out var storageWeightSt) ? storageWeightSt.PlayerMaxWt : 0,
                Properties = StoredItemProperties,
            },
            moveRng,
            LogTag);
        Func<tPoseidon.Core.Game.Scripting.Lang.EventKind, long?, string?, Task>? scriptEvents = null;
        var npcScript = new NpcScriptExecutor(
            ctx,
            new NpcScriptCatalogs
            {
                Quests = questCatalog,
                QuestResults = questResults,
                MonsterQuests = monsterQuestCatalog,
                MonsterQuestResults = monsterQuestResults,
                PartyQuestResults = partyQuestResults,
                NpcMessages = npcMsgCatalog,
                Items = itemCatalog,
                Teleports = teleports,
                NpcMoves = npcMoves,
                SkillChecks = skillChecks,
                ItemStatChecks = itemStatChecks,
                ItemProperties = () => itemPropertyRules,
                StoredProperties = itemPropertyStore,
                MonsterName = monsterId => monsterCatalog.GetById(monsterId)?.Name,
                GuildSetup = guildSetup,
                PartyZones = partyZoneTables,
            },
            new NpcScriptStores
            {
                Quests = questStore,
                History = questHistoryStore,
                Items = itemStore,
                Hunts = monsterHuntStore,
            },
            new NpcScriptPlayer
            {
                Character = () => currentChar,
                Stats = () => statManager,
                MaxWeight = () => liveSessions.TryGetValue(ctx, out var weightSt) ? weightSt.PlayerMaxWt : 0,
                Skills = () => liveSessions.TryGetValue(ctx, out var skillSt) ? skillSt.Skills : new Dictionary<int, int>(),
                GrantProgress = GiveQuestProgressAsync,
                QuestCleared = async (clear, clearCt) =>
                {
                    if (!liveSessions.TryGetValue(ctx, out var clearSt)) return;
                    await honorTitles.QuestClearedAsync(clearSt, clear, clearCt).ConfigureAwait(false);
                    await soulSeeds.QuestClearedAsync(clearSt, clear).ConfigureAwait(false);
                },
                CheckAwaken = awakenCt => liveSessions.TryGetValue(ctx, out var checkSt)
                    ? soulSeeds.CheckAwakenAsync(checkSt, awakenCt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                AwakenComplete = awakenCt => liveSessions.TryGetValue(ctx, out var awakenSt)
                    ? soulSeeds.AwakenAsync(awakenSt, awakenCt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenLookChange = _ => OpenLookChangeAsync(),
                OpenGhostExchange = (neo, _) => liveSessions.TryGetValue(ctx, out var ghostSt)
                    ? ghostExchange.OpenAsync(ghostSt, neo)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenMelting = _ => liveSessions.TryGetValue(ctx, out var meltingSt)
                    ? melting.OpenAsync(meltingSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenEquipReform = (reformWindow, _) => liveSessions.TryGetValue(ctx, out var reformSt)
                    ? equipReform.OpenAsync(reformSt, reformWindow)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                HarconDefEnter = (harconArgs, _) => liveSessions.TryGetValue(ctx, out var harconSt)
                    ? harcon.EnterAsync(harconSt, harconArgs)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                HarconPlay = (harconFunction, harconPlayArgs, harconNpc, _) => liveSessions.TryGetValue(ctx, out var harconPlaySt)
                    ? harcon.PlayAsync(harconPlaySt, harconFunction, harconPlayArgs, harconNpc)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                LatestBossKills = bossCt => liveSessions.TryGetValue(ctx, out var bossSt) && bossSt.Character is { } bossCharacter
                    ? masterUniqueItems.LatestBossKillsAsync(bossCharacter.CharacterId, bossCt)
                    : Task.FromResult<IReadOnlyList<tPoseidon.Core.Data.BossKillRecord>?>(null),
                ZoneUserCount = (countZone, countCt) => liveSessions.TryGetValue(ctx, out var countSt) && countSt.Character is { } countCharacter
                    ? masterZoneUserCount.CountAsync(countCharacter.CharacterId, countZone, countCt)
                    : Task.FromResult<int?>(null),
                SummonMonsters = (summonSet, _) => liveSessions.TryGetValue(ctx, out var summonSt)
                    ? SummonNpcMonstersAsync(summonSt, summonSet)
                    : Task.CompletedTask,
                StartNpcSkill = (npcSkillId, _) => liveSessions.TryGetValue(ctx, out var npcSkillSt)
                    ? QueueNpcSkill(npcSkillSt, npcSkillId)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenHarconShop = (harconShopNpc, _) => liveSessions.TryGetValue(ctx, out var harconShopSt)
                    ? harcon.OpenShopAsync(harconShopSt, harconShopNpc)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                MissionCheck = (missionArgs, _) => liveSessions.TryGetValue(ctx, out var missionSt)
                    ? missions.CheckAsync(missionSt, missionArgs)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenPetRetrain = _ => liveSessions.TryGetValue(ctx, out var petRetrainSt)
                    ? petRetrain.OpenAsync(petRetrainSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenPetReinforce = _ => liveSessions.TryGetValue(ctx, out var petReinforceSt)
                    ? petReinforce.OpenAsync(petReinforceSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenPetReinforceReset = _ => liveSessions.TryGetValue(ctx, out var petReinforceResetSt)
                    ? petReinforce.OpenResetAsync(petReinforceResetSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenPetLevelUp = _ => liveSessions.TryGetValue(ctx, out var petLevelUpSt)
                    ? petLevelUp.OpenAsync(petLevelUpSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                CoupleFunction = (coupleFunctionId, _) => liveSessions.TryGetValue(ctx, out var coupleSt)
                    ? couples.NpcFunctionAsync(coupleSt, coupleFunctionId, coupleSkill =>
                    {
                        SetSkillLevel(coupleSkill, 1);
                        return true;
                    })
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenChargeTimer = _ => liveSessions.TryGetValue(ctx, out var chargeTimerSt)
                    ? chargeTimer.OpenAsync(chargeTimerSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenEquipMerge = _ => liveSessions.TryGetValue(ctx, out var mergeSt)
                    ? equipMerge.OpenAsync(mergeSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenDecompose = _ => liveSessions.TryGetValue(ctx, out var decomposeSt)
                    ? itemComposing.OpenDecomposeAsync(decomposeSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenPetComposing = _ => liveSessions.TryGetValue(ctx, out var petComposingSt)
                    ? itemComposing.OpenAsync(petComposingSt, tPoseidon.Core.Game.Items.ItemComposingRules.PetKind)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenEquipComposing = _ => liveSessions.TryGetValue(ctx, out var composingSt)
                    ? itemComposing.OpenAsync(composingSt, tPoseidon.Core.Game.Items.ItemComposingRules.EquipmentKind)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenSoulWindow = (soulWindow, _) => liveSessions.TryGetValue(ctx, out var soulWindowSt)
                    ? soulSystem.OpenAsync(soulWindowSt, soulWindow)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenSlotExtend = _ => liveSessions.TryGetValue(ctx, out var slotExtendSt)
                    ? slotExtend.OpenAsync(slotExtendSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                TakeIssuedItem = (issuingNpc, _) => liveSessions.TryGetValue(ctx, out var issueTakerSt)
                    ? TakeIssuedItemAsync(issueTakerSt, issuingNpc)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenItemRecycle = _ => liveSessions.TryGetValue(ctx, out var recycleOpenSt)
                    ? recycle.OpenItemRecycleAsync(recycleOpenSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                OpenMyCampRecycle = async _ =>
                {
                    if (!liveSessions.TryGetValue(ctx, out var campRecycleSt)) return tPoseidon.Core.Game.Quests.NpcScript.Refused;
                    var opened = await recycle.OpenMyCampRecycleAsync(campRecycleSt).ConfigureAwait(false);
                    await myCamps.StartRecycleListAsync(campRecycleSt).ConfigureAwait(false);
                    return opened;
                },
                OpenCouponExchange = _ => liveSessions.TryGetValue(ctx, out var couponSt)
                    ? recycle.OpenCouponExchangeAsync(couponSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                AwakenReset = _ => liveSessions.TryGetValue(ctx, out var resetSt)
                    ? soulSeeds.ResetAwakeningAsync(resetSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                DailyPhase = () => dailyCycle.PhaseAt(DateTimeOffset.UtcNow),
                SoulSeedPlanted = () => liveSessions.TryGetValue(ctx, out var seedSt) && seedSt.Seed.Planted,
                EnterFortuneHouse = _ => liveSessions.TryGetValue(ctx, out var houseSt)
                    ? fortune.OpenAsync(houseSt)
                    : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused),
                Rates = () => serverRatesProvider.Current,
                SetJobQuestState = async (state, stateCt) =>
                {
                    if (currentChar is not { } questChar) return false;
                    if (!await masterOccupation.SetJobQuestStateAsync(questChar.CharacterId, state, stateCt).ConfigureAwait(false)) return false;
                    if (accountStore.GetCharacterById(questChar.CharacterId) is { } refreshed)
                    {
                        currentChar = refreshed;
                        if (liveSessions.TryGetValue(ctx, out var questSt)) questSt.Character = refreshed;
                    }
                    return true;
                },
                OpenGrowth = (reset, growthCt) =>
                {
                    compound.Leave();
                    exchangeShop.Leave();
                    vipShop.Leave();
                    refine.Leave();
                    bank.Leave();
                    warehouse.Leave();
                    return growth.OpenAsync(reset, growthCt);
                },
                OpenPhantomRefine = phantomCt =>
                {
                    growth.Leave();
                    compound.Leave();
                    exchangeShop.Leave();
                    vipShop.Leave();
                    bank.Leave();
                    warehouse.Leave();
                    return refine.OpenPhantomAsync(phantomCt);
                },
                OpenRefine = refineCt =>
                {
                    growth.Leave();
                    compound.Leave();
                    exchangeShop.Leave();
                    vipShop.Leave();
                    bank.Leave();
                    warehouse.Leave();
                    return refine.OpenAsync(refineCt);
                },
                OpenVipShop = (vipTable, vipCt) =>
                {
                    refine.Leave();
                    growth.Leave();
                    compound.Leave();
                    exchangeShop.Leave();
                    bank.Leave();
                    warehouse.Leave();
                    return vipShop.OpenAsync(vipTable, vipCt);
                },
                OpenExchangeShop = (exchangeTable, exchangeCt) =>
                {
                    compound.Leave();
                    bank.Leave();
                    warehouse.Leave();
                    vipShop.Leave();
                    refine.Leave();
                    growth.Leave();
                    return exchangeShop.OpenAsync(exchangeTable, exchangeCt);
                },
                OpenCompound = (compounderTable, compoundCt) =>
                {
                    exchangeShop.Leave();
                    vipShop.Leave();
                    refine.Leave();
                    growth.Leave();
                    bank.Leave();
                    warehouse.Leave();
                    return compound.OpenAsync(compounderTable, compoundCt);
                },
                OpenBank = bankCt =>
                {
                    compound.Leave();
                    exchangeShop.Leave();
                    vipShop.Leave();
                    refine.Leave();
                    growth.Leave();
                    warehouse.Leave();
                    return bank.OpenAsync(bankCt);
                },
                OpenCashBox = cashBoxCt =>
                {
                    compound.Leave();
                    exchangeShop.Leave();
                    vipShop.Leave();
                    refine.Leave();
                    growth.Leave();
                    bank.Leave();
                    warehouse.Leave();
                    return liveSessions.TryGetValue(ctx, out var cashBoxSt) ? cashBox.OpenAsync(cashBoxSt) : Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Refused);
                },
                OpenWarehouse = storageCt =>
                {
                    compound.Leave();
                    exchangeShop.Leave();
                    vipShop.Leave();
                    refine.Leave();
                    growth.Leave();
                    bank.Leave();
                    return warehouse.OpenAsync(storageCt);
                },
            },
            globalItemManager,
            moveRng,
            LogTag);
        npcScript.PartyQuests = partyQuests;
        Func<tPoseidon.Core.Net.Dialogs.ZoneChangeRequest, CancellationToken, Task> requestZoneChange = async (change, changeCt) =>
        {
            try
            {
                if (change.Delay > TimeSpan.Zero) await Task.Delay(change.Delay, changeCt).ConfigureAwait(false);
                zoneGeometryByMap.TryGetValue(change.Zone, out var changeGeometry);
                ushort changeX;
                ushort changeY;
                var namedPoint = change.PointId is { } pointId
                    ? changeGeometry?.PointsOfType(tPoseidon.Core.Game.Quests.NpcMoveRules.DestinationPointType).FirstOrDefault(point => point.Id == pointId)
                    : null;
                if (change.Position is { } position)
                {
                    (changeX, changeY) = position;
                }
                else if (namedPoint is { } point && point.X > 0 && point.Y > 0)
                {
                    (changeX, changeY) = ((ushort)Math.Clamp(point.X, 0, ushort.MaxValue), (ushort)Math.Clamp(point.Y, 0, ushort.MaxValue));
                }
                else if (change.ChangeType == tPoseidon.Core.Game.Warp.TeleportArrival.ChangeZoneType)
                {
                    lock (moveRng) (changeX, changeY) = tPoseidon.Core.Game.Warp.TeleportArrival.Pick(changeGeometry, moveRng);
                }
                else
                {
                    lock (moveRng) (changeX, changeY) = tPoseidon.Core.Game.Warp.RestartArrival.Pick(changeGeometry, moveRng);
                }
                var changeDestination = new PortalDestination
                {
                    DestMapId = change.Zone,
                    DestPortalId = 0,
                    SpawnX = changeX,
                    SpawnY = changeY,
                };
                await SendZoneChangeAsync(changeDestination, change.ChangeType, portalPreamble: true, $"zone change type {change.ChangeType}").ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} zone change to {change.Zone} cancelled: the session ended");
            }
        };
        npcScript.ZoneChangeRequested += requestZoneChange;
        var scriptDialog = new ScriptDialogSession(
            () => scriptEngine.Library,
            NewScriptHost(Array.Empty<string>()),
            (scriptOpcode, scriptPayload, scriptCt) =>
            {
                byte scriptKey;
                lock (moveRng) scriptKey = (byte)moveRng.Next(1, 256);
                return ctx.SendFrameAsync(scriptOpcode, scriptPayload, randKey: scriptKey, scriptCt);
            },
            (scriptNpc, scriptEntityId, scriptCt) => npcScript.OpenAsync(scriptNpc, scriptEntityId, scriptCt),
            scriptMessage => Console.WriteLine($"{LogTag} {ctx.Remote} {scriptMessage}"),
            scriptEngine.InstructionBudget);
        var scriptEventGate = new SemaphoreSlim(1, 1);

        ScriptPlayerHost NewScriptHost(IReadOnlyList<string> scriptArguments) => new(
            npcScript,
            scriptWorld,
            new ScriptPlayerServices
            {
                Position = () =>
                {
                    if (!liveSessions.TryGetValue(ctx, out var positionSt)) return (0, 0, 0);
                    var (positionX, positionY) = PlayerPositionAt(positionSt, DateTimeOffset.UtcNow);
                    return (positionSt.MapId, (ushort)positionX, (ushort)positionY);
                },
                IsGameMaster = () => liveSessions.TryGetValue(ctx, out var gameMasterSt) && gameMasterSt.GameMasterAttribute,
                Tell = (scriptText, tellCt) => liveSessions.TryGetValue(ctx, out var tellSt) ? TellScriptLineAsync(tellSt, scriptText, tellCt) : Task.CompletedTask,
                Arguments = scriptArguments,
            });

        async Task RunScriptEventAsync(tPoseidon.Core.Game.Scripting.Lang.EventKind eventKind, long? eventSubject, string? eventName)
        {
            await scriptEventGate.WaitAsync(sessionCt).ConfigureAwait(false);
            try
            {
                await scriptEngine.RunEventAsync(eventKind, eventSubject, eventName, NewScriptHost(Array.Empty<string>()), sessionCt).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} [scripts] {eventKind} event stopped: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                scriptEventGate.Release();
            }
        }

        scriptEvents = RunScriptEventAsync;

        async Task<bool> TryHandleScriptChatAsync(InGameSessionState speaker, string chatText)
        {
            var trimmed = chatText.Trim();
            if (speaker.GameMasterAttribute && trimmed.StartsWith("/script", StringComparison.OrdinalIgnoreCase))
            {
                var action = trimmed[7..].Trim();
                var library = scriptEngine.Library;
                if (action.Equals("reload", StringComparison.OrdinalIgnoreCase))
                {
                    var reloaded = ReloadScripts();
                    await TellScriptLineAsync(speaker, $"Scripts reloaded: {reloaded.FilesLoaded} file(s) loaded, {reloaded.FilesFailed} failed.", sessionCt).ConfigureAwait(false);
                    foreach (var reloadError in reloaded.Errors.Take(3)) await TellScriptLineAsync(speaker, reloadError, sessionCt).ConfigureAwait(false);
                    return true;
                }
                await TellScriptLineAsync(speaker, $"Scripts: {library.Npcs.Count} npc(s), {library.OverrideCount} override(s), {library.EventCount} event(s), {library.Timers.Count} timer(s), {library.CommandCount} command(s). Use /script reload.", sessionCt).ConfigureAwait(false);
                return true;
            }
            if (!ScriptEngine.TryParseChatCommand(trimmed, out var commandName, out var commandArguments)) return false;
            return await scriptEngine.RunCommandAsync(
                commandName,
                commandArguments,
                speaker.GameMasterAttribute,
                NewScriptHost,
                (line, tellCt) => TellScriptLineAsync(speaker, line, tellCt),
                sessionCt).ConfigureAwait(false);
        }
        var shopper = new ShopPlayer
        {
            Character = () => currentChar,
            Stats = () => statManager,
            MaxWeight = () => liveSessions.TryGetValue(ctx, out var shopWeightSt) ? shopWeightSt.PlayerMaxWt : 0,
            MapId = () => currentChar?.MapId ?? 0,
        };
        var npcShop = new NpcShopService(
            ctx,
            shopCatalogs,
            shopper,
            itemStore,
            globalItemManager,
            clientTextEncoding,
            moveRng,
            LogTag);
        var equipment = new EquipmentService(
            ctx,
            itemStore,
            itemCatalog,
            new EquipmentPlayer
            {
                Character = () => currentChar,
                EntityId = () => moveEntityId,
                MapId = () => currentChar?.MapId ?? 0,
                StatTotals = () => currentChar is null
                    ? Array.Empty<int>()
                    : tPoseidon.Core.Game.Items.EquipmentRules.Totals(StatsWithEquipment(currentChar)),
                SendToMap = SendToMapAsync,
                EquipmentChanged = ApplyEquipmentChangeAsync,
            },
            moveRng,
            LogTag);
        var skillShop = new SkillShopService(
            ctx,
            skillShopCatalogs,
            new SkillShopPlayer
            {
                Shopper = shopper,
                EntityId = () => moveEntityId,
                TypeIndex = () => currentChar is null ? (ushort)0 : (ushort)StatGrowth.TypeIndexOf(currentChar.FType),
                Skills = () => liveSessions.TryGetValue(ctx, out var skillShopSt) ? skillShopSt.Skills : new Dictionary<int, int>(),
                SetSkillLevel = SetSkillLevel,
                SendToMap = SendToMapAsync,
                WornCount = () => currentChar is null ? 0 : tPoseidon.Core.Game.Items.EquipmentRules.WornBy(itemStore, itemCatalog, currentChar.CharacterId).Count,
                FreeBagSlots = () => currentChar is null
                    ? 0
                    : tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity - itemStore.GetForCharacterBySlotKind(currentChar.CharacterId, SlotKind.Inventory).Count,
                ChangeJob = ChangeJobAsync,
                JobQuests = npcScript,
            },
            itemStore,
            globalItemManager,
            clientTextEncoding,
            moveRng,
            LogTag);

        ctx.Dispatcher.Register(Opcode.GAME_INIT, async (_, payload, innerCt) =>
        {
            var peerIp = ctx.Remote?.Address ?? System.Net.IPAddress.None;
            var pending = accountStore.FindPendingGameJoin(peerIp, TimeSpan.FromSeconds(30));
            if (pending is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} GAME_INIT no pending state=3 row for IP {peerIp} (lobby/channel handshake missing or expired); dropping");
                return;
            }
            sessionAccountId = pending.AccountId;

            var accountCharacters = accountStore.GetCharactersForAccount(pending.AccountId);
            if (accountCharacters.Count == 0)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} GAME_INIT account {pending.AccountId} has no characters; dropping");
                return;
            }

            Character? character = null;
            if (pending.SelectedCharacterId != 0)
            {
                character = accountStore.GetCharacterById(pending.SelectedCharacterId);
                if (character is null || character.OwnerAccountId != pending.AccountId)
                {
                    Console.Error.WriteLine($"{LogTag} {ctx.Remote} GAME_INIT selected char_uid=0x{pending.SelectedCharacterId:X16} not owned by account {pending.AccountId}; dropping");
                    return;
                }
            }
            else
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} GAME_INIT account {pending.AccountId} has no selected_char_uid (state=3 row missing CharSelect upstream); dropping");
                return;
            }

            if (character.PositionX < 1 || character.PositionY < 1)
            {
                zoneGeometryByMap.TryGetValue(character.MapId, out var loginGeometry);
                ushort loginX;
                ushort loginY;
                lock (moveRng) (loginX, loginY) = tPoseidon.Core.Game.Warp.RestartArrival.Pick(loginGeometry, moveRng);
                accountStore.SetCharacterPosition(character.CharacterId, character.MapId, loginX, loginY);
                character = accountStore.GetCharacterById(character.CharacterId) ?? character;
                Console.WriteLine($"{LogTag} {ctx.Remote} GAME_INIT — character saved without a position starts at ({loginX},{loginY}) on map {character.MapId}");
            }
            if (character.CurrentHp < 1)
            {
                var maxHpAtLogin = Math.Max(0L, (long)StatsWithEquipment(character).Hp);
                character.CurrentHp = tPoseidon.Core.Game.Stats.StatManager.RestartHp(maxHpAtLogin);
                Console.WriteLine($"{LogTag} {ctx.Remote} GAME_INIT — character saved without HP logs in with {character.CurrentHp} of {maxHpAtLogin}");
            }
            currentChar = character;
            TrackReturnMap(atLogin: true);
            moveEntityId = idAllocator.NewId();
            EnsureStatManagerWired();
            npcScript.LoadForCharacter(character.CharacterId);

            try
            {
                accountStore.TransitionToInGame(pending.AccountId, pending.Island);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} GAME_INIT state=3->4 failed: {ex.Message}");
                idAllocator.Release(moveEntityId);
                return;
            }

            ctx.Guard.Authenticated();

            var safetyRecord = safetyPasswordStore.Find(pending.AccountId);
            var lockFlag = safetyRecord?.LockFlag ?? 0;
            if (lockFlag != 0)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} GAME_INIT account={pending.AccountId} safety-lock active (lock_flag=0x{lockFlag:X2}); client-side dialog flow expected (no server-side gating yet)");
            }

            var entityId = moveEntityId;

            Console.WriteLine($"{LogTag} {ctx.Remote} GAME_INIT — char='{character.Name}' " +
                $"({character.Species} {character.FType} lvl {character.Level}) " +
                $"hair={character.Hair} mapId={character.MapId} entity=0x{entityId:X4}");

            var loginTimedItems = tPoseidon.Core.Game.Items.TimedItemRules.Running(itemStore.GetAllForCharacter(character.CharacterId), DateTimeOffset.UtcNow);
            var loginTitles = await honorTitles.LoadAsync(character.CharacterId, innerCt).ConfigureAwait(false);
            var loginFortune = await fortune.LoadAsync(character.CharacterId, DateTimeOffset.UtcNow, innerCt).ConfigureAwait(false);
            if (loginTitles is not null && FortuneController.ClearsProfileTitle(loginFortune, loginTitles.ProfileTitleType))
            {
                loginTitles = loginTitles with { ProfileTitleType = 0, ProfileTitleId = 0 };
                await honorTitles.SaveProfileTitleAsync(character.CharacterId, ProfileTitle.None, innerCt).ConfigureAwait(false);
            }
            await EnterWorldBatchSynthesizer.SendBatchAsync(
                ctx, character, entityId, dialogService,
                npcScript.LoginLists(character.CharacterId),
                worldEntities, characterInfoCatalog,
                monsterManager, itemStore,
                skillStore,
                rates: serverRatesProvider.Current,
                shops: shopManager,
                skillShops: skillShopManager,
                activation: npcActivation,
                itemOptions: loginItem => tPoseidon.Core.Game.Items.ItemPropertyRules.ForClient(
                    itemPropertyStore.ForItem(loginItem.ItemUid), itemCatalog.GetById(loginItem.ItemIndex)),
                timedItems: loginTimedItems,
                inView: (x, y) => InViewOn(character.MapId, character.PositionX, character.PositionY, x, y),
                titles: honorTitles.LoginFrames(loginTitles, entityId, PartnerNameOf(CouplePartnerOf(character.CharacterId))),
                fortune: FortuneController.AppliedFrame(loginFortune),
                fortuneTitleId: (uint)(loginFortune?.Info.TitleId ?? 0),
                abilityPercents: gameMasters.Rates.AbilityPacket(),
                petBag: petCollection.ConnectPhaseBag(character.CharacterId),
                ct: innerCt).ConfigureAwait(false);

            Console.WriteLine($"{LogTag} {ctx.Remote} 34-frame enter-world batch sent");
            if (await mail.CountFrameAsync(character.CharacterId).ConfigureAwait(false) is { } loginPostCount)
            {
                await ctx.SendFrameAsync((Opcode)tPoseidon.Core.Net.Social.PostFrames.Count, loginPostCount, (byte)Random.Shared.Next(1, 256), innerCt).ConfigureAwait(false);
            }

            var sessionState = liveSessions.GetOrAdd(ctx, _ => new InGameSessionState
            {
                SendFrameAsync = (op, payload, rk, ct) => ctx.SendFrameAsync(op, payload, rk, ct),
            });
            sessionState.Fortune = loginFortune;
            sessionState.TimedItems.Clear();
            foreach (var timedItem in loginTimedItems) sessionState.TimedItems.Track(timedItem);
            sessionState.ReleaseExpiredItem = expiredUid => equipment.ReleaseExpiredAsync(expiredUid, sessionCt);
            sessionState.MapId = character.MapId;
            sessionState.PlayerEntityId = entityId;
            sessionState.Quests = npcScript;
            sessionState.RunScriptEvent = RunScriptEventAsync;
            sessionState.PlayerX = character.PositionX;
            sessionState.PlayerY = character.PositionY;
            sessionState.PlayerAttackSpeedStat = ResolveAttackSpeedStat(character);
            ApplyCombatStats(sessionState, StatsWithEquipment(character));
            sessionState.PlayerCharacterType = (int)character.Species;
            sessionState.Disguise.Reset(sessionState.PlayerCharacterType);
            sessionState.Stats = statManager;
            sessionState.Character = character;
            sessionState.ActiveSets = ActiveSetIds(character);
            ArmProgress(sessionState, character);
            honorTitles.Attach(sessionState, loginTitles);
            await soulSeeds.LoadAsync(sessionState, character.CharacterId, innerCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.GAME_PHASE_2, (_, _, _) =>
        {
            Console.WriteLine($"{LogTag} {ctx.Remote} GAME_PHASE_2 received (informational; batch already sent)");
            return Task.CompletedTask;
        });

        async Task EmitMobSpawnsForCurrentMapAsync(string trigger)
        {
            if (currentChar is null) return;
            var mobs = monsterManager.GetByMap(currentChar.MapId);
            if (mobs.Count == 0)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote}   no mobs on map {currentChar.MapId} ({trigger})");
                return;
            }
            byte rk = 0x42;
            foreach (var mob in mobs)
            {
                if (!InViewOn(currentChar.MapId, currentChar.PositionX, currentChar.PositionY, mob.CurrentX, mob.CurrentY)) continue;
                var spawnPayload = MobSpawnFrames.Encode(mob, monsterCatalog.GetById(mob.MonsterParamId));
                await ctx.SendFrameAsync(Opcode.NPC_SPAWN, spawnPayload, randKey: rk, default).ConfigureAwait(false);
                rk = (byte)(rk + 0x2B);
            }
            Console.WriteLine($"{LogTag} {ctx.Remote}   emitted {mobs.Count} mob spawns for map {currentChar.MapId} ({trigger})");
        }

        async Task EmitGroundItemsForCurrentMapAsync()
        {
            if (currentChar is null) return;
            var lying = groundItems.OnMap(currentChar.MapId);
            foreach (var item in lying)
            {
                if (!InViewOn(currentChar.MapId, currentChar.PositionX, currentChar.PositionY, item.X, item.Y)) continue;
                byte rkItem;
                lock (moveRng) rkItem = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_ITEM_ON_GROUND, GroundItemFrames.AlreadyThere(item), randKey: rkItem, default).ConfigureAwait(false);
            }
            if (lying.Count > 0)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote}   emitted {lying.Count} items on the ground for map {currentChar.MapId}");
            }
        }

        ctx.Dispatcher.Register(Opcode.C2S_UNK_4A5, async (_, _, _) =>
        {
            Console.WriteLine($"{LogTag} {ctx.Remote} 0x04A5 received (post-batch checkpoint)");
            if (liveSessions.TryGetValue(ctx, out var readyState))
            {
                readyState.ClientReady = true;
                await ShowPlayersAsync(readyState, Opcode.PLAYER_SPAWN_LOGIN).ConfigureAwait(false);
                await SendPassiveStatsAsync(sessionCt).ConfigureAwait(false);
                _ = RunScriptEventAsync(tPoseidon.Core.Game.Scripting.Lang.EventKind.Login, null, null);
                _ = RunScriptEventAsync(tPoseidon.Core.Game.Scripting.Lang.EventKind.MapEnter, readyState.MapId, null);
            }
            if (currentChar is not null)
            {
                await systemLink.EnteredZoneAsync(ctx, currentChar.CharacterId, currentChar.MapId, sessionCt).ConfigureAwait(false);
                await partyQuests.RestoreAsync(currentChar.CharacterId, sessionCt).ConfigureAwait(false);
                if (readyState is not null) await couples.EnteredZoneAsync(readyState).ConfigureAwait(false);
                if (readyState is not null) await pk.EnteredZoneAsync(readyState, modeOffAlreadySent: true).ConfigureAwait(false);
                if (readyState is not null) await fiesta.EnteredZoneAsync(readyState).ConfigureAwait(false);
                if (readyState is not null) await missions.EnteredZoneAsync(readyState).ConfigureAwait(false);
                if (readyState is not null) await angelLink.LoginAsync(readyState).ConfigureAwait(false);
                if (readyState is not null) await harcon.EnteredZoneAsync(readyState).ConfigureAwait(false);
                if (readyState is not null) await petCollection.LoginAsync(readyState).ConfigureAwait(false);
                if (readyState is not null) await gameMasters.EnteredZoneAsync(readyState).ConfigureAwait(false);
            }
            await EmitMobSpawnsForCurrentMapAsync("0x04A5 post-batch").ConfigureAwait(false);
            await EmitGroundItemsForCurrentMapAsync().ConfigureAwait(false);
            await ShowMapPetsAsync().ConfigureAwait(false);
            await SyncPetAsync(withEffect: false).ConfigureAwait(false);
            await SyncGuardianAsync(sessionCt).ConfigureAwait(false);
            await SendGuideHistoryAsync().ConfigureAwait(false);
        });

        async Task SendGuideHistoryAsync()
        {
            if (currentChar is null) return;
            byte rkGuide;
            lock (moveRng) rkGuide = (byte)moveRng.Next(1, 256);
            GuideHistory? history;
            try
            {
                history = guideHistoryStore.GetForCharacter(currentChar.CharacterId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} [tutorial] guide history read failed: {ex.GetType().Name}: {ex.Message} — answered 0x04A7");
                await ctx.SendFrameAsync(Opcode.S2C_GUIDE_HISTORY_CANCEL, Array.Empty<byte>(), randKey: rkGuide, default).ConfigureAwait(false);
                return;
            }
            var tutorialGuide = history?.TutorialGuide ?? 0UL;
            await ctx.SendFrameAsync(
                Opcode.GAME_UNK_4A6,
                GuideHistoryFrames.History(GuideHistoryFrames.FromStored(history?.SystemGuide), tutorialGuide),
                randKey: rkGuide,
                default).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote} [tutorial] sent the guide history: tutorial step {tutorialGuide}{(history is null ? " (nothing stored yet)" : string.Empty)}");
        }

        ctx.Dispatcher.Register(Opcode.C2S_OP_04A4_TBD, (_, payload, _) =>
        {
            if (GuideHistoryFrames.ReadTutorialGuide(payload.Span) is not { } tutorialStep)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x04A4 expected {GuideHistoryFrames.TutorialGuideSize}B got {payload.Length} — drop");
                return Task.CompletedTask;
            }
            if (currentChar is null) return Task.CompletedTask;
            guideHistoryStore.UpsertTutorialGuide(currentChar.CharacterId, tutorialStep);
            Console.WriteLine($"{LogTag} {ctx.Remote} [tutorial] the client is now on tutorial step {tutorialStep}; stored");
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_SYSTEM_GUIDE_UPDATE, (_, payload, _) =>
        {
            if (GuideHistoryFrames.ReadSystemGuide(payload.Span) is not { } systemGuide)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x04A3 expected {GuideHistoryFrames.SystemGuideSize}B got {payload.Length} — drop");
                return Task.CompletedTask;
            }
            if (currentChar is null) return Task.CompletedTask;
            guideHistoryStore.UpsertSystemGuide(currentChar.CharacterId, GuideHistoryFrames.Stored(systemGuide));
            Console.WriteLine($"{LogTag} {ctx.Remote} [tutorial] help notices seen updated; stored");
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_OP_034D_CLIENT_LOCAL_PING, (_, _, _) => Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_OP_04AF_CLIENT_LOCAL_PING, (_, _, _) => Task.CompletedTask);

        var sessionMovType = (byte)0x02;
        int playerLastX = currentChar?.PositionX ?? 0;
        int playerLastY = currentChar?.PositionY ?? 0;
        int playerPrevX = playerLastX;
        int playerPrevY = playerLastY;

        ctx.Dispatcher.Register(Opcode.ENTITY_MOVE, async (_, payload, moveCt) =>
        {
            if (liveSessions.TryGetValue(ctx, out var chatHeldSt) && chatHeldSt.InstantChatState is not null) return;
            if (payload.Length != C2SMovementPacket.PayloadSize)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} ENTITY_MOVE bad length {payload.Length} (expected {C2SMovementPacket.PayloadSize}) — drop");
                return;
            }
            var move = C2SMovementPacket.DecodePayload(payload.Span);
            if (liveSessions.TryGetValue(ctx, out var deadMoveSt) && deadMoveSt.IsDead) return;

            var moveNow = DateTimeOffset.UtcNow;
            var (serverX, serverY) = ((int)move.FromX, (int)move.FromY);
            IReadOnlyList<(int X, int Y)>? route = [(move.ToX, move.ToY)];
            if (liveSessions.TryGetValue(ctx, out var moveSt))
            {
                (serverX, serverY) = PlayerPositionAt(moveSt, moveNow);
                if (zoneGeometryByMap.TryGetValue(moveSt.MapId, out var moveGeometry) && moveGeometry is not null)
                {
                    route = tPoseidon.Core.Game.Monsters.WanderRoute.FindWaypoints(moveGeometry, serverX, serverY, move.ToX, move.ToY);
                }
            }
            var (goalX, goalY) = route is { } found ? found[^1] : (serverX, serverY);
            playerPrevX = playerLastX;
            playerPrevY = playerLastY;
            playerLastX = goalX;
            playerLastY = goalY;
            if (moveSt is not null)
            {
                moveSt.PlayerX = (ushort)goalX;
                moveSt.PlayerY = (ushort)goalY;
                moveSt.PlayerMoveType = move.Flag;
                moveSt.Walk = route is { } walkRoute
                    ? tPoseidon.Core.Game.Pets.OwnerWalk.Along(serverX, serverY, walkRoute,
                        playerMoveSpeeds is { } walkSpeeds ? SpeedOfMoveType(walkSpeeds, tPoseidon.Core.Game.Combat.MeleeApproach.EffectiveMoveType(move.Flag), moveSt.GameMasterSpeed) : 0f,
                        moveNow)
                    : null;
                moveSt.PickupItemEntityId = 0;
                moveSt.PickupWalkEnd = null;
                moveSt.PortalWalk = null;
                if (moveSt.Dig is { Begun: false } walkingDig)
                {
                    walkingDig.WalkEnd = null;
                    walkingDig.BeginsAt = DateTimeOffset.UtcNow;
                }
                if (moveSt.Cast is { IsUnderWay: true })
                {
                    moveSt.Cast = null;
                    byte rkCastCancel;
                    lock (moveRng) rkCastCancel = (byte)moveRng.Next(1, 256);
                    await ctx.SendFrameAsync(Opcode.SKILL_CAST_CANCEL, Array.Empty<byte>(), randKey: rkCastCancel, moveCt).ConfigureAwait(false);
                }
                if (moveSt.CurrentTargetEntityId != 0)
                {
                    monsterManager.Get(moveSt.CurrentTargetEntityId)?.RemoveAttacker(moveSt.PlayerEntityId);
                    moveSt.CurrentTargetEntityId = 0;
                    moveSt.NextSwingAt = default;

                    var leaveAttack = new tPoseidon.Core.Net.PacketWriter(MeleeSwingFrames.LeaveAttackSize);
                    leaveAttack.WriteUInt16(moveSt.PlayerEntityId);
                    var leaveAttackPayload = leaveAttack.ToArray();
                    foreach (var watcher in liveSessions.ToArray())
                    {
                        if (watcher.Value.MapId != moveSt.MapId || !watcher.Value.ClientReady) continue;
                        byte rkLeave;
                        lock (moveRng) rkLeave = (byte)moveRng.Next(1, 256);
                        await watcher.Key.SendFrameAsync(Opcode.S2C_OP_0020_TBD, leaveAttackPayload, randKey: rkLeave, moveCt).ConfigureAwait(false);
                    }
                }
            }

            if (route is null)
            {
                var stopped = GroundItemFrames.Stopped(moveEntityId, (ushort)serverX, (ushort)serverY, move.Flag);
                byte stopKey;
                lock (moveRng) stopKey = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_OP_0019_TBD, stopped, randKey: stopKey, moveCt).ConfigureAwait(false);
                if (moveSt is not null) await SendToOthersOnMapAsync(moveSt, Opcode.S2C_OP_0019_TBD, stopped).ConfigureAwait(false);
                return;
            }

            var moveSpeedQuarter = ScaledSpeedQuarter(move.Flag == 0x02 ? EntitySpeedPacket.RunSpeedQuarter : EntitySpeedPacket.WalkSpeedQuarter, moveSt?.GameMasterSpeed ?? 1f);
            if ((move.Flag == 0x01 || move.Flag == 0x02)
                && (move.Flag != sessionMovType || (moveSt is not null && moveSt.AnnouncedSpeedQuarter != moveSpeedQuarter)))
            {
                sessionMovType = move.Flag;
                var speedFrame = new EntitySpeedPacket
                {
                    EntityId = moveEntityId,
                    SpeedQuarter = moveSpeedQuarter,
                };
                byte speedRandKey;
                lock (moveRng) speedRandKey = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_ENTITY_SPEED, speedFrame.EncodePayload(),
                    randKey: speedRandKey, moveCt).ConfigureAwait(false);
                if (moveSt is not null) await SendToOthersOnMapAsync(moveSt, Opcode.S2C_ENTITY_SPEED, speedFrame.EncodePayload()).ConfigureAwait(false);
                if (liveSessions.TryGetValue(ctx, out var speedSt)) speedSt.AnnouncedSpeedQuarter = speedFrame.SpeedQuarter;
                Console.WriteLine($"{LogTag} {ctx.Remote} R-toggle: movType=0x{move.Flag:X2} → 0x01D8 speed=0x{speedFrame.SpeedQuarter:X2}");
            }

            var echo = new EntityMovementPacket
            {
                EntityId = moveEntityId,
                FromX = (ushort)serverX,
                FromY = (ushort)serverY,
                ToX = (ushort)goalX,
                ToY = (ushort)goalY,
                Flag = move.Flag,
            };

            byte randKey;
            lock (moveRng) randKey = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.ENTITY_MOVE, echo.EncodePayload(), randKey: randKey, moveCt)
                .ConfigureAwait(false);
            if (moveSt is not null) await SendToOthersOnMapAsync(moveSt, Opcode.ENTITY_MOVE, echo.EncodePayload()).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_REST_ACTION, async (_, payload, actionCt) =>
        {
            if (payload.Length != 1)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} C2S 0x0040 expected 1B payload, got {payload.Length} — drop");
                return;
            }
            var newState = payload.Span[0];
            if (liveSessions.TryGetValue(ctx, out var deadActionSt) && deadActionSt.IsDead) return;
            actionState = newState;
            if (deadActionSt is not null && NaturalRecovery.AmountsFor(deadActionSt.RecoveryType, newState) is { } postureAmounts)
            {
                deadActionSt.RecoveryAmounts = postureAmounts;
            }

            var broadcast = new byte[3]
            {
                (byte)(moveEntityId & 0xFF),
                (byte)((moveEntityId >> 8) & 0xFF),
                newState,
            };
            byte randKey;
            lock (moveRng) randKey = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.C2S_REST_ACTION, broadcast,
                randKey: randKey, actionCt).ConfigureAwait(false);
            if (deadActionSt is not null) await SendToOthersOnMapAsync(deadActionSt, Opcode.C2S_REST_ACTION, broadcast).ConfigureAwait(false);

            var label = newState switch
            {
                0x00 => "standing",
                0x02 => "sit",
                0x0D => "lie down",
                _    => $"unknown(0x{newState:X2})",
            };
            Console.WriteLine($"{LogTag} {ctx.Remote} action: {label} → echoed S2C 0x0040 broadcast");

            if (newState == 0x02 || newState == 0x0D)
            {
                if (liveSessions.TryGetValue(ctx, out var actSt))
                {
                    if (actSt.CurrentTargetEntityId != 0)
                    {
                        var prev = monsterManager.Get(actSt.CurrentTargetEntityId);
                        if (prev is not null) prev.RemoveAttacker(actSt.PlayerEntityId);
                    }
                    actSt.CurrentTargetEntityId = 0;
                }
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_UID_ACTION, (_, payload, useCt) => itemUse.UseAsync(payload, useCt));
        ctx.Dispatcher.Register(Opcode.C2S_ITEM_USE_ON_TARGET, (_, payload, useCt) =>
            liveSessions.TryGetValue(ctx, out var userSt)
                ? itemUse.UseOnTargetAsync(payload, targetId =>
                    liveSessions.Values.Any(targetSt => targetSt.MapId == userSt.MapId && targetSt.PlayerEntityId == targetId)
                    || monsterManager.Get(targetId) is { } targetMonster && targetMonster.MapId == userSt.MapId, useCt)
                : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_ITEM_USE_WITH_DATA, async (_, payload, useCt) =>
        {
            if (liveSessions.TryGetValue(ctx, out var porterSt) && await memoryPorts.TryUseAsync(porterSt, payload, useCt).ConfigureAwait(false)) return;
            await itemUse.UseAsync(payload, useCt).ConfigureAwait(false);
        });
        ctx.Dispatcher.Register(Opcode.C2S_MEMORY_PORT_OPEN, (_, payload, portCt) =>
            liveSessions.TryGetValue(ctx, out var openPortSt) ? memoryPorts.OpenAsync(openPortSt, payload, portCt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MEMORY_PORT_SAVE, (_, payload, portCt) =>
            liveSessions.TryGetValue(ctx, out var savePortSt) ? memoryPorts.SaveAsync(savePortSt, payload, portCt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MEMORY_PORT_DELETE, (_, payload, portCt) =>
            liveSessions.TryGetValue(ctx, out var deletePortSt) ? memoryPorts.DeleteAsync(deletePortSt, payload, portCt) : Task.CompletedTask);

        ushort targetedNpcEntityId = 0;

        Task ReleaseSessionAsync()
        {
            FlushStatsToDb("disconnect");
            npcScript.SaveHunt();
            if (currentChar is not null && liveSessions.TryGetValue(ctx, out var leavingSt) && leavingSt.IsDead)
            {
                accountStore.SetCharacterPosition(currentChar.CharacterId, returnMapId, 0, 0);
                Console.WriteLine($"{LogTag} {ctx.Remote}   left while dead: saved on the return map {returnMapId} without a position");
            }
            else if (currentChar is not null)
            {
                ushort persistX = (playerLastX > 0 || playerLastY > 0) ? (ushort)playerLastX : currentChar.PositionX;
                ushort persistY = (playerLastX > 0 || playerLastY > 0) ? (ushort)playerLastY : currentChar.PositionY;
                accountStore.SetCharacterPosition(
                    currentChar.CharacterId, currentChar.MapId, persistX, persistY);
                Console.WriteLine($"{LogTag} {ctx.Remote}   position flushed (disconnect): map={currentChar.MapId} pos=({persistX},{persistY}){((playerLastX == 0 && playerLastY == 0) ? " [no-move; using currentChar load value]" : "")}");
            }
            if (sessionAccountId is not null)
            {
                var logoutAt = DateTimeOffset.UtcNow;
                var playTimeSec = (int)Math.Max(0, (logoutAt - sessionLoginAt).TotalSeconds);
                accountLogoutStore.Append(sessionAccountId.Value, sessionLoginAt, logoutAt, 0, playTimeSec);
                accountStore.TransitionToOffline(sessionAccountId.Value);
                Console.WriteLine($"{LogTag} {ctx.Remote}   account {sessionAccountId.Value} -> state=0 (offline); play_time={playTimeSec}s");
            }
            if (!liveSessions.TryRemove(ctx, out var goneSt)) return Task.CompletedTask;
            var tradeLeft = trade.LeaveAsync(goneSt);
            _ = couples.LeftAsync(goneSt);
            _ = missions.LeftAsync(goneSt);
            _ = harcon.LeftAsync(goneSt);
            _ = guildGames.LeftAsync(goneSt);
            _ = myCamps.LeftAsync(goneSt);
            _ = puzzles.LeftAsync(goneSt);
            if (goneSt.CurrentTargetEntityId != 0)
            {
                var prev = monsterManager.Get(goneSt.CurrentTargetEntityId);
                if (prev is not null) prev.RemoveAttacker(goneSt.PlayerEntityId);
            }
            var leaving = goneSt.Engagements.TakeAll()
                .Select(left => ReleaseMonsterOwnerAsync(goneSt.MapId, left, goneSt.PlayerEntityId))
                .ToList();
            foreach (var leftEffect in goneSt.Effects.Clear()) idAllocator.Release(leftEffect.ObjectId);
            leaving.Add(tradeLeft);
            leaving.Add(SendToMapAsync(goneSt.MapId, Opcode.S2C_GROUND_ITEM_DESPAWN, GroundItemFrames.Removed(goneSt.PlayerEntityId)));
            idAllocator.Release(goneSt.PlayerEntityId);
            if (goneSt.Pet is { } leftPet)
            {
                goneSt.Pet = null;
                leaving.Add(RemovePetAsync(leftPet));
            }
            return Task.WhenAll(leaving);
        }
        sessionReleases[ctx] = ReleaseSessionAsync;

        ushort ResolveAttackSpeedStat(Character c) => c.DxPoints;

        void EnsureStatManagerWired()
        {
            if (statManager is not null || currentChar is null) return;
            statManager = new tPoseidon.Core.Game.Stats.StatManager(
                currentChar,
                capsProvider: () => serverRatesProvider.Current.Caps);

            var sm = statManager;
            statManager.MoneyChanged += ev =>
            {
                if (ev.Delta == 0) return;
                Console.WriteLine($"{LogTag} {ctx.Remote}   money → {ev.NewTotal} (delta {ev.Delta:+#;-#;0}) source={ev.Source}");
            };
            statManager.HealthChanged += async ev =>
            {
                if (sessionCt.IsCancellationRequested) return;
                try
                {
                    var pkt = tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(ev.NewHp);
                    byte rk;
                    lock (moveRng) rk = (byte)moveRng.Next(1, 256);
                    await ctx.SendFrameAsync(Opcode.S2C_HEALTH_STATUS,
                        pkt.EncodePayload(), randKey: rk, default).ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { }
                catch (System.IO.IOException) { }
            };

            ctx.Dispatcher.Register(Opcode.C2S_STAT_ALLOCATE, async (_, payload, allocCt) =>
            {
                if (currentChar is null || statManager is null || !liveSessions.TryGetValue(ctx, out var allocSt)) return;
                if (payload.Length < StatPointSpending.RequestSize)
                {
                    Console.Error.WriteLine($"{LogTag} {ctx.Remote} C2S 0x006C is {payload.Length} bytes, {StatPointSpending.RequestSize} expected: dropped");
                    return;
                }

                var amounts = StatPointSpending.ReadAmounts(payload.Span);
                var statPoints = StatusValueChart.PointsOf(currentChar);
                byte allocRk;
                lock (moveRng) allocRk = (byte)moveRng.Next(1, 256);
                if (!StatPointSpending.TrySpend(statPoints, currentChar.AttrPoints, amounts, out var bonusPointsLeft))
                {
                    Console.WriteLine($"{LogTag} {ctx.Remote} [progress] stat points refused: asked [{string.Join(",", amounts)}] with {currentChar.AttrPoints} bonus points");
                    await ctx.SendFrameAsync(Opcode.S2C_STAT_POINTS_REFUSED, Array.Empty<byte>(), randKey: allocRk, allocCt).ConfigureAwait(false);
                    return;
                }

                var spentStats = StatsWithEquipment(currentChar, statPoints);
                ApplyCombatStats(allocSt, spentStats);
                statManager.SetVitals(
                    Math.Min(statManager.CurrentHp, allocSt.PlayerMaxHp),
                    Math.Min(statManager.CurrentMp, allocSt.PlayerMaxMp));
                FlushStatsToDb("stat-points", statPoints, (ushort)bonusPointsLeft);
                currentChar = accountStore.GetCharacterById(currentChar.CharacterId) ?? currentChar;
                allocSt.Character = currentChar;
                Console.WriteLine($"{LogTag} {ctx.Remote} [progress] stat points spent [{string.Join(",", amounts)}], {bonusPointsLeft} left; HP max {spentStats.Hp}, MP max {spentStats.Mp}");

                await ctx.SendFrameAsync(Opcode.S2C_STAT_POINTS,
                    ProgressFrames.StatPoints((ushort)bonusPointsLeft, partyBonuses.WithBonus(currentChar.CharacterId, statPoints)), randKey: allocRk, allocCt).ConfigureAwait(false);
            });

            statManager.MoneyChanged += _ =>
            {
                try
                {
                    FlushStatsToDb("money-change");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{LogTag} {ctx.Remote} stats flush (money-change) failed: {ex.GetType().Name}: {ex.Message}");
                }
            };
        }

        tPoseidon.Core.Game.Characters.EffectiveStats StatsWithEquipment(Character character, IReadOnlyList<short>? points = null)
        {
            var worn = tPoseidon.Core.Game.Items.EquipmentRules.WornBy(itemStore, itemCatalog, character.CharacterId);
            return tPoseidon.Core.Game.Items.EquipmentRules.WithEquipment(
                gameMasters.Rates.WithAbilities(statusChart.Compute(partyBonuses.WithBonus(character.CharacterId, points ?? StatusValueChart.PointsOf(character)))),
                worn.Select(held => held.Definition),
                WithFortune(tPoseidon.Core.Game.Items.SetItemBook.Bonus(setItems.Active(worn.Select(held => held.Definition.Id)))));
        }

        int[] WithFortune(int[] setBonus)
        {
            if (!liveSessions.TryGetValue(ctx, out var fortuneSt) || fortuneSt.Fortune is not { Active: true } held) return setBonus;
            var stats = held.ActiveStats;
            for (var slot = 0; slot < setBonus.Length && slot < stats.Count; slot++) setBonus[slot] += stats[slot];
            return setBonus;
        }

        IReadOnlySet<int> ActiveSetIds(Character character) =>
            setItems.Active(tPoseidon.Core.Game.Items.EquipmentRules.WornBy(itemStore, itemCatalog, character.CharacterId).Select(held => held.Definition.Id))
                .Select(set => set.Id).ToHashSet();

        async Task ApplyEquipmentChangeAsync()
        {
            ApplyEquipmentChange();
            if (currentChar is null || !liveSessions.TryGetValue(ctx, out var setSt)) return;
            var now = ActiveSetIds(currentChar);
            foreach (var on in now.Except(setSt.ActiveSets).Order())
            {
                await SendToMapAsync(setSt.MapId, Opcode.GAME_UNK_345, tPoseidon.Core.Net.World.EquipmentFrames.SetChanged(setSt.PlayerEntityId, on, true)).ConfigureAwait(false);
            }
            foreach (var off in setSt.ActiveSets.Except(now).Order())
            {
                await SendToMapAsync(setSt.MapId, Opcode.GAME_UNK_345, tPoseidon.Core.Net.World.EquipmentFrames.SetChanged(setSt.PlayerEntityId, off, false)).ConfigureAwait(false);
            }
            setSt.ActiveSets = now;
        }

        async Task<bool> LearnSkillCardAsync(ItemParamEntry cardDefinition, CancellationToken learnCt)
        {
            if (currentChar is not { } learner || statManager is not { } learnStats || !liveSessions.TryGetValue(ctx, out var learnSt)) return false;
            if (!skillCards.TryGetValue(cardDefinition.Id, out var card))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] card {cardDefinition.Id} has no Item_SkillCardParam row");
                return false;
            }
            var learnerJob = OccupationRules.CurrentJobOf(learner);
            byte rkLearn;
            lock (moveRng) rkLearn = (byte)moveRng.Next(1, 256);
            if (tPoseidon.Core.Game.Skills.SkillCardRules.Refusal(card, learnSt.Skills, learnerJob, learnStats.TmLevel, learnStats.TmPoint) is { } learnNotice)
            {
                await ctx.SendFrameAsync(Opcode.S2C_SKILL_NOTICE,
                    SkillFrames.Notice(tPoseidon.Core.Game.Skills.SkillCardRules.LearnNotice, (ushort)card.AffectionSkill, learnNotice),
                    randKey: rkLearn, learnCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] card {card.Id} for skill {card.AffectionSkill} refused with notice {learnNotice} (job {learnerJob}, TM level {learnStats.TmLevel}, TM points {learnStats.TmPoint})");
                return false;
            }
            var learnPointsLeft = learnStats.SpendTmPoints(card.AffectionPoint);
            SetSkillLevel(card.AffectionSkill, card.RegisterLevel);
            await ctx.SendFrameAsync(Opcode.S2C_SKILL_LEARNED,
                SkillFrames.Record((ushort)card.AffectionSkill, (byte)card.RegisterLevel, (ushort)card.AffectionPoint),
                randKey: rkLearn, learnCt).ConfigureAwait(false);
            await SendToMapAsync(learnSt.MapId, Opcode.S2C_SKILL_LEARN_SEEN, SkillFrames.LearnSeen(learnSt.PlayerEntityId)).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote} [skill] card {card.Id} teaches skill {card.AffectionSkill} level {card.RegisterLevel} for {card.AffectionPoint} TM points, {learnPointsLeft} left");
            if (skillBook.Find(card.AffectionSkill) is { IsPassive: true } learnedPassive && PassiveStats.IsStatPassive(learnedPassive.ActionId))
            {
                await SendPassiveStatsAsync(learnCt).ConfigureAwait(false);
            }
            return true;
        }

        void SetSkillLevel(int skillId, int level)
        {
            if (currentChar is null || !liveSessions.TryGetValue(ctx, out var levelSt)) return;
            levelSt.Skills = new Dictionary<int, int>(levelSt.Skills) { [skillId] = level };
            skillStore.Upsert(new Skill { CharacterId = currentChar.CharacterId, SkillId = skillId, SkillLevel = level });
            FlushStatsToDb("skill-level");
        }

        async Task<bool> ChangeJobAsync(byte tier, ushort type, byte job, CancellationToken changeCt)
        {
            if (currentChar is not { } changer || !liveSessions.TryGetValue(ctx, out var jobSt)) return false;
            var changed = await masterOccupation.ChangeAsync(
                changer.CharacterId, new tPoseidon.Core.InterServer.OccupationChange(type, job, 0, tier), changeCt).ConfigureAwait(false);
            if (changed is null) return false;
            var uniquesBeforeJobChange = WornAbsoluteUniques();
            var released = await equipment.ReleaseAllAsync(changeCt).ConfigureAwait(false);
            await WithdrawReleasedUniquesAsync(uniquesBeforeJobChange, changeCt).ConfigureAwait(false);
            await SyncPetAsync(withEffect: false).ConfigureAwait(false);
            if (accountStore.GetCharacterById(changer.CharacterId) is { } updated)
            {
                currentChar = updated;
                jobSt.Character = updated;
            }
            ApplyEquipmentChange();
            await SendToMapAsync(jobSt.MapId, (Opcode)tPoseidon.Core.Net.World.SkillShopFrames.JobChanged,
                tPoseidon.Core.Net.World.SkillShopFrames.JobChangedFor(jobSt.PlayerEntityId, type, job, 0, tier)).ConfigureAwait(false);
            byte jobRk;
            lock (moveRng) jobRk = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.SKILL_SHOP_CLOSE, Array.Empty<byte>(), randKey: jobRk, changeCt).ConfigureAwait(false);
            await SendToMapAsync(jobSt.MapId, (Opcode)tPoseidon.Core.Net.World.SkillShopFrames.AppearanceChanged,
                tPoseidon.Core.Net.World.SkillShopFrames.BareAppearance(jobSt.PlayerEntityId)).ConfigureAwait(false);
            await systemLink.SendAsync(tPoseidon.Core.Net.Social.SystemLinkFrames.CharacterTypeChanged,
                tPoseidon.Core.Net.Social.SystemLinkFrames.CharacterTypeChangedFor(changer.CharacterId, (byte)type, job), changeCt).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote} [job-change] '{changer.Name}' is now type {type}, job {job} (index {tier}); {released} items moved to the bag");
            return true;
        }

        async Task<int> OpenLookChangeAsync()
        {
            if (currentChar is not { } looker || statManager is null || !liveSessions.TryGetValue(ctx, out var lookSt)) return tPoseidon.Core.Game.Quests.NpcScript.Refused;
            var worn = tPoseidon.Core.Game.Items.EquipmentRules.WornBy(itemStore, itemCatalog, looker.CharacterId).Count;
            var freeSlots = tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity - itemStore.GetForCharacterBySlotKind(looker.CharacterId, SlotKind.Inventory).Count;
            if (tPoseidon.Core.Game.Characters.LookChangeRules.OpenRefusal(statManager.Money, OccupationRules.CurrentJobOf(looker), worn, freeSlots, lookSt.Disguise.Disguised) is { } lookRefusal)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [look-change] window refused with result {lookRefusal}");
                return lookRefusal;
            }
            byte lookRk;
            lock (moveRng) lookRk = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_LOOK_CHANGE_OPEN,
                tPoseidon.Core.Net.World.LookChangeFrames.Looks(tPoseidon.Core.Game.Characters.LookChangeRules.Looks(looker)), randKey: lookRk, default).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote} [look-change] window opened (job index {looker.JobState})");
            return tPoseidon.Core.Game.Quests.NpcScript.Silent;
        }

        async Task CloseLookTalkAsync(InGameSessionState talker, string reason, CancellationToken closeCt)
        {
            Console.WriteLine($"{LogTag} {ctx.Remote} [look-change] {reason}; dialog closed");
            byte closeRk;
            lock (moveRng) closeRk = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), randKey: closeRk, closeCt).ConfigureAwait(false);
            talker.Quests?.Close();
        }

        async Task ChangeLookAsync(ReadOnlyMemory<byte> lookPayload, CancellationToken lookCt)
        {
            if (currentChar is not { } looker || statManager is null || !liveSessions.TryGetValue(ctx, out var lookSt)) return;
            if (!tPoseidon.Core.Net.World.LookChangeFrames.TryReadRequest(lookPayload.Span, out var lookState) || lookSt.Quests is not { IsEngaged: true }) return;
            var worn = tPoseidon.Core.Game.Items.EquipmentRules.WornBy(itemStore, itemCatalog, looker.CharacterId).Count;
            var freeSlots = tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity - itemStore.GetForCharacterBySlotKind(looker.CharacterId, SlotKind.Inventory).Count;
            if (!tPoseidon.Core.Game.Characters.LookChangeRules.RequestAllowed(statManager.Money, OccupationRules.CurrentJobOf(looker), worn, freeSlots))
            {
                await CloseLookTalkAsync(lookSt, $"request refused: money {statManager.Money}, job {OccupationRules.CurrentJobOf(looker)}, {worn} worn, {freeSlots} free slots", lookCt).ConfigureAwait(false);
                return;
            }
            if (lookSt.LookChangeBusy)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [look-change] request while one is pending");
                return;
            }
            if (lookState == looker.JobState)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [look-change] appearance {lookState} is already shown");
                return;
            }
            if (tPoseidon.Core.Game.Characters.LookChangeRules.MakeUpOf(looker, lookState) is not { } makeUp)
            {
                await CloseLookTalkAsync(lookSt, $"!SetMakeUp for appearance {lookState}", lookCt).ConfigureAwait(false);
                return;
            }
            lookSt.LookChangeBusy = true;
            try
            {
                if (!await masterOccupation.SetJobIndexAsync(looker.CharacterId, (byte)lookState, lookCt).ConfigureAwait(false))
                {
                    await CloseLookTalkAsync(lookSt, $"hItemChangeAppearance::OnCancel for appearance {lookState}", lookCt).ConfigureAwait(false);
                    return;
                }
                if (accountStore.GetCharacterById(looker.CharacterId) is { } relooked)
                {
                    currentChar = relooked;
                    lookSt.Character = relooked;
                }
                var uniquesBeforeLookChange = WornAbsoluteUniques();
                var released = await equipment.ReleaseAllAsync(lookCt).ConfigureAwait(false);
                await WithdrawReleasedUniquesAsync(uniquesBeforeLookChange, lookCt).ConfigureAwait(false);
                await SyncPetAsync(withEffect: false).ConfigureAwait(false);
                ApplyEquipmentChange();
                statManager.GrantMoney(-tPoseidon.Core.Game.Characters.LookChangeRules.Fee, StatChangeSource.LookChange);
                byte moneyRk;
                lock (moveRng) moneyRk = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_MONEY, GroundItemFrames.Money(statManager.Money), randKey: moneyRk, lookCt).ConfigureAwait(false);
                await SendToMapAsync(lookSt.MapId, (Opcode)tPoseidon.Core.Net.World.SkillShopFrames.JobChanged,
                    tPoseidon.Core.Net.World.LookChangeFrames.Changed(lookSt.PlayerEntityId, makeUp, (byte)lookState)).ConfigureAwait(false);
                await SendToMapAsync(lookSt.MapId, (Opcode)tPoseidon.Core.Net.World.SkillShopFrames.AppearanceChanged,
                    tPoseidon.Core.Net.World.SkillShopFrames.BareAppearance(lookSt.PlayerEntityId)).ConfigureAwait(false);
                await systemLink.SendAsync(tPoseidon.Core.Net.Social.SystemLinkFrames.CharacterTypeChanged,
                    tPoseidon.Core.Net.Social.SystemLinkFrames.CharacterTypeChangedFor(looker.CharacterId, (byte)makeUp.Type, makeUp.Job), lookCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [look-change] '{looker.Name}' shows appearance {lookState}: type {makeUp.Type}, job {makeUp.Job}; {released} items moved to the bag, fee {tPoseidon.Core.Game.Characters.LookChangeRules.Fee}");
                await CloseLookTalkAsync(lookSt, "appearance changed", lookCt).ConfigureAwait(false);
            }
            finally
            {
                lookSt.LookChangeBusy = false;
            }
        }

        void ApplyEquipmentChange()
        {
            if (currentChar is null || statManager is null || !liveSessions.TryGetValue(ctx, out var gearSt)) return;
            var geared = StatsWithEquipment(currentChar);
            ApplyCombatStats(gearSt, geared);
            statManager.SetVitals(
                Math.Min(statManager.CurrentHp, gearSt.PlayerMaxHp),
                Math.Min(statManager.CurrentMp, gearSt.PlayerMaxMp));
        }

        void ApplyCombatStats(InGameSessionState state, tPoseidon.Core.Game.Characters.EffectiveStats stats)
        {
            state.CombatBase = stats;
            ApplyEffectDeltas(state);
        }

        async Task SendPassiveStatsAsync(CancellationToken passiveCt)
        {
            if (!liveSessions.TryGetValue(ctx, out var passiveSt)) return;
            var passiveDeltas = ApplyEffectDeltas(passiveSt);
            foreach (var passiveStat in PassiveStats.StatsOf(passiveSt.Skills, skillBook))
            {
                byte passiveRk;
                lock (moveRng) passiveRk = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_OP_011B_TBD, SkillEffectFrames.StatChanged(passiveStat, passiveDeltas[passiveStat]),
                    randKey: passiveRk, passiveCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] passive stat {passiveStat} delta {passiveDeltas[passiveStat]}");
            }
        }

        void ArmProgress(InGameSessionState state, Character character)
        {
            state.RecoveryType = StatGrowth.TypeIndexOf(character.FType);
            if (NaturalRecovery.AmountsFor(state.RecoveryType, NaturalRecovery.StandAction) is { } standing)
            {
                state.RecoveryAmounts = standing;
            }
            else
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} character type {character.FType} has no recovery amounts");
            }
            state.Recovery.Period = partyBonuses.RecoveryPeriod(character.CharacterId, NaturalRecovery.PeriodMs);
            state.RefreshPartyBonus = async () =>
            {
                if (currentChar is null || statManager is null) return;
                var bonusStats = StatsWithEquipment(currentChar);
                ApplyCombatStats(state, bonusStats);
                statManager.SetVitals(
                    Math.Min(statManager.CurrentHp, state.PlayerMaxHp),
                    Math.Min(statManager.CurrentMp, state.PlayerMaxMp));
                state.Recovery.Period = partyBonuses.RecoveryPeriod(currentChar.CharacterId, NaturalRecovery.PeriodMs);
                byte bonusRk;
                lock (moveRng) bonusRk = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_STAT_POINTS,
                    ProgressFrames.StatPoints(currentChar.AttrPoints, partyBonuses.WithBonus(currentChar.CharacterId, StatusValueChart.PointsOf(currentChar))),
                    randKey: bonusRk, CancellationToken.None).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [party] type bonus {partyBonuses.BonusOf(currentChar.CharacterId)?.ToString() ?? "none"}: recovery every {state.Recovery.Period} ms");
            };
            state.RecomputeStats = ApplyEquipmentChange;
            state.GainHitExp = gain => GainExp(gain, fromSwing: true, gmRate: true);
            state.GainExpTotal = gain => GainExp(gain, fromSwing: false, gmRate: true);
            state.GainRewardExp = gain => GainExp(gain, fromSwing: false, gmRate: false);
            state.GainRewardTmExp = gain => GainTmExpFrom(gain, ProgressFrames.TmFromMonsterKill, gmRate: false);
            state.GainKillTmExp = GainTmExp;
            state.GainSkillTmExp = gain => GainTmExpFrom(gain, (byte)StatGrowth.TypeIndexOf(character.FType), gmRate: true);
            state.RequestZoneChange = requestZoneChange;
            state.CouplePartnerId = CouplePartnerOf(character.CharacterId);
            var ownedSkills = new Dictionary<int, int>();
            foreach (var held in skillStore.GetForCharacter(character.CharacterId)) ownedSkills[held.SkillId] = held.SkillLevel;
            if (ownedSkills.Count == 0)
            {
                foreach (var starterSkillId in SpeciesDefaults.StarterSkillIds(character.Species, character.FType))
                {
                    ownedSkills[starterSkillId] = SkillDefinition.LowestLevel;
                }
            }
            state.Skills = ownedSkills;
            state.Cast = null;
        }

        IReadOnlyList<(Opcode Opcode, byte[] Payload, SwingAudience Audience)> GainExp(long gain, bool fromSwing, bool gmRate)
        {
            if (statManager is null || currentChar is null || !liveSessions.TryGetValue(ctx, out var progressSt))
            {
                return Array.Empty<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>();
            }
            if (gmRate) gain = gameMasters.Rates.ScaleExp(gain);

            var outcome = statManager.AddExp(gain, levelExpChart);
            if (outcome.Kind == ExpGainKind.Withheld)
            {
                return Array.Empty<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>();
            }
            if (outcome.Kind == ExpGainKind.Added)
            {
                FlushStatsToDb("exp");
                return fromSwing
                    ? new[] { (Opcode.S2C_OP_0406_TBD, ProgressFrames.ExpGained(gain), SwingAudience.Attacker) }
                    : new[] { (Opcode.S2C_EXP_VALUE, PlayerDeathFrames.ExpValue(outcome.Exp), SwingAudience.Attacker) };
            }

            var statPoints = StatusValueChart.PointsOf(currentChar);
            var weights = StatGrowth.WeightsInUse(
                new short[] { currentChar.PtsP, currentChar.PtsM, currentChar.PtsS, currentChar.PtsC },
                StatGrowth.TypeIndexOf(currentChar.FType));
            StatGrowth.Raise(statPoints, weights, outcome.LevelsGained);
            var bonusPoints = (ushort)Math.Min(
                ushort.MaxValue, currentChar.AttrPoints + StatGrowth.BonusPointsPerLevel * outcome.LevelsGained);
            var grown = StatsWithEquipment(currentChar, statPoints);
            ApplyCombatStats(progressSt, grown);
            statManager.SetVitals(progressSt.PlayerMaxHp, progressSt.PlayerMaxMp);
            FlushStatsToDb("level-up", statPoints, bonusPoints);
            currentChar = accountStore.GetCharacterById(currentChar.CharacterId) ?? currentChar;
            progressSt.Character = currentChar;
            Console.WriteLine($"{LogTag} {ctx.Remote} [progress] level {outcome.Level} (+{outcome.LevelsGained}), exp {outcome.Exp}, bonus points {bonusPoints}, HP {grown.Hp}, MP {grown.Mp}");
            _ = systemLink.LevelChangedAsync(currentChar.CharacterId, (uint)outcome.Level, CancellationToken.None);
            if (scriptEvents is { } levelScripts) _ = levelScripts(tPoseidon.Core.Game.Scripting.Lang.EventKind.LevelUp, outcome.Level, null);
            var giftedCharacter = currentChar;
            var firstGiftLevel = outcome.Level - outcome.LevelsGained + 1;
            var lastGiftLevel = outcome.Level;
            _ = Task.Run(async () =>
            {
                try
                {
                    await mail.SendLevelUpGiftsAsync(progressSt, giftedCharacter, firstGiftLevel, lastGiftLevel, levelUpGifts).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
                {
                    Console.WriteLine($"{LogTag} [mail] level-up gifts for 0x{progressSt.PlayerEntityId:X4} not delivered: {ex.Message}");
                }
            }, CancellationToken.None);

            var block = new CharacterStatBlock(
                statPoints,
                bonusPoints,
                statManager.TmPoint,
                statManager.TmLevel,
                outcome.Level,
                CharacterStatBlock.LoginMarkWhenUnset);
            return new[]
            {
                (Opcode.S2C_OP_0027_TBD,
                    ProgressFrames.LevelUp(block, statManager.CurrentHp, statManager.CurrentMp, outcome.Exp),
                    SwingAudience.Attacker),
                (Opcode.S2C_OP_0028_TBD,
                    ProgressFrames.LevelSeen(progressSt.PlayerEntityId, outcome.Level),
                    SwingAudience.Everyone),
            }.Concat(soulSeeds.Event(progressSt, SoulSeedRules.LevelUpEvent, 1).Select(seedFrame => (seedFrame.Opcode, seedFrame.Payload, SwingAudience.Attacker))).ToArray();
        }

        async Task GiveQuestProgressAsync(long exp, long tmExp, CancellationToken questCt)
        {
            if (!liveSessions.TryGetValue(ctx, out var questSt)) return;
            if (exp > 0)
            {
                foreach (var (opcode, payload, audience) in GainExp(exp, fromSwing: false, gmRate: false))
                {
                    if (audience == SwingAudience.Everyone)
                    {
                        await SendToMapAsync(questSt.MapId, opcode, payload).ConfigureAwait(false);
                        continue;
                    }
                    byte rkQuestExp;
                    lock (moveRng) rkQuestExp = (byte)moveRng.Next(1, 256);
                    await ctx.SendFrameAsync(opcode, payload, randKey: rkQuestExp, questCt).ConfigureAwait(false);
                }
                Console.WriteLine($"{LogTag} {ctx.Remote} [progress] quest exp {exp}: level {statManager?.Level}, exp {statManager?.Exp}");
            }
            if (tmExp > 0)
            {
                foreach (var (opcode, payload) in GainTmExpFrom(tmExp, ProgressFrames.TmFromMonsterKill, gmRate: false))
                {
                    byte rkQuestTm;
                    lock (moveRng) rkQuestTm = (byte)moveRng.Next(1, 256);
                    await ctx.SendFrameAsync(opcode, payload, randKey: rkQuestTm, questCt).ConfigureAwait(false);
                }
                Console.WriteLine($"{LogTag} {ctx.Remote} [progress] quest TM exp {tmExp}: TM level {statManager?.TmLevel}, TM exp {statManager?.TmExp}");
            }
        }

        IReadOnlyList<(Opcode Opcode, byte[] Payload)> GainTmExp(long gain) =>
            GainTmExpFrom(gain, ProgressFrames.TmFromMonsterKill, gmRate: true);

        IReadOnlyList<(Opcode Opcode, byte[] Payload)> GainTmExpFrom(long gain, byte source, bool gmRate)
        {
            if (statManager is null || currentChar is null) return Array.Empty<(Opcode Opcode, byte[] Payload)>();
            if (gmRate) gain = gameMasters.Rates.ScaleTm(gain);

            var outcome = statManager.AddTmExp(gain, tmLevelChart);
            var frames = new List<(Opcode Opcode, byte[] Payload)>(outcome.LevelUps.Count + 1);
            var characterType = (byte)StatGrowth.TypeIndexOf(currentChar.FType);
            foreach (var step in outcome.LevelUps)
            {
                frames.Add((Opcode.S2C_TM_LEVEL_UP, ProgressFrames.TmLevelUp(characterType, step.TmLevel, step.TmExp, step.TmPoint)));
                Console.WriteLine($"{LogTag} {ctx.Remote} [progress] TM level {step.TmLevel}, TM exp {step.TmExp}, TM points {step.TmPoint}");
                if (liveSessions.TryGetValue(ctx, out var tmSeedSt)) frames.AddRange(soulSeeds.Event(tmSeedSt, SoulSeedRules.TmLevelUpEvent, 1));
            }
            if (outcome.SendsTotal)
            {
                frames.Add((Opcode.S2C_OP_011D_TBD, ProgressFrames.TmTotal(source, outcome.TmExp)));
            }
            FlushStatsToDb("tm-exp");
            return frames;
        }

        void FlushStatsToDb(string trigger, IReadOnlyList<short>? grownStatPoints = null, ushort? grownBonusPoints = null)
        {
            if (statManager is null || currentChar is null) return;
            var statPoints = grownStatPoints ?? StatusValueChart.PointsOf(currentChar);
            var snapshot = new CharacterStatsSnapshot
            {
                CurrentHp = statManager.CurrentHp,
                CurrentMp = statManager.CurrentMp,
                Level = statManager.Level,
                Exp = statManager.Exp,
                Money = statManager.Money,
                TmExp = statManager.TmExp,
                TmLevel = statManager.TmLevel,
                TmPoint = statManager.TmPoint,
                ApPoints = (ushort)statPoints[StatusValueChart.Ap],
                AcPoints = (ushort)statPoints[StatusValueChart.Ac],
                DxPoints = (ushort)statPoints[StatusValueChart.Dx],
                MaPoints = (ushort)statPoints[StatusValueChart.Ma],
                MdPoints = (ushort)statPoints[StatusValueChart.Md],
                DaPoints = (ushort)statPoints[StatusValueChart.Da],
                LkPoints = (ushort)statPoints[StatusValueChart.Lk],
                DpPoints = (ushort)statPoints[StatusValueChart.Dp],
                HvPoints = (ushort)statPoints[StatusValueChart.Hv],
                MaxHpPoints = (ushort)statPoints[StatusValueChart.Hp],
                MaxMpPoints = (ushort)statPoints[StatusValueChart.Mp],
                MaxWtPoints = (ushort)statPoints[StatusValueChart.Wt],
                AttrPoints = grownBonusPoints ?? currentChar.AttrPoints,
                TmLevelPower = currentChar.TmLevelPower,
                TmLevelMagic = currentChar.TmLevelMagic,
                TmLevelSense = currentChar.TmLevelSense,
                TmLevelCharm = currentChar.TmLevelCharm,
            };
            if (accountStore.SaveCharacterStats(currentChar.CharacterId, snapshot))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote}   stats flushed ({trigger}): hp={snapshot.CurrentHp} mp={snapshot.CurrentMp} lvl={snapshot.Level} exp={snapshot.Exp} money={snapshot.Money}");
            }
        }

        Task SendShopFrameAsync(Opcode opcode, byte[] payload, CancellationToken shopCt)
        {
            byte shopKey;
            lock (moveRng) shopKey = (byte)moveRng.Next(1, 256);
            return ctx.SendFrameAsync(opcode, payload, randKey: shopKey, shopCt);
        }
        tPoseidon.Core.Net.World.MyShopHandlers.Register(
            ctx.Dispatcher,
            SendShopFrameAsync,
            () => liveSessions.TryGetValue(ctx, out var shopper)
                ? new tPoseidon.Core.Net.World.MyShopReturn(shopper.MapId, shopper.PlayerX, shopper.PlayerY)
                : null,
            clientTextEncoding,
            line => Console.WriteLine($"{LogTag} {ctx.Remote} {line}"));
        ctx.Dispatcher.Register(Opcode.C2S_CASH_BOX_TAKE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var cashTakeSt) ? cashBox.TakeAsync(cashTakeSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_CASH_BOX_REFRESH, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var cashListSt) ? cashBox.ListAsync(cashListSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_CASH_BOX_RELOAD, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var cashReloadSt) ? cashBox.ListAsync(cashReloadSt) : Task.CompletedTask);
        ctx.Dispatcher.Register((Opcode)tPoseidon.Core.Net.Social.PostFrames.OpenList, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var listSt) ? mail.OpenListAsync(listSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register((Opcode)tPoseidon.Core.Net.Social.PostFrames.OpenPost, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var openSt) ? mail.OpenPostAsync(openSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register((Opcode)tPoseidon.Core.Net.Social.PostFrames.TakePost, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var takeSt) ? mail.TakeAsync(takeSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register((Opcode)tPoseidon.Core.Net.Social.PostFrames.SendPost, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var sendSt) ? mail.SendPostAsync(sendSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.TRADE_INVITE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var inviterSt) ? trade.InviteAsync(inviterSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.TRADE_ACCEPT, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var accepterSt) ? trade.AcceptAsync(accepterSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.TRADE_DECLINE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var declinerSt) ? trade.DeclineAsync(declinerSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.TRADE_CLOSED, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var cancellerSt) ? trade.CancelAsync(cancellerSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.TRADE_UPDATE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var offererSt) ? trade.UpdateAsync(offererSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.TRADE_CHECK, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var checkerSt) ? trade.CheckAsync(checkerSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.TRADE_CONFIRM, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var confirmerSt) ? trade.ConfirmAsync(confirmerSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.TRADE_CONFIRM_CANCEL, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var backerSt) ? trade.ConfirmCancelAsync(backerSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.TRADE_FINAL_CANCEL, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var finalBackerSt) ? trade.FinalCancelAsync(finalBackerSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.TRADE_CHAT, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var talkerSt) ? trade.ChatAsync(talkerSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register((ushort)Kr2014Opcode.C2S_NPC_DIALOG_RESPONSE, async (_, payload, dialogCt) =>
        {
            if (TunnelledRequest.TrySplit(payload.Span, out var tradeOpcode, out var _)
                && tradeOpcode is (ushort)Opcode.TRADE_LOCK or (ushort)Opcode.TRADE_EXECUTE)
            {
                if (!liveSessions.TryGetValue(ctx, out var tradingSt)) return;
                byte tradeAckKey;
                lock (moveRng) tradeAckKey = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_DIALOG_STATE_ACK,
                    new DialogStateAckPacket { Marker = tradeOpcode }.EncodePayload(),
                    randKey: tradeAckKey, dialogCt).ConfigureAwait(false);
                await (tradeOpcode == (ushort)Opcode.TRADE_LOCK ? trade.LockAsync(tradingSt) : trade.ExecuteAsync(tradingSt)).ConfigureAwait(false);
                return;
            }
            if (TunnelledRequest.TrySplit(payload.Span, out var postOpcode, out var postOffset)
                && postOpcode is tPoseidon.Core.Net.Social.PostFrames.TakePost or tPoseidon.Core.Net.Social.PostFrames.SendPost or tPoseidon.Core.Net.Social.PostFrames.DeletePost)
            {
                if (!liveSessions.TryGetValue(ctx, out var poster)) return;
                byte postAckKey;
                lock (moveRng) postAckKey = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_DIALOG_STATE_ACK,
                    new DialogStateAckPacket { Marker = postOpcode }.EncodePayload(),
                    randKey: postAckKey, dialogCt).ConfigureAwait(false);
                var postBody = payload[postOffset..];
                await (postOpcode switch
                {
                    tPoseidon.Core.Net.Social.PostFrames.TakePost => mail.TakeAsync(poster, postBody),
                    tPoseidon.Core.Net.Social.PostFrames.SendPost => mail.SendPostAsync(poster, postBody),
                    _ => mail.DeleteAsync(poster, postBody),
                }).ConfigureAwait(false);
                return;
            }
            if (TunnelledRequest.TrySplit(payload.Span, out var throwOpcode, out var throwOffset)
                && throwOpcode == (ushort)Opcode.C2S_ITEM_THROW)
            {
                if (!liveSessions.TryGetValue(ctx, out var thrower)) return;
                byte throwAckKey;
                lock (moveRng) throwAckKey = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_DIALOG_STATE_ACK,
                    new DialogStateAckPacket { Marker = throwOpcode }.EncodePayload(),
                    randKey: throwAckKey, dialogCt).ConfigureAwait(false);
                await itemThrows.ThrowAsync(thrower, payload[throwOffset..]).ConfigureAwait(false);
                return;
            }
            if (TunnelledRequest.TrySplit(payload.Span, out var innerOpcode, out var bodyOffset)
                && innerOpcode == (ushort)Opcode.C2S_SHOP_SELL)
            {
                var saleBody = payload[bodyOffset..].ToArray();
                if (skillShop.IsOpen) await skillShop.SellAsync(saleBody, dialogCt).ConfigureAwait(false);
                else await npcShop.SellAsync(saleBody, dialogCt).ConfigureAwait(false);
                return;
            }
            if (!NpcScriptRequest.TryRead(payload.Span, out var request))
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x039F not an NPC script call ({payload.Length}B: {Convert.ToHexString(payload.Span)}); drop");
                return;
            }
            Console.WriteLine($"{LogTag} {ctx.Remote} 0x039F NPC script: entity=0x{request.NpcEntityId:X4} function={request.FunctionId} args=[{string.Join(",", request.Args)}]");

            byte ackRandKey;
            lock (moveRng) ackRandKey = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_DIALOG_STATE_ACK,
                new DialogStateAckPacket().EncodePayload(),
                randKey: ackRandKey, dialogCt).ConfigureAwait(false);

            if (await scriptDialog.HandleFunctionAsync(request.NpcEntityId, request.FunctionId, dialogCt).ConfigureAwait(false)) return;
            await npcScript.CallAsync(request.NpcEntityId, request.FunctionId, request.Args, dialogCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_SHOP_OPEN, async (_, payload, shopCt) =>
        {
            if (payload.Length < sizeof(ushort))
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} C2S 0x0046 expected 2 bytes, got {payload.Length}; drop");
                return;
            }
            skillShop.Leave();
            bank.Leave();
            warehouse.Leave();
            compound.Leave();
            exchangeShop.Leave();
            vipShop.Leave();
            refine.Leave();
            growth.Leave();
            await npcShop.OpenAsync(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.Span), shopCt).ConfigureAwait(false);
            if (!liveSessions.TryGetValue(ctx, out var shopSt)) return;
            shopSt.PickupItemEntityId = 0;
            shopSt.PickupWalkEnd = null;
            shopSt.ChaseEndsAt = default;
            await SendToMapAsync(
                shopSt.MapId,
                Opcode.S2C_PLAYER_IN_SHOP,
                ShopFrames.PlayerInShop(shopSt.PlayerEntityId, shopSt.PlayerX, shopSt.PlayerY, shopSt.PlayerMoveType)).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_SHOP_BUY_PRICES, async (_, payload, shopCt) =>
        {
            if (payload.Length < sizeof(ushort)) return;
            await npcShop.SendBuyPricesAsync(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.Span), shopCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_SHOP_SELL_RATES, async (_, payload, shopCt) =>
        {
            if (payload.Length < sizeof(ushort)) return;
            await npcShop.SendSellRatesAsync(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.Span), shopCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_SHOP_BUY, (_, payload, shopCt) =>
            skillShop.IsOpen ? skillShop.BuyAsync(payload, shopCt) : npcShop.BuyAsync(payload, shopCt));

        ctx.Dispatcher.Register(Opcode.C2S_SKILL_SHOP_OPEN, (_, payload, skillShopCt) =>
        {
            npcShop.Leave();
            bank.Leave();
            warehouse.Leave();
            compound.Leave();
            exchangeShop.Leave();
            vipShop.Leave();
            refine.Leave();
            growth.Leave();
            return skillShop.OpenAsync(payload, skillShopCt);
        });

        ctx.Dispatcher.Register(Opcode.C2S_SKILL_SHOP_TALK, (_, payload, skillShopCt) => skillShop.TalkAsync(payload, skillShopCt));
        ctx.Dispatcher.Register(tPoseidon.Core.Net.World.SkillShopFrames.JobChangeRequest, (_, payload, jobCt) => skillShop.ChangeJobAsync(payload, jobCt));

        ctx.Dispatcher.Register(Opcode.C2S_SKILL_SHOP_SCRIPT, (_, payload, skillShopCt) => skillShop.ScriptAsync(payload, skillShopCt));

        ctx.Dispatcher.Register(Opcode.C2S_SKILL_SHOP_PARAM_SCRIPT, (_, payload, skillShopCt) => skillShop.ParamScriptAsync(payload, skillShopCt));

        ctx.Dispatcher.Register(Opcode.C2S_SKILL_CARD_PRICES, (_, payload, skillShopCt) => skillShop.SendBuyPricesAsync(payload, skillShopCt));

        ctx.Dispatcher.Register(Opcode.C2S_SKILL_CARD_RATES, (_, payload, skillShopCt) => skillShop.SendSellRatesAsync(payload, skillShopCt));

        ctx.Dispatcher.Register(Opcode.SKILL_SHOP_CLOSE, (_, _, _) =>
        {
            skillShop.Leave();
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_SKILL_SHOP_LEAVE, (_, _, _) =>
        {
            skillShop.Leave();
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_SKILL_MASTER, (_, payload, masterCt) => skillShop.MasterAsync(payload, masterCt));

        ctx.Dispatcher.Register(Opcode.BANK_CLOSE, (_, _, _) =>
        {
            bank.Leave();
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_BANK_DEPOSIT, (_, payload, bankCt) => bank.DepositAsync(payload, bankCt));

        ctx.Dispatcher.Register(Opcode.C2S_BANK_WITHDRAW, (_, payload, bankCt) => bank.WithdrawAsync(payload, bankCt));

        ctx.Dispatcher.Register(Opcode.C2S_BANK_MONEY_IN, (_, payload, bankCt) => bank.MoneyInAsync(payload, bankCt));

        ctx.Dispatcher.Register(Opcode.C2S_BANK_MONEY_OUT, (_, payload, bankCt) => bank.MoneyOutAsync(payload, bankCt));

        ctx.Dispatcher.Register(Opcode.C2S_DRILL_START, async (_, payload, digCt) =>
        {
            if (!DrillFrames.TryReadStart(payload.Span, out var digX, out var digY, out var digMoveType)) return;
            if (!liveSessions.TryGetValue(ctx, out var digSt) || digSt.IsDead || digSt.Dig is not null) return;
            byte rkDig;
            if (drillController.WornDrill(digSt) is null)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [drill] 0x{digSt.PlayerEntityId:X4} has no drill equipped");
                lock (moveRng) rkDig = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_DRILL_REFUSED, Array.Empty<byte>(), randKey: rkDig, digCt).ConfigureAwait(false);
                return;
            }
            if (disguises.DigRefusal(digSt) is { } formRefusal)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [drill] 0x{digSt.PlayerEntityId:X4} refused: not in normal form (0x{formRefusal:X2})");
                lock (moveRng) rkDig = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_SKILL_NOTICE, SkillFrames.Notice(formRefusal, 0, 0), randKey: rkDig, digCt).ConfigureAwait(false);
                lock (moveRng) rkDig = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_DRILL_REFUSED, Array.Empty<byte>(), randKey: rkDig, digCt).ConfigureAwait(false);
                return;
            }
            if (await MoveToDigAsync(digSt, digX, digY, digMoveType, Opcode.S2C_DRILL_ACCEPTED, refuseInPlace: false, digCt).ConfigureAwait(false) != DigMove.Refused) return;
            lock (moveRng) rkDig = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_DRILL_REFUSED, Array.Empty<byte>(), randKey: rkDig, digCt).ConfigureAwait(false);
            await SendToMapAsync(digSt.MapId, Opcode.S2C_OP_0019_TBD,
                GroundItemFrames.Stopped(digSt.PlayerEntityId, digSt.PlayerX, digSt.PlayerY, digMoveType)).ConfigureAwait(false);
        });

        async Task<DigMove> MoveToDigAsync(InGameSessionState digSt, ushort digX, ushort digY, byte digMoveType, Opcode accepted, bool refuseInPlace, CancellationToken digCt)
        {
            byte rkDig;
            digSt.PickupItemEntityId = 0;
            digSt.PickupWalkEnd = null;
            if (ZoneGeometry.CellOf(digX) == ZoneGeometry.CellOf(digSt.PlayerX) && ZoneGeometry.CellOf(digY) == ZoneGeometry.CellOf(digSt.PlayerY))
            {
                digSt.Dig = new DrillDig { X = digSt.PlayerX, Y = digSt.PlayerY };
                lock (moveRng) rkDig = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(accepted, Array.Empty<byte>(), randKey: rkDig, digCt).ConfigureAwait(false);
                await SendToMapAsync(digSt.MapId, Opcode.S2C_OP_0019_TBD,
                    GroundItemFrames.Stopped(digSt.PlayerEntityId, digSt.PlayerX, digSt.PlayerY, digMoveType)).ConfigureAwait(false);
                await drillController.BeginAsync(digSt).ConfigureAwait(false);
                return DigMove.Began;
            }
            if (playerMoveSpeeds is { } digSpeeds && zoneGeometryByMap.TryGetValue(digSt.MapId, out var digGeometry))
            {
                var digWalkType = tPoseidon.Core.Game.Combat.MeleeApproach.EffectiveMoveType(digMoveType);
                var digWalkSpeed = SpeedOfMoveType(digSpeeds, digWalkType, digSt.GameMasterSpeed);
                var digWalk = digWalkSpeed > 0 ? WanderRoute.Find(digGeometry, digSt.PlayerX, digSt.PlayerY, digX, digY) : null;
                if (digWalk is not null)
                {
                    var digFromX = digSt.PlayerX;
                    var digFromY = digSt.PlayerY;
                    var digTo = ((ushort)digWalk.DestinationX, (ushort)digWalk.DestinationY);
                    if (refuseInPlace && digTo.Item1 == digFromX && digTo.Item2 == digFromY) return DigMove.InPlace;
                    digSt.Dig = new DrillDig
                    {
                        X = digTo.Item1,
                        Y = digTo.Item2,
                        WalkEnd = digTo,
                        BeginsAt = DateTimeOffset.UtcNow.AddMilliseconds(Math.Ceiling(digWalk.Length / digWalkSpeed * 1000.0)),
                    };
                    digSt.PlayerMoveType = digWalkType;
                    var digSpeedQuarter = ProgressFrames.SpeedQuarter(digWalkSpeed);
                    if (digSpeedQuarter != digSt.AnnouncedSpeedQuarter)
                    {
                        digSt.AnnouncedSpeedQuarter = digSpeedQuarter;
                        await SendToMapAsync(digSt.MapId, Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(digSt.PlayerEntityId, digWalkSpeed)).ConfigureAwait(false);
                    }
                    lock (moveRng) rkDig = (byte)moveRng.Next(1, 256);
                    await ctx.SendFrameAsync(accepted, Array.Empty<byte>(), randKey: rkDig, digCt).ConfigureAwait(false);
                    await SendToMapAsync(digSt.MapId, Opcode.ENTITY_MOVE, new EntityMovementPacket
                    {
                        EntityId = digSt.PlayerEntityId,
                        FromX = digFromX,
                        FromY = digFromY,
                        ToX = digTo.Item1,
                        ToY = digTo.Item2,
                        Flag = digMoveType,
                    }.EncodePayload()).ConfigureAwait(false);
                    return DigMove.Walking;
                }
            }
            return DigMove.Refused;
        }

        ctx.Dispatcher.Register(Opcode.AUTO_DRILL_BEGIN, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var autoSt) ? drillController.BeginAutoAsync(autoSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.AUTO_DRILL_END, (_, _, _) =>
        {
            if (!liveSessions.TryGetValue(ctx, out var autoEndSt)) return Task.CompletedTask;
            var (stopX, stopY) = PlayerPositionAt(autoEndSt, DateTimeOffset.UtcNow);
            autoEndSt.PlayerX = (ushort)stopX;
            autoEndSt.PlayerY = (ushort)stopY;
            autoEndSt.Walk = null;
            if (autoEndSt.Dig is { Begun: false } stoppedDig)
            {
                stoppedDig.WalkEnd = null;
                stoppedDig.BeginsAt = default;
            }
            return drillController.EndAutoAsync(autoEndSt);
        });

        ctx.Dispatcher.Register(Opcode.C2S_AUTO_DRILL_COOLTIME, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var coolSt) ? drillController.CoolTimeAsync(coolSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_AUTO_DRILL, async (_, payload, autoCt) =>
        {
            if (!liveSessions.TryGetValue(ctx, out var autoDigSt) || currentChar is not { } autoChar) return;
            var autoPet = itemStore.FindBySlot(autoChar.CharacterId, SlotKind.Equipment, EquipmentSlot.Pet);
            var autoMapOption = mapKindById.TryGetValue(autoDigSt.MapId, out var autoMap) ? autoMap.Option : 0;
            if (tPoseidon.Core.Game.Drilling.AutoDrillRules.ModeError(autoDigSt.AutoDrill, autoMapOption, autoPet?.ItemIndex, drillController.AutoDrillPets) is { } modeError)
            {
                await drillController.AbortAutoAsync(autoDigSt, modeError).ConfigureAwait(false);
                return;
            }
            if (!DrillFrames.TryReadStart(payload.Span, out var autoX, out var autoY, out var autoMoveType)) return;
            if (autoDigSt.IsDead || autoDigSt.Dig is not null) return;
            var coolTime = drillController.AutoDrillPets[autoPet!.ItemIndex];
            if (autoDigSt.AutoDrillCoolTimeAt is { } coolStamp
                && tPoseidon.Core.Game.Drilling.AutoDrillRules.IsBeforeCoolTime(coolTime, (DateTimeOffset.UtcNow - coolStamp).TotalMilliseconds))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [AUTODRILL] 0x{autoDigSt.PlayerEntityId:X4} COOLTIME ({coolTime} ms pet)");
            }
            autoDigSt.AutoDrillCoolTimeAt = null;
            if (drillController.WornDrill(autoDigSt) is null)
            {
                await drillController.AbortAutoAsync(autoDigSt, tPoseidon.Core.Game.Drilling.AutoDrillRules.NoDrill).ConfigureAwait(false);
                return;
            }
            byte rkAuto;
            if (disguises.DigRefusal(autoDigSt) is not null)
            {
                await drillController.AbortAutoAsync(autoDigSt, tPoseidon.Core.Game.Drilling.AutoDrillRules.NotNormalForm).ConfigureAwait(false);
                lock (moveRng) rkAuto = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_SKILL_NOTICE, SkillFrames.Notice(tPoseidon.Core.Game.Drilling.AutoDrillRules.FormRefusalNotice, 0, 0), randKey: rkAuto, autoCt).ConfigureAwait(false);
                return;
            }
            var moved = await MoveToDigAsync(autoDigSt, autoX, autoY, autoMoveType, Opcode.S2C_AUTO_DRILL_MOVING, refuseInPlace: true, autoCt).ConfigureAwait(false);
            if (moved is DigMove.Began or DigMove.Walking) return;
            lock (moveRng) rkAuto = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_AUTO_DRILL_REFUSED,
                AutoDrillFrames.Refused(moved == DigMove.InPlace ? tPoseidon.Core.Game.Drilling.AutoDrillRules.AlreadyThere : tPoseidon.Core.Game.Drilling.AutoDrillRules.NotWalking),
                randKey: rkAuto, autoCt).ConfigureAwait(false);
            await SendToMapAsync(autoDigSt.MapId, Opcode.S2C_OP_0019_TBD,
                GroundItemFrames.Stopped(autoDigSt.PlayerEntityId, autoDigSt.PlayerX, autoDigSt.PlayerY, autoMoveType)).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_DRILL_CANCEL, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var cancelDigSt) ? drillController.CancelAsync(cancelDigSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_DRILL_END, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var endDigSt) ? drillController.EndAsync(endDigSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.COMPOUND_ENTER, (_, _, _) =>
        {
            compound.Enter();
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.COMPOUND_CLOSE, (_, _, compoundCt) =>
        {
            exchangeShop.Leave();
            vipShop.Leave();
            refine.Leave();
            growth.Leave();
            return compound.CloseAsync(compoundCt);
        });

        ctx.Dispatcher.Register(Opcode.C2S_GROWTH, (_, payload, growthCt) => growth.GrowAsync(payload, growthCt));

        ctx.Dispatcher.Register(Opcode.C2S_RESET_MIX, (_, payload, growthCt) => growth.ResetAsync(payload, growthCt));

        ctx.Dispatcher.Register(Opcode.GROWTH_CLOSE, (_, _, _) =>
        {
            growth.Leave();
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.RESET_MIX_CLOSE, (_, _, _) =>
        {
            growth.Leave();
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_REFINE, (_, payload, refineCt) => refine.RefineAsync(payload, refineCt));

        ctx.Dispatcher.Register((Opcode)RefineService.Repair, (_, payload, repairCt) => refine.RepairAsync(payload, repairCt));

        ctx.Dispatcher.Register((Opcode)RefineService.PhantomClose, (_, _, phantomCloseCt) =>
        {
            growth.Leave();
            return refine.ClosePhantomAsync(phantomCloseCt);
        });

        ctx.Dispatcher.Register(Opcode.REFINE_CLOSE, (_, _, _) =>
        {
            refine.Leave();
            growth.Leave();
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_VIP_SHOP_BUY, (_, payload, vipCt) => vipShop.BuyAsync(payload, vipCt));

        ctx.Dispatcher.Register(Opcode.VIP_SHOP_CLOSE, (_, _, vipCt) => vipShop.CloseAsync(vipCt));

        ctx.Dispatcher.Register(Opcode.EXCHANGE_CLOSE, (_, _, exchangeCt) =>
        {
            compound.Leave();
            return exchangeShop.CloseAsync(exchangeCt);
        });

        ctx.Dispatcher.Register(Opcode.C2S_EXCHANGE_OFFER, (_, payload, exchangeCt) => exchangeShop.OfferAsync(payload, exchangeCt));

        ctx.Dispatcher.Register(Opcode.C2S_EXCHANGE, (_, payload, exchangeCt) => exchangeShop.ExchangeAsync(payload, exchangeCt));

        ctx.Dispatcher.Register(Opcode.C2S_COMPOUND_RECIPE, (_, payload, compoundCt) => compound.RecipeAsync(payload, compoundCt));

        ctx.Dispatcher.Register(Opcode.C2S_COMPOUND_PERCENT, (_, payload, compoundCt) => compound.PercentAsync(payload, compoundCt));

        ctx.Dispatcher.Register(Opcode.C2S_COMPOUND, (_, payload, compoundCt) => compound.CompoundAsync(payload, compoundCt));

        ctx.Dispatcher.Register(Opcode.WAREHOUSE_CLOSE, (_, _, _) =>
        {
            warehouse.Leave();
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_WAREHOUSE_DEPOSIT, (_, payload, storageCt) => warehouse.DepositAsync(payload, storageCt));

        ctx.Dispatcher.Register(Opcode.C2S_WAREHOUSE_WITHDRAW, (_, payload, storageCt) => warehouse.WithdrawAsync(payload, storageCt));

        ctx.Dispatcher.Register(Opcode.C2S_WAREHOUSE_MOVE, (_, payload, storageCt) => warehouse.MoveAsync(payload, storageCt));

        ctx.Dispatcher.Register(Opcode.C2S_SHOP_LEAVE, (_, _, _) =>
        {
            npcShop.Leave();
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_QUEST_GIVE_UP, async (_, payload, giveUpCt) =>
        {
            if (payload.Length < 4)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} C2S 0x0092 expected 4 bytes, got {payload.Length}; drop");
                return;
            }
            await npcScript.GiveUpQuestAsync(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.Span), giveUpCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(tPoseidon.Core.Net.World.QuestFrames.JobQuestGiveUp, async (_, payload, giveUpCt) =>
        {
            if (payload.Length < sizeof(int)) return;
            await npcScript.GiveUpJobQuestAsync(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.Span), giveUpCt).ConfigureAwait(false);
        });
        ctx.Dispatcher.Register(Opcode.C2S_MONSTER_QUEST_GIVE_UP, async (_, payload, giveUpCt) =>
        {
            if (payload.Length < 8)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} C2S 0x00DC expected 8 bytes, got {payload.Length}; drop");
                return;
            }
            await npcScript.GiveUpMonsterQuestAsync(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.Span), giveUpCt).ConfigureAwait(false);
        });

        HashSet<ulong> WornAbsoluteUniques() => currentChar is { } wearer
            ? itemStore.GetForCharacterBySlotKind(wearer.CharacterId, SlotKind.Equipment)
                .Where(worn => itemCatalog.GetById(worn.ItemIndex) is { } wornDefinition && tPoseidon.Core.Game.Items.UniqueItemKinds.WithdrawnOnRelease(wornDefinition.Options))
                .Select(worn => worn.ItemUid)
                .ToHashSet()
            : new HashSet<ulong>();

        async Task WithdrawReleasedUniquesAsync(HashSet<ulong> wornBefore, CancellationToken withdrawCt)
        {
            if (wornBefore.Count == 0 || currentChar is not { } holder || !liveSessions.TryGetValue(ctx, out var holderSt)) return;
            var releasedUniques = itemStore.GetForCharacterBySlotKind(holder.CharacterId, SlotKind.Inventory).Where(held => wornBefore.Contains(held.ItemUid)).ToList();
            foreach (var chunk in releasedUniques.Chunk(UniqueItemRules.MaxWithdrawnPerRequest))
            {
                var taken = await masterUniqueItems.WithdrawAsync(new tPoseidon.Core.InterServer.UniqueWithdrawRequest(
                    (ushort)holderSt.MapId, holderSt.PlayerEntityId, holder.CharacterId, tPoseidon.Core.InterServer.UniqueItemMessages.WithdrawCallback,
                    chunk.Select(held => new UniqueHeldItem(held.ItemUid, held.ItemIndex)).ToList()), withdrawCt).ConfigureAwait(false);
                if (taken is null || taken.Count == 0)
                {
                    Console.WriteLine($"{LogTag} {ctx.Remote} [unique] withdraw of {chunk.Length} released items refused by the Master");
                    continue;
                }
                var withdrawOps = taken.Select(gone => chunk.First(held => held.ItemUid == gone.ItemUid)).Select(gone => new BagOperation(
                    QuestResultFrames.Removed, itemCatalog.GetById(gone.ItemIndex)?.ObjectClass ?? gone.ItemClass, (uint)gone.ItemIndex, BagItemIds.OnWire(gone.ItemUid), 0,
                    gone.ItemSlot, (byte)gone.RefineType, gone.RefineLevel, gone.RefineState)).ToList();
                foreach (var gone in taken) holderSt.TimedItems.Forget(gone.ItemUid);
                byte withdrawRk;
                lock (moveRng) withdrawRk = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_UNIQUE_ITEM_WITHDRAWN, UniqueItemFrames.Withdrawn(withdrawOps), randKey: withdrawRk, withdrawCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [unique] {taken.Count} absolute unique items withdrawn after release");
            }
        }

        ctx.Dispatcher.Register(Opcode.CMD_EQUIP_ITEM, async (_, payload, equipCt) =>
        {
            var uniquesBeforeEquip = WornAbsoluteUniques();
            await equipment.EquipAsync(payload, equipCt).ConfigureAwait(false);
            await WithdrawReleasedUniquesAsync(uniquesBeforeEquip, equipCt).ConfigureAwait(false);
            await SyncPetAsync(withEffect: true).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.CMD_RELEASE_ITEM, async (_, payload, releaseCt) =>
        {
            var uniquesBeforeRelease = WornAbsoluteUniques();
            await equipment.ReleaseAsync(payload, releaseCt).ConfigureAwait(false);
            await WithdrawReleasedUniquesAsync(uniquesBeforeRelease, releaseCt).ConfigureAwait(false);
            await SyncPetAsync(withEffect: true).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_EQUIP_COMMON_ITEM, async (_, payload, commonCt) =>
        {
            var uniquesBeforeCommon = WornAbsoluteUniques();
            await equipment.EquipCommonAsync(payload, commonCt).ConfigureAwait(false);
            await WithdrawReleasedUniquesAsync(uniquesBeforeCommon, commonCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_RELEASE_COMMON_ITEM, (_, payload, commonCt) => equipment.ReleaseCommonAsync(payload, commonCt));

        ctx.Dispatcher.Register(Opcode.C2S_GUN_EQUIPPED, async (_, _, gunKeyCt) =>
        {
            byte gunKeyRandKey;
            lock (moveRng) gunKeyRandKey = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_GUN_SHOT_KEY, GunFrames.Key((ushort)Environment.TickCount64), randKey: gunKeyRandKey, gunKeyCt).ConfigureAwait(false);
        });

        async Task SendGuardianFrameAsync(Opcode opcode, byte[] payload, CancellationToken guardianCt)
        {
            byte guardianKey;
            lock (moveRng) guardianKey = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(opcode, payload, randKey: guardianKey, guardianCt).ConfigureAwait(false);
        }

        async Task SendGuardianViewAsync(GuardianLogin guardians, CancellationToken guardianCt)
        {
            await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_LIST,
                tPoseidon.Core.Net.World.GuardianFrames.List(guardians.SlotMaxCount, guardians.Guardians, clientTextEncoding), guardianCt).ConfigureAwait(false);
            await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_RECORD,
                tPoseidon.Core.Net.World.GuardianFrames.Record(guardians.Equipped, clientTextEncoding), guardianCt).ConfigureAwait(false);
        }

        async Task SyncGuardianAsync(CancellationToken guardianCt)
        {
            if (currentChar is null || !liveSessions.TryGetValue(ctx, out var guardianSt)) return;
            guardianSt.Guardians ??= await masterGuardian.LoadAsync(currentChar.CharacterId, guardianCt).ConfigureAwait(false);
            if (guardianSt.Guardians is not { Awakened: true } guardians)
            {
                await soulSystem.SyncAsync(guardianSt, guardianCt).ConfigureAwait(false);
                return;
            }
            await SendGuardianViewAsync(guardians, guardianCt).ConfigureAwait(false);
            await soulSystem.SyncAsync(guardianSt, guardianCt).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote}   guardians: {guardians.Guardians.Count} of {guardians.SlotMaxCount}, equipped {guardians.Equipped?.GuardianUid.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}");
        }

        async Task SyncPetAsync(bool withEffect)
        {
            if (currentChar is null || !liveSessions.TryGetValue(ctx, out var petSt) || !petSt.ClientReady) return;
            var petItem = itemStore.FindBySlot(currentChar.CharacterId, SlotKind.Equipment, EquipmentSlot.Pet);
            var petDefinition = petItem is null ? null : itemCatalog.GetById(petItem.ItemIndex);
            if (petSt.Pet is { } shownPet && (petItem is null || shownPet.ItemUid != petItem.ItemUid || shownPet.MapId != petSt.MapId))
            {
                petSt.Pet = null;
                await RemovePetAsync(shownPet).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote}   pet 0x{shownPet.EntityId:X4} ({shownPet.Name}) removed from map {shownPet.MapId}");
            }
            if (petSt.Pet is not null || petItem is null || petDefinition is null || !tPoseidon.Core.Game.Pets.PetRules.IsPet(petDefinition)) return;

            var petType = tPoseidon.Core.Game.Pets.PetRules.CharacterType(petItem.ItemIndex);
            var petProperties = itemPropertyRules.Full(petDefinition, itemPropertyStore.ForItem(petItem.ItemUid));
            var pet = new tPoseidon.Core.Game.Pets.PetActor
            {
                EntityId = idAllocator.NewId(),
                ItemUid = petItem.ItemUid,
                CharacterType = petType,
                Name = characterInfoCatalog.GetById(petType)?.DisplayName ?? string.Empty,
                MapId = petSt.MapId,
                Recovery = new tPoseidon.Core.Game.Pets.PetRecovery(
                    PropertyValue(petProperties, tPoseidon.Core.Game.Items.ItemPropertyRules.HpRecoveryId),
                    PropertyValue(petProperties, tPoseidon.Core.Game.Items.ItemPropertyRules.MpRecoveryId)),
                SpeedQuarter = petSt.AnnouncedSpeedQuarter,
            };
            var placedAt = DateTimeOffset.UtcNow;
            pet.PlaceAt(petSt.PlayerX, petSt.PlayerY, placedAt);
            lock (moveRng) pet.NextEmoteAt = placedAt + tPoseidon.Core.Game.Pets.PetRules.NextIdleEmote(moveRng);
            await SendToViewersAsync(petSt.MapId, petSt.PlayerX, petSt.PlayerY, withEffect ? Opcode.S2C_PLAYER_SPAWN_POST_WARP : Opcode.NPC_SPAWN,
                PetFrames.Spawn(pet, petSt.PlayerEntityId, petSt.PlayerX, petSt.PlayerY)).ConfigureAwait(false);
            petSt.Pet = pet;
            Console.WriteLine($"{LogTag} {ctx.Remote}   pet 0x{pet.EntityId:X4} ({pet.Name}, type {petType}) from item {petItem.ItemIndex} on map {pet.MapId}");
        }

        async Task ShowMapPetsAsync()
        {
            if (!liveSessions.TryGetValue(ctx, out var viewerSt)) return;
            var shownAt = DateTimeOffset.UtcNow;
            foreach (var other in liveSessions.ToArray())
            {
                if (other.Key == ctx || other.Value.Pet is not { } otherPet || otherPet.MapId != viewerSt.MapId) continue;
                var (otherX, otherY) = otherPet.PositionAt(shownAt);
                if (!InViewOn(viewerSt.MapId, viewerSt.PlayerX, viewerSt.PlayerY, otherX, otherY)) continue;
                byte rkPet;
                lock (moveRng) rkPet = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.NPC_SPAWN, PetFrames.Spawn(otherPet, other.Value.PlayerEntityId, otherX, otherY), randKey: rkPet, default).ConfigureAwait(false);
            }
        }

        async Task<bool> SendZoneChangeAsync(PortalDestination dest, byte changeType, bool portalPreamble, string label)
        {
            if (currentChar is null) return false;
            var pending = new tPoseidon.Core.Game.Warp.PendingWarp
            {
                CharacterId = currentChar.CharacterId,
                SourceMapId = currentChar.MapId,
                DestMapId = dest.DestMapId,
                SpawnX = dest.SpawnX,
                SpawnY = dest.SpawnY,
                ChangeType = changeType,
            };
            if (!warpManager.TryBegin(pending, out var token))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} {label} duplicate request for char 0x{currentChar.CharacterId:X16}: warp 0x{token:X8} already in flight");
                return false;
            }

            if (masterClient is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} {label} no masterClient: handoff aborted");
                warpManager.ConsumeByCharId(currentChar.CharacterId);
                return false;
            }
            var leave = new tPoseidon.Core.InterServer.LeaveZoneRequest
            {
                CharUid = currentChar.CharacterId,
                FromZone = currentChar.MapId,
                ToZone = (ushort)dest.DestMapId,
                StartX = (ushort)dest.SpawnX,
                StartY = (ushort)dest.SpawnY,
                ChangeType = changeType,
                Token = token,
            };
            var leaveFrame = new tPoseidon.Core.InterServer.InterServerFrame
            {
                Opcode = tPoseidon.Core.InterServer.InterServerOpcode.McmdLeaveZoneAckOk,
                Header = new tPoseidon.Core.InterServer.InterServerHeader
                {
                    SourceServerIndex = selfIdentity.ServerIndex,
                    DestServerIndex = 0,
                    CharUid = currentChar.CharacterId,
                    RequestId = masterClient.AllocateRequestId(),
                },
                Body = leave.EncodeBody(),
            };
            ushort altPort;
            try
            {
                var reply = await masterClient
                    .SendAndAwaitReplyAsync(
                        leaveFrame,
                        new HashSet<tPoseidon.Core.InterServer.InterServerOpcode>
                        {
                            tPoseidon.Core.InterServer.InterServerOpcode.McmdLeaveZoneAckOk,
                        },
                        TimeSpan.FromSeconds(2),
                        default)
                    .ConfigureAwait(false);
                var ackBody = tPoseidon.Core.InterServer.LeaveZoneAck.DecodeBody(reply.Body);
                altPort = ackBody.DestServerPort;
                Console.WriteLine($"{LogTag} {ctx.Remote} {label} LeaveZone ack: char 0x{currentChar.CharacterId:X16} -> zone {dest.DestMapId} dest_port={altPort} (via MasterServer)");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} {label} LeaveZone Master ack failed: {ex.Message}: handoff aborted");
                warpManager.ConsumeByCharId(currentChar.CharacterId);
                return false;
            }

            warpManager.ConsumeByCharId(currentChar.CharacterId);
            npcScript.LeaveZone();
            await systemLink.LeftZoneAsync(currentChar.CharacterId, (ushort)dest.DestMapId, sessionCt).ConfigureAwait(false);
            if (liveSessions.TryGetValue(ctx, out var coupleLeavingSt)) await couples.LeftAsync(coupleLeavingSt).ConfigureAwait(false);

            if (portalPreamble)
            {
                byte rk1;
                lock (moveRng) rk1 = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.GAME_UNK_99,
                    new tPoseidon.Core.Net.Packets.ChannelHandoffPreambelPacket().EncodePayload(),
                    randKey: rk1, default).ConfigureAwait(false);
            }

            var headerToken = token;
            var fieldDToken = unchecked(token ^ 0x42100000u);
            var ack = new tPoseidon.Core.Net.Packets.LeaveZoneAckPacket
            {
                Header = headerToken,
                CharUid = currentChar.CharacterId,
                DestinationIp = ServerEndpointConfig.PublicIp,
                DestinationPort = altPort,
                SessionToken = fieldDToken,
                DestMapId = dest.DestMapId,
                SpawnX = dest.SpawnX,
                SpawnY = dest.SpawnY,
                ChangeType = changeType,
            };
            byte rk2;
            lock (moveRng) rk2 = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.GAME_UNK_9A,
                ack.EncodePayload(), randKey: rk2, default).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote} {label} handoff: dest_map={dest.DestMapId} spawn=({dest.SpawnX},{dest.SpawnY}) token=0x{token:X8} (from server-port {ctx.LocalPort} → {altPort})");
            return true;
        }

        ctx.Dispatcher.Register(Opcode.C2S_REQUEST_MOVE, async (_, payload, _) =>
        {
            tPoseidon.Core.Net.Packets.LeaveZoneRequestPacket req;
            try
            {
                req = tPoseidon.Core.Net.Packets.LeaveZoneRequestPacket.DecodePayload(payload.Span);
            }
            catch (tPoseidon.Core.Common.InvalidPacketException ex)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x01D4 bad payload: {ex.Message}");
                return;
            }
            if (currentChar is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x01D4 no currentChar — drop");
                return;
            }

            if (portalLookup is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x01D4 portalLookup is null (data/portals.json failed to load at startup) — drop");
                return;
            }
            var clickX = req.DestZone;
            var clickY = req.SourceEntity;
            var clicked = portalLookup.FindDestinationWithin(currentChar.MapId, clickX, clickY, PortalClick.Margin);
            var standing = portalLookup.FindDestination(currentChar.MapId, playerLastX, playerLastY);
            if (clicked is not null
                && standing?.DestPortalId != clicked.DestPortalId
                && liveSessions.TryGetValue(ctx, out var portalSt)
                && playerMoveSpeeds is { } portalSpeeds
                && zoneGeometryByMap.TryGetValue(portalSt.MapId, out var portalGeometry))
            {
                var portalMoveType = tPoseidon.Core.Game.Combat.MeleeApproach.EffectiveMoveType(req.ChangeType);
                var portalSpeed = SpeedOfMoveType(portalSpeeds, portalMoveType, portalSt.GameMasterSpeed);
                var portalRoute = portalSpeed > 0 ? WanderRoute.Find(portalGeometry, portalSt.PlayerX, portalSt.PlayerY, clickX, clickY) : null;
                if (portalRoute is not null)
                {
                    var portalFromX = portalSt.PlayerX;
                    var portalFromY = portalSt.PlayerY;
                    var portalEnd = ((ushort)portalRoute.DestinationX, (ushort)portalRoute.DestinationY);
                    var portalChangeType = req.ChangeType;
                    portalSt.PickupItemEntityId = 0;
                    portalSt.PickupWalkEnd = null;
                    portalSt.PlayerMoveType = portalMoveType;
                    portalSt.PortalWalk = new PortalWalk(
                        DateTimeOffset.UtcNow.AddMilliseconds(Math.Ceiling(portalRoute.Length / portalSpeed * 1000.0)),
                        portalEnd,
                        async () =>
                        {
                            playerPrevX = playerLastX;
                            playerPrevY = playerLastY;
                            playerLastX = portalEnd.Item1;
                            playerLastY = portalEnd.Item2;
                            if (portalLookup.FindDestinationWithin(currentChar.MapId, portalEnd.Item1, portalEnd.Item2, PortalClick.Margin) is not { } reached
                                || reached.DestPortalId != clicked.DestPortalId)
                            {
                                Console.WriteLine($"{LogTag} {ctx.Remote} 0x01D4 walk ended at ({portalEnd.Item1},{portalEnd.Item2}) outside portal {clicked.DestPortalId}");
                                return;
                            }
                            await SendZoneChangeAsync(reached, portalChangeType, portalPreamble: true, "0x01D4 walk").ConfigureAwait(false);
                        });
                    var portalSpeedQuarter = ProgressFrames.SpeedQuarter(portalSpeed);
                    if (portalSpeedQuarter != portalSt.AnnouncedSpeedQuarter)
                    {
                        portalSt.AnnouncedSpeedQuarter = portalSpeedQuarter;
                        await SendToMapAsync(portalSt.MapId, Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(portalSt.PlayerEntityId, portalSpeed)).ConfigureAwait(false);
                    }
                    await SendToMapAsync(portalSt.MapId, Opcode.ENTITY_MOVE, new EntityMovementPacket
                    {
                        EntityId = portalSt.PlayerEntityId,
                        FromX = portalFromX,
                        FromY = portalFromY,
                        ToX = portalEnd.Item1,
                        ToY = portalEnd.Item2,
                        Flag = req.ChangeType,
                    }.EncodePayload()).ConfigureAwait(false);
                    Console.WriteLine($"{LogTag} {ctx.Remote} 0x01D4 walks to portal {clicked.DestPortalId} at ({clickX},{clickY}) -> map {clicked.DestMapId}");
                    return;
                }
            }
            var dest = standing
                    ?? portalLookup.FindDestination(currentChar.MapId, playerPrevX, playerPrevY);
            if (dest is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x01D4 no portal at ({playerLastX},{playerLastY}) or prev ({playerPrevX},{playerPrevY}) on map {currentChar.MapId} (portalLookup has {portalLookup.TotalPortals} portals across {portalLookup.MapCount} maps); client wire dest_zone=0x{req.DestZone:X4} src_entity=0x{req.SourceEntity:X4} — drop");
                return;
            }

            if (!await SendZoneChangeAsync(dest, req.ChangeType, portalPreamble: true, "0x01D4").ConfigureAwait(false)) return;

            Console.WriteLine($"{LogTag} {ctx.Remote} 0x01D4 wire dest_zone=0x{req.DestZone:X4} entity=0x{req.SourceEntity:X4} → resolved dest_map={dest.DestMapId} spawn=({dest.SpawnX},{dest.SpawnY})");
        });

        ctx.Dispatcher.Register(Opcode.C2S_ENTER_MAP, async (_, payload, _) =>
        {
            tPoseidon.Core.Net.Packets.EnterMapPacket req;
            try
            {
                req = tPoseidon.Core.Net.Packets.EnterMapPacket.DecodePayload(payload.Span);
            }
            catch (tPoseidon.Core.Common.InvalidPacketException ex)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x009C bad payload: {ex.Message}");
                return;
            }
            Console.WriteLine($"{LogTag} {ctx.Remote} 0x009C CMD_ENTER_MAP char_uid=0x{req.CharId:X16} aux=0x{req.AuxToken:X4} (server-port={ctx.LocalPort})");

            ushort destMapId;
            ushort spawnX;
            ushort spawnY;
            ushort sourceMapId;
            uint landingToken = 0;
            if (brokeredJoins.TryConsume(req.CharId, out var brokered))
            {
                landingToken = brokered.Token;
                destMapId = brokered.ToZone;
                sourceMapId = brokered.FromZone;
                (ushort X, ushort Y)? arrival;
                if (brokered.ChangeZoneType == tPoseidon.Core.Game.Quests.NpcMoveRules.ChangeZoneType && brokered.StartX >= 1 && brokered.StartY >= 1)
                {
                    arrival = (brokered.StartX, brokered.StartY);
                }
                else if (brokered.ChangeZoneType == tPoseidon.Core.Game.Quests.NpcMoveRules.ChangeZoneType)
                {
                    zoneGeometryByMap.TryGetValue(destMapId, out var moveGeometry);
                    lock (moveRng) arrival = tPoseidon.Core.Game.Warp.RestartArrival.Pick(moveGeometry, moveRng);
                }
                else if (brokered.ChangeZoneType == tPoseidon.Core.Game.Warp.TeleportArrival.ChangeZoneType)
                {
                    if (brokered.StartX >= 1 && brokered.StartY >= 1)
                    {
                        arrival = (brokered.StartX, brokered.StartY);
                    }
                    else
                    {
                        zoneGeometryByMap.TryGetValue(destMapId, out var teleportGeometry);
                        lock (moveRng) arrival = tPoseidon.Core.Game.Warp.TeleportArrival.Pick(teleportGeometry, moveRng);
                    }
                }
                else if (brokered.ChangeZoneType != tPoseidon.Core.Game.Warp.RestartArrival.ChangeZoneType)
                {
                    arrival = portalLookup?.FindArrival(sourceMapId, destMapId);
                }
                else if (brokered.StartX >= 1 && brokered.StartY >= 1)
                {
                    arrival = (brokered.StartX, brokered.StartY);
                }
                else
                {
                    zoneGeometryByMap.TryGetValue(destMapId, out var restartGeometry);
                    lock (moveRng) arrival = tPoseidon.Core.Game.Warp.RestartArrival.Pick(restartGeometry, moveRng);
                }
                if (arrival is null)
                {
                    Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x009C brokered warp char 0x{req.CharId:X16} {sourceMapId}->{destMapId}: no portal arrival row for this zone pair; drop");
                    return;
                }
                spawnX = arrival.Value.X;
                spawnY = arrival.Value.Y;
                Console.WriteLine($"{LogTag} {ctx.Remote} 0x009C brokered warp char 0x{req.CharId:X16} {sourceMapId}->{destMapId} spawn=({spawnX},{spawnY}) token=0x{brokered.Token:X8} echo=0x{req.DestPortEcho:X8}");
            }
            else
            {
                var pending = warpManager.ConsumeByCharId(req.CharId);
                if (pending is null)
                {
                    Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x009C no pending warp for char_uid=0x{req.CharId:X16} (neither brokered join nor local warp); drop");
                    return;
                }
                destMapId = pending.DestMapId;
                spawnX = pending.SpawnX;
                spawnY = pending.SpawnY;
                sourceMapId = pending.SourceMapId;
            }

            if (!warpManager.TryClaimLanding(req.CharId))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} 0x009C landing for char_uid=0x{req.CharId:X16} already sent; suppressing duplicate landing batch");
                return;
            }

            currentChar = accountStore.GetCharacterById(req.CharId);
            if (currentChar is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x009C char_uid 0x{req.CharId:X16} not found in DB — drop");
                return;
            }
            sessionAccountId = currentChar.OwnerAccountId;
            ctx.Guard.Authenticated();
            moveEntityId = idAllocator.NewId();
            EnsureStatManagerWired();

            FlushStatsToDb("pre-warp");
            accountStore.SetCharacterPosition(currentChar.CharacterId, destMapId, spawnX, spawnY);
            currentChar = accountStore.GetCharacterById(currentChar.CharacterId) ?? currentChar;
            TrackReturnMap(atLogin: false);
            playerPrevX = playerLastX = spawnX;
            playerPrevY = playerLastY = spawnY;
            Console.WriteLine($"{LogTag} {ctx.Remote} warp landed: account={sessionAccountId.Value} map {sourceMapId} → {destMapId} spawn=({spawnX},{spawnY})");

            var entityId = moveEntityId;
            await EnterWorldBatchSynthesizer.SendWarpLandingBatchAsync(
                ctx, currentChar, entityId,
                worldEntities, characterInfoCatalog, monsterManager, monsterCatalog, itemStore,
                shops: shopManager,
                skillShops: skillShopManager,
                activation: npcActivation,
                inView: (x, y) => InViewOn(destMapId, spawnX, spawnY, x, y),
                ct: default).ConfigureAwait(false);

            Console.WriteLine($"{LogTag} {ctx.Remote} sent warp landing batch for new map {currentChar.MapId} (entity=0x{entityId:X4})");
            if (await mail.CountFrameAsync(currentChar.CharacterId).ConfigureAwait(false) is { } warpPostCount)
            {
                await ctx.SendFrameAsync((Opcode)tPoseidon.Core.Net.Social.PostFrames.Count, warpPostCount, (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
            }

            if (masterClient is not null)
            {
                var done = new tPoseidon.Core.InterServer.EnterZoneDone
                {
                    EntryIndex = 0,
                    CharUid = currentChar.CharacterId,
                    ZoneId = destMapId,
                    SessionRandom = landingToken,
                    Tail = 0,
                };
                var doneFrame = new tPoseidon.Core.InterServer.InterServerFrame
                {
                    Opcode = tPoseidon.Core.InterServer.InterServerOpcode.EnterZoneDoneAck,
                    Header = new tPoseidon.Core.InterServer.InterServerHeader
                    {
                        SourceServerIndex = selfIdentity.ServerIndex,
                        DestServerIndex = 0,
                        CharUid = currentChar.CharacterId,
                        RequestId = 0,
                    },
                    Body = done.EncodeBody(),
                };
                try
                {
                    await masterClient.SendAsync(doneFrame, default).ConfigureAwait(false);
                    Console.WriteLine($"{LogTag} {ctx.Remote} sent EnterZoneDoneAck to Master (char 0x{currentChar.CharacterId:X16} zone {destMapId})");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.Error.WriteLine($"{LogTag} {ctx.Remote} EnterZoneDoneAck send failed: {ex.Message}");
                }
            }

            warpManager.ReleaseLandingClaim(currentChar.CharacterId);

            var warpedState = liveSessions.GetOrAdd(ctx, _ => new InGameSessionState
            {
                SendFrameAsync = (op, payload, rk, ct) => ctx.SendFrameAsync(op, payload, rk, ct),
            });
            warpedState.Fortune = await fortune.LoadAsync(currentChar.CharacterId, DateTimeOffset.UtcNow, sessionCt).ConfigureAwait(false);
            warpedState.MapId = currentChar.MapId;
            warpedState.PlayerEntityId = entityId;
            warpedState.PlayerX = currentChar.PositionX;
            warpedState.PlayerY = currentChar.PositionY;
            warpedState.PlayerAttackSpeedStat = ResolveAttackSpeedStat(currentChar);
            ApplyCombatStats(warpedState, StatsWithEquipment(currentChar));
            warpedState.PlayerCharacterType = (int)currentChar.Species;
            warpedState.Disguise.Reset(warpedState.PlayerCharacterType);
            warpedState.Stats = statManager;
            warpedState.Character = currentChar;
            warpedState.ActiveSets = ActiveSetIds(currentChar);
            warpedState.TimedItems.Clear();
            foreach (var warpTimed in tPoseidon.Core.Game.Items.TimedItemRules.Running(itemStore.GetAllForCharacter(currentChar.CharacterId), DateTimeOffset.UtcNow))
            {
                warpedState.TimedItems.Track(warpTimed);
            }
            warpedState.ReleaseExpiredItem = expiredUid => equipment.ReleaseExpiredAsync(expiredUid, sessionCt);
            npcScript.LoadForCharacter(currentChar.CharacterId);
            warpedState.Quests = npcScript;
            warpedState.RunScriptEvent = RunScriptEventAsync;
            ArmProgress(warpedState, currentChar);
            warpedState.AutoAttackActive = false;
            warpedState.CurrentTargetEntityId = 0;
            warpedState.NextSwingAt = default;
            warpedState.ClientReady = true;
            honorTitles.Attach(warpedState, await honorTitles.LoadAsync(currentChar.CharacterId, sessionCt).ConfigureAwait(false));
            if (FortuneController.ClearsProfileTitle(warpedState.Fortune, warpedState.ProfileTitle.TitleType))
            {
                await honorTitles.ApplyProfileTitleAsync(warpedState, currentChar.CharacterId, ProfileTitle.None, sessionCt).ConfigureAwait(false);
            }
            await fortune.SendAppliedAsync(warpedState).ConfigureAwait(false);
            await soulSeeds.LoadAsync(warpedState, currentChar.CharacterId, sessionCt).ConfigureAwait(false);
            await ShowPlayersAsync(warpedState, Opcode.S2C_PLAYER_SPAWN_POST_WARP).ConfigureAwait(false);
            await SendPassiveStatsAsync(sessionCt).ConfigureAwait(false);
            await systemLink.EnteredZoneAsync(ctx, currentChar.CharacterId, destMapId, sessionCt).ConfigureAwait(false);
            await partyQuests.RestoreAsync(currentChar.CharacterId, sessionCt).ConfigureAwait(false);
            await couples.EnteredZoneAsync(warpedState).ConfigureAwait(false);
            await pk.EnteredZoneAsync(warpedState, modeOffAlreadySent: false).ConfigureAwait(false);
            await fiesta.EnteredZoneAsync(warpedState).ConfigureAwait(false);
            await missions.EnteredZoneAsync(warpedState).ConfigureAwait(false);
            await harcon.EnteredZoneAsync(warpedState).ConfigureAwait(false);
            await gameMasters.EnteredZoneAsync(warpedState).ConfigureAwait(false);
            _ = RunScriptEventAsync(tPoseidon.Core.Game.Scripting.Lang.EventKind.MapEnter, destMapId, null);
            await guildGames.EnteredZoneAsync(warpedState).ConfigureAwait(false);
            await ShowMapPetsAsync().ConfigureAwait(false);
            await SyncPetAsync(withEffect: false).ConfigureAwait(false);
            await SyncGuardianAsync(sessionCt).ConfigureAwait(false);
        });

        ushort lastTargetedMobEntity = 0;

        ctx.Dispatcher.Register(Opcode.C2S_TARGET_MARK, async (_, payload, _) =>
        {
            var rawHex = payload.Length == 0 ? "<empty>" : Convert.ToHexString(payload.Span);
            Console.WriteLine($"{LogTag} {ctx.Remote} TRACE 0x0120 C2S_TARGET_MARK {payload.Length}B: {rawHex}");
            if (payload.Length != 2)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x0120 expected 2B got {payload.Length} — drop");
                return;
            }
            var bytes = payload.ToArray();
            var targetEntity = (ushort)(bytes[0] | (bytes[1] << 8));
            var mob = monsterManager.Get(targetEntity);
            if (mob is null || mob.IsDead)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} target-mark 0x{targetEntity:X4} — unknown/dead, drop");
                return;
            }
            lastTargetedMobEntity = targetEntity;
            if (liveSessions.TryGetValue(ctx, out var tgtSt))
            {
                tgtSt.CurrentTargetEntityId = targetEntity;
            }
            Console.WriteLine($"{LogTag} {ctx.Remote} target-mark mob 0x{targetEntity:X4} ({mob.Name}) — send 0x0121 target info");

            byte rkEngaged;
            lock (moveRng) rkEngaged = (byte)moveRng.Next(1, 256);
            try
            {
                var mobDefinition = monsterCatalog.GetById(mob.MonsterParamId);
                var mobInfo = new MobTargetInfoPacket
                {
                    EntityId = targetEntity,
                    FieldAt02Tbd = WireLimits.Int32FieldUnsigned(mob.CurrentHp),
                    FieldAt06Tbd = mobDefinition is null
                        ? 0u
                        : (uint)Math.Max(0, tPoseidon.Core.Game.Monsters.MonsterManager.ResolveMaxMp(mobDefinition)),
                }.EncodePayload();
                await ctx.SendFrameAsync(Opcode.S2C_MOB_TARGET_INFO, mobInfo, randKey: rkEngaged, default).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} mob target info failed: {ex.GetType().Name}: {ex.Message}");
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_LOOK_CHANGE, (_, payload, lookCt) => ChangeLookAsync(payload, lookCt));

        ctx.Dispatcher.Register(Opcode.C2S_RECYCLE_ITEMS, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var recycleItemsSt) ? recycle.RecycleItemsAsync(recycleItemsSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_RECYCLE_MYCAMP_ITEMS, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campRecycleItemsSt) ? recycle.RecycleMyCampItemsAsync(campRecycleItemsSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_RECYCLE_COUPON_EXCHANGE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var exchangeCouponSt) ? recycle.ExchangeCouponsAsync(exchangeCouponSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_CARD_IDENTIFY_START, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var identifySt) ? cardIdentify.StartAsync(identifySt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_CARD_IDENTIFY_DEAL, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var dealSt) ? cardIdentify.DealAsync(dealSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_CARD_IDENTIFY_FLIP, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var flipSt) ? cardIdentify.FlipAsync(flipSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_CARD_IDENTIFY_PAIR, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var pairSt) ? cardIdentify.PairAsync(pairSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_CARD_IDENTIFY_TAKE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var takeSt) ? cardIdentify.TakeAsync(takeSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.CARD_IDENTIFY_END, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var identifyEndSt) ? cardIdentify.EndAsync(identifyEndSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_OPEN, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var shopOpenSt) ? playerShops.OpenAsync(shopOpenSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_REGISTER, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var shopRegisterSt) ? playerShops.RegisterAsync(shopRegisterSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_START, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var shopStartSt) ? playerShops.StartAsync(shopStartSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var shopCloseSt) ? playerShops.CloseAsync(shopCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_VISIT, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var shopVisitSt) ? playerShops.VisitAsync(shopVisitSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_BUY, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var shopBuySt) ? playerShops.BuyAsync(shopBuySt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_SEARCH_OPEN, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var searchOpenSt) ? playerShops.OpenSearchAsync(searchOpenSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_SEARCH, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var searchSt) ? playerShops.SearchAsync(searchSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_SEARCH_NEXT, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var searchNextSt) ? playerShops.NextSearchPageAsync(searchNextSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_SEARCH_BUY, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var searchBuySt) ? playerShops.SearchBuyAsync(searchBuySt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_SEARCH_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var searchCloseSt) ? playerShops.CloseSearchAsync(searchCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PLAYER_SHOP_LEAVE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var shopLeaveSt) ? playerShops.LeaveAsync(shopLeaveSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_GHOST_STONE_EXCHANGE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var ghostStoneSt) ? ghostExchange.ExchangeStoneAsync(ghostStoneSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_GHOST_NEO_EXCHANGE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var ghostNeoSt) ? ghostExchange.ExchangeNeoAsync(ghostNeoSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_GHOST_STONE_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var ghostStoneCloseSt) ? ghostExchange.CloseAsync(ghostStoneCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_GHOST_NEO_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var ghostNeoCloseSt) ? ghostExchange.CloseAsync(ghostNeoCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SOUL_SOURCE_REGISTER, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var soulRegisterSt) ? soulSystem.RegisterAsync(soulRegisterSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SOUL_SOURCE_UNREGISTER, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var soulUnregisterSt) ? soulSystem.UnregisterAsync(soulUnregisterSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SOUL_POTION_TAKE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var soulTakeSt) ? soulSystem.TakePotionsAsync(soulTakeSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SOUL_CHARGE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var soulChargeSt) ? soulSystem.ChargeAsync(soulChargeSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SOUL_CRAFT, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var soulCraftSt) ? soulSystem.CraftAsync(soulCraftSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SOUL_REINFORCE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var soulReinforceSt) ? soulSystem.ReinforceAsync(soulReinforceSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SOUL_CHARGE_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var soulChargeCloseSt) ? soulSystem.CloseAsync(soulChargeCloseSt, SoulController.ChargeWindow) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SOUL_REINFORCE_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var soulReinforceCloseSt) ? soulSystem.CloseAsync(soulReinforceCloseSt, SoulController.ReinforceWindow) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SOUL_CRAFT_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var soulCraftCloseSt) ? soulSystem.CloseAsync(soulCraftCloseSt, SoulController.CraftWindow) : Task.CompletedTask);

        ctx.Dispatcher.Register((Opcode)NpcFunctionFrames.EventTalkEnd, (_, _, eventTalkCt) =>
            npcScript.EventTalkEndedAsync(NpcFunctionFrames.EventTalkEnd, eventTalkCt));

        ctx.Dispatcher.Register((Opcode)NpcFunctionFrames.EventSceneEnd, (_, _, eventSceneCt) =>
            npcScript.EventTalkEndedAsync(NpcFunctionFrames.EventSceneEnd, eventSceneCt));

        ctx.Dispatcher.Register((Opcode)HarconFrames.ShopClose, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var harconShopCloseSt) ? harcon.CloseShopAsync(harconShopCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register((Opcode)ChargeTimerFrames.Recharge, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var rechargeSt) ? chargeTimer.RechargeAsync(rechargeSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register((Opcode)ChargeTimerFrames.Close, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var rechargeCloseSt) ? chargeTimer.CloseAsync(rechargeCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register((Opcode)EquipMergeFrames.Merge, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var mergeRequestSt) ? equipMerge.MergeAsync(mergeRequestSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register((Opcode)EquipMergeFrames.Close, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var mergeCloseSt) ? equipMerge.CloseAsync(mergeCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register((Opcode)ItemComposingFrames.Decompose, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var decomposeRequestSt) ? itemComposing.DecomposeAsync(decomposeRequestSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_COMPOSING, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var composeSt) ? itemComposing.ComposeAsync(composeSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_EQUIP_REFORM, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var reformReqSt) ? equipReform.ReformAsync(reformReqSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_REFORM_RESET, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var reformResetSt) ? equipReform.ResetAsync(reformResetSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_EQUIP_REFORM_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var reformCloseSt) ? equipReform.CloseAsync(reformCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_OPEN, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt0) ? myCamps.OpenAsync(campSt0, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var campSt1) ? myCamps.CloseRequestAsync(campSt1) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_ENTER, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt2) ? myCamps.EnterAsync(campSt2, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_LEAVE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var campSt3) ? myCamps.LeaveAsync(campSt3) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_GUEST_REFRESH, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var campSt4) ? myCamps.GuestLoadedAsync(campSt4) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_NEW_ITEM, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt5) ? myCamps.MoveNewItemAsync(campSt5, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_OWNER_READY, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt6) ? myCamps.OwnerReadyAsync(campSt6, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_MODE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt7) ? myCamps.ModeAsync(campSt7, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_RENAME, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt8) ? myCamps.RenameAsync(campSt8, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_CHAT_INFO, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt9) ? myCamps.ChatInfoAsync(campSt9, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_SHOUT, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt10) ? myCamps.ShoutAsync(campSt10, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_KICK, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt11) ? myCamps.KickAsync(campSt11, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_MODEL, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt12) ? myCamps.ModelAsync(campSt12, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_ITEMS_FIRST, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var campSt13) ? myCamps.TransferNextAsync(campSt13) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_ITEMS_NEXT, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var campSt14) ? myCamps.TransferNextAsync(campSt14) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_SAVE_OBJECTS, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var campSt15) ? myCamps.SaveObjectsAsync(campSt15, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_ITEMS_DONE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var campEndSt0) ? myCamps.TransferEndAsync(campEndSt0, aborted: false) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_ITEMS_FINISHED, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var campEndSt1) ? myCamps.TransferEndAsync(campEndSt1, aborted: false) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_ITEMS_CLOSED, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var campEndSt2) ? myCamps.TransferEndAsync(campEndSt2, aborted: false) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_MYCAMP_ITEMS_ABORT, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var campEndSt3) ? myCamps.TransferEndAsync(campEndSt3, aborted: true) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PUZZLE_START, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var puzzleSt) ? puzzles.StartAsync(puzzleSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_HARCON_ROOM_LIST, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var harconListSt) ? harcon.RoomListAsync(harconListSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_HARCON_CREATE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var harconCreateSt) ? harcon.CreateAsync(harconCreateSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_HARCON_STAGE_RECORD, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var harconRecordSt) ? harcon.StageRecordAsync(harconRecordSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_HARCON_JOIN, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var harconJoinSt) ? harcon.JoinAsync(harconJoinSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_HARCON_LEAVE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var harconLeaveSt) ? harcon.LeaveAsync(harconLeaveSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_HARCON_KICK, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var harconKickSt) ? harcon.KickAsync(harconKickSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_HARCON_START, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var harconStartSt) ? harcon.StartAsync(harconStartSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_HARCON_READY, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var harconReadySt) ? harcon.ReadyAsync(harconReadySt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_HARCON_SHOP_BUY, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var harconBuySt) ? harcon.BuyAsync(harconBuySt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_MISSION_START, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var missionStartSt) ? missions.StartAsync(missionStartSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.SPOT_CREVICE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var creviceSt) ? drillController.CreviceAnswerAsync(creviceSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PK_TOGGLE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var pkSt) ? pk.ToggleAsync(pkSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_GUILD_GAME_READY, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var guildReadySt) ? guildGames.ReadyAsync(guildReadySt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_GUILD_GAME_BAD_QUIT, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var guildQuitSt) ? guildGames.BadQuitAsync(guildQuitSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_GUILD_GAME_DENY, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var guildDenySt) ? guildGames.DenyAsync(guildDenySt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.PK_SCORE_QUERY, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var pkScoreSt) ? pk.ScoreAsync(pkScoreSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_COUPLE_PROPOSE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var proposeSt) ? couples.ProposeAsync(proposeSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_COUPLE_ACCEPT, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var acceptSt) ? couples.AcceptAsync(acceptSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_COUPLE_REFUSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var refuseSt) ? couples.RefuseAsync(refuseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_COUPLE_QUIT, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var quitSt) ? couples.RefuseAsync(quitSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_COUPLE_READY, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var readySt) ? couples.ReadyAsync(readySt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_MARRIAGE_OPTION, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var optionSt) ? couples.OptionAsync(optionSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PET_RETRAIN, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var petRetrainReqSt) ? petRetrain.RetrainAsync(petRetrainReqSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PET_LEVELUP_OPEN, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var petLevelUpOpenSt) ? petLevelUp.RequestOpenAsync(petLevelUpOpenSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PET_LEVELUP, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var petLevelUpReqSt) ? petLevelUp.LevelUpAsync(petLevelUpReqSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PET_LEVELUP_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var petLevelUpCloseSt) ? petLevelUp.CloseAsync(petLevelUpCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_CHARACTER_PET_REINFORCE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var characterPetSt) ? characterPetReinforce.ReinforceAsync(characterPetSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_PET_EVOLVE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var petEvolveSt) ? petEvolveMerge.EvolveAsync(petEvolveSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_PET_MERGE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var petMergeSt) ? petEvolveMerge.MergeAsync(petMergeSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_PET_COLLECTION_START, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var petDigStartSt) ? petCollection.StartAsync(petDigStartSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_PET_COLLECTION_STOP, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var petDigStopSt) ? petCollection.StopAsync(petDigStopSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_PET_COLLECTION_RETURN, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var petDigReturnSt) ? petCollection.ReturnAsync(petDigReturnSt) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_PET_COLLECTION_GET, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var petDigGetSt) ? petCollection.GetAsync(petDigGetSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_PET_COLLECTION_DESTROY, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var petDigDestroySt) ? petCollection.DestroyAsync(petDigDestroySt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_PET_REINFORCE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var petReinforceReqSt) ? petReinforce.ReinforceAsync(petReinforceReqSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_PET_REINFORCE_CLOSE, (_, _, _) =>
        {
            if (liveSessions.TryGetValue(ctx, out var petReinforceCloseSt)) petReinforce.Close(petReinforceCloseSt);
            return Task.CompletedTask;
        });
        ctx.Dispatcher.Register(Opcode.C2S_PET_REINFORCE_RESET, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var petResetReqSt) ? petReinforce.ResetAsync(petResetReqSt, payload) : Task.CompletedTask);
        ctx.Dispatcher.Register(Opcode.C2S_PET_REINFORCE_RESET_CLOSE, (_, _, _) =>
        {
            if (liveSessions.TryGetValue(ctx, out var petResetCloseSt)) petReinforce.CloseReset(petResetCloseSt);
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_PET_RETRAIN_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var petRetrainCloseSt) ? petRetrain.CloseAsync(petRetrainCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_COMPOSING_CLOSE, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var composeCloseSt) ? itemComposing.CloseAsync(composeCloseSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_MELTING, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var meltSt) ? melting.MeltAsync(meltSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_MELTING_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var meltCloseSt) ? melting.CloseAsync(meltCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SLOT_EXTEND, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var slotExtendSt) ? slotExtend.ExtendAsync(slotExtendSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SLOT_EXTEND_CLOSE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var slotExtendCloseSt) ? slotExtend.CloseAsync(slotExtendCloseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_RECYCLE_CLOSE, async (_, payload, _) =>
        {
            if (!liveSessions.TryGetValue(ctx, out var recycleCloseSt)) return;
            await myCamps.DropRecycleListAsync(recycleCloseSt).ConfigureAwait(false);
            await recycle.CloseAsync(recycleCloseSt, payload).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_FORTUNE_CHANGE_CARDS, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var cardsSt) ? fortune.ChangeAsync(cardsSt, payload) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_FORTUNE_ROLL, (_, payload, _) =>
            liveSessions.TryGetValue(ctx, out var rollSt) ? fortune.RollAsync(rollSt, payload, DateTimeOffset.UtcNow) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_FORTUNE_AGAIN, (_, _, againCt) =>
            liveSessions.TryGetValue(ctx, out var againSt) ? fortune.AgainAsync(againSt, againCt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_FORTUNE_LEAVE, (_, _, leaveCt) =>
            liveSessions.TryGetValue(ctx, out var leaveSt) ? fortune.LeaveAsync(leaveSt, DateTimeOffset.UtcNow, ApplyEquipmentChange, leaveCt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_DISGUISE_REMOVE, (_, _, _) =>
            liveSessions.TryGetValue(ctx, out var undisguiseSt) ? disguises.RemoveAsync(undisguiseSt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_PROFILE_TITLE, (_, payload, titleCt) =>
            liveSessions.TryGetValue(ctx, out var titleSt) ? honorTitles.SetProfileTitleAsync(titleSt, payload, titleCt) : Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_GUARDIAN_RECORD, async (_, _, recordCt) =>
        {
            if (!liveSessions.TryGetValue(ctx, out var recordSt) || recordSt.Guardians is not { Awakened: true } guardians) return;
            await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_RECORD,
                tPoseidon.Core.Net.World.GuardianFrames.Record(guardians.Equipped, clientTextEncoding), recordCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_GUARDIAN_ORDER, async (_, payload, orderCt) =>
        {
            if (currentChar is null || !liveSessions.TryGetValue(ctx, out var orderSt)) return;
            if (!tPoseidon.Core.Net.World.GuardianFrames.TryReadOrder(payload.Span, out var orderRequest))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] order request of {payload.Length} bytes not read");
                return;
            }
            var orderGuardian = orderSt.Guardians is { Awakened: true } orderOwned ? orderOwned.Equipped : null;
            if (tPoseidon.Core.Game.Guardian.GuardianOrderRules.Refusal(orderGuardian, orderRequest.SkillId, orderRequest.Remove, orderSt.GuardianBusy) is { } orderRefusal)
            {
                await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_ORDER_REFUSED, tPoseidon.Core.Net.World.GuardianFrames.OrderRefused(orderRefusal), orderCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] order of skill {orderRequest.SkillId} refused with {orderRefusal}");
                return;
            }
            if (orderGuardian is null || orderSt.Guardians is not { } orderLogin) return;
            var orderSlot = tPoseidon.Core.Game.Guardian.GuardianOrderRules.SlotOf(orderRequest.Second);
            var orderRecord = tPoseidon.Core.Game.Guardian.GuardianOrderRules.Record(orderGuardian, orderRequest.SkillId, orderSlot, orderRequest.Remove);
            orderSt.GuardianBusy = true;
            try
            {
                var stored = await masterGuardian.OrderAsync(currentChar.CharacterId,
                    new tPoseidon.Core.InterServer.GuardianOrderChange(orderGuardian.GuardianUid, orderRequest.SkillId, orderRecord.Slot, orderRequest.Remove), orderCt).ConfigureAwait(false);
                if (!stored)
                {
                    Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] Master refused the order change of skill {orderRequest.SkillId} slot {orderRecord.Slot}");
                    return;
                }
                orderSt.Guardians = orderLogin with { Equipped = tPoseidon.Core.Game.Guardian.GuardianOrderRules.Apply(orderGuardian, orderRecord, orderRequest.Remove) };
                await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_ORDER_CHANGED, tPoseidon.Core.Net.World.GuardianFrames.OrderChanged(orderRecord, orderRequest.Remove), orderCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] skill {orderRequest.SkillId} {(orderRequest.Remove ? "removed from" : "added to")} order slot {orderRecord.Slot}");
            }
            finally
            {
                orderSt.GuardianBusy = false;
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_GUARDIAN_EQUIP, async (_, payload, equipCt) =>
        {
            if (currentChar is null || !liveSessions.TryGetValue(ctx, out var equipSt)) return;
            if (!tPoseidon.Core.Net.World.GuardianFrames.TryReadEquip(payload.Span, out var guardianUid)
                || equipSt.GuardianBusy
                || equipSt.Guardians is not { Awakened: true } owned
                || owned.Guardians.All(guardian => guardian.GuardianUid != guardianUid))
            {
                await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_EQUIP_REFUSED, tPoseidon.Core.Net.World.GuardianFrames.Empty(), equipCt).ConfigureAwait(false);
                return;
            }
            equipSt.GuardianBusy = true;
            var previousGuardianUid = owned.Equipped?.GuardianUid ?? 0;
            try
            {
                var equipped = await masterGuardian.EquipAsync(currentChar.CharacterId, guardianUid, equipCt).ConfigureAwait(false);
                if (equipped is null)
                {
                    await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_EQUIP_REFUSED, tPoseidon.Core.Net.World.GuardianFrames.Empty(), equipCt).ConfigureAwait(false);
                    return;
                }
                var updated = owned with
                {
                    Equipped = equipped,
                    Guardians = owned.Guardians.Select(guardian => guardian with { Equipped = guardian.GuardianUid == guardianUid }).ToArray(),
                };
                equipSt.Guardians = updated;
                await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_RELEASED, tPoseidon.Core.Net.World.GuardianFrames.Empty(), equipCt).ConfigureAwait(false);
                await SendGuardianViewAsync(updated, equipCt).ConfigureAwait(false);
                await soulSystem.GuardianChangedAsync(equipSt, previousGuardianUid, guardianUid).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] equipped {guardianUid} ({equipped.Name})");
            }
            finally
            {
                equipSt.GuardianBusy = false;
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_GUARDIAN_CARDS, async (_, payload, cardsCt) =>
        {
            if (currentChar is null || !liveSessions.TryGetValue(ctx, out var cardsSt) || cardsSt.GuardianBusy) return;
            if (!tPoseidon.Core.Net.World.GuardianFrames.TryReadCardSave(payload.Span, out var cardSave)) return;
            if (cardsSt.Guardians is not { Awakened: true, Equipped: { } cardsGuardian } cardsOwned
                || !cardSave.Records.All(tPoseidon.Core.Game.Guardian.GuardianCardRules.Storable))
            {
                await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_CARDS_REFUSED, tPoseidon.Core.Net.World.GuardianFrames.Empty(), cardsCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] card save refused: no equipped guardian or a slot out of range");
                return;
            }
            var cardBag = itemStore.GetForCharacterBySlotKind(currentChar.CharacterId, SlotKind.Inventory);
            var cardRows = new List<EquipUpgradeItem>();
            var cardList = new List<tPoseidon.Core.Game.Guardian.GuardianCardRecord>();
            foreach (var cardRecord in cardSave.Records)
            {
                if (cardRecord.ItemUid != 0)
                {
                    if (cardBag.FirstOrDefault(held => held.ItemUid == BagItemIds.Stored(cardRecord.ItemUid)) is not { } cardItem)
                    {
                        Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] card save: item 0x{cardRecord.ItemUid:X} is not in the bag, record skipped");
                        continue;
                    }
                    cardRows.Add(tPoseidon.Core.Net.World.EquipUpgradeOperations.Take(cardItem, 1));
                }
                cardList.Add(cardRecord);
            }
            var cardEntries = cardList.Select(record => skillCards.GetValueOrDefault(record.ItemId)).ToArray();
            var cardSlots = cardList.Select((record, index) => tPoseidon.Core.Game.Guardian.GuardianCardRules.Reserve(cardEntries[index], record))
                .OfType<GuardianSkill>().ToArray();
            cardsSt.GuardianBusy = true;
            try
            {
                var cardsSaved = await masterGuardian.SaveCardsAsync(currentChar.CharacterId, new tPoseidon.Core.InterServer.GuardianCardSave(
                    cardsGuardian.GuardianUid, (int)cardSave.SkillPoint, cardSave.Records.Count, cardSlots,
                    new EquipStatusUpgrade(currentChar.CharacterId, 0, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, cardRows)), cardsCt).ConfigureAwait(false);
                if (!cardsSaved.Succeeded)
                {
                    await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_CARDS_REFUSED, tPoseidon.Core.Net.World.GuardianFrames.Empty(), cardsCt).ConfigureAwait(false);
                    Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] card save of {cardsGuardian.GuardianUid} refused by the Master (code {cardsSaved.Error})");
                    return;
                }
                var cardsUpdated = tPoseidon.Core.Game.Guardian.GuardianCardRules.Replace(cardsGuardian, (int)cardSave.SkillPoint, cardSlots);
                cardsSt.Guardians = cardsOwned with { Equipped = cardsUpdated };
                var reservedCards = cardList.Select((record, index) => tPoseidon.Core.Game.Guardian.GuardianCardRules.Raw(cardEntries[index], record)).ToArray();
                await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_CARDS_SAVED, tPoseidon.Core.Net.World.GuardianFrames.CardsSaved(
                    cardsSaved.Items.Select(tPoseidon.Core.Net.World.EquipUpgradeOperations.ToBag).ToArray(), cardsGuardian.GuardianUid,
                    reservedCards, cardsUpdated.Skills.Where(skill => skill.Type == GuardianSkillType.Order).ToArray()), cardsCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] {cardsGuardian.GuardianUid} card slots saved: {cardSlots.Length} cards, skill points {cardSave.SkillPoint}");
            }
            finally
            {
                cardsSt.GuardianBusy = false;
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_GUARDIAN_RENAME, async (_, payload, renameCt) =>
        {
            if (currentChar is null || !liveSessions.TryGetValue(ctx, out var renameSt)) return;
            if (renameSt.GuardianBusy) return;
            if (!tPoseidon.Core.Net.World.GuardianFrames.TryReadRename(payload.Span, clientTextEncoding, out var renameItemWireUid, out var newGuardianName)
                || newGuardianName.Length == 0
                || renameSt.Guardians is not { Awakened: true, Equipped: { } renamedGuardian } renameOwned
                || itemStore.GetForCharacterBySlotKind(currentChar.CharacterId, SlotKind.Inventory)
                    .FirstOrDefault(held => held.ItemUid == BagItemIds.Stored(renameItemWireUid)) is not { } renameItem)
            {
                await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_RENAME_REFUSED, tPoseidon.Core.Net.World.GuardianFrames.Empty(), renameCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] rename refused: no item, no name or no equipped guardian");
                return;
            }
            renameSt.GuardianBusy = true;
            try
            {
                var renameRow = tPoseidon.Core.Net.World.EquipUpgradeOperations.Take(renameItem, 1);
                var renamed = await masterGuardian.RenameAsync(currentChar.CharacterId, new tPoseidon.Core.InterServer.GuardianRename(
                    renamedGuardian.GuardianUid, newGuardianName,
                    new EquipStatusUpgrade(currentChar.CharacterId, 0, tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity, [renameRow])), renameCt).ConfigureAwait(false);
                if (!renamed.Succeeded || renamed.Items.Count == 0)
                {
                    await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_RENAME_REFUSED, tPoseidon.Core.Net.World.GuardianFrames.Empty(), renameCt).ConfigureAwait(false);
                    Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] rename of {renamedGuardian.GuardianUid} refused by the Master (code {renamed.Error})");
                    return;
                }
                renameSt.Guardians = renameOwned with
                {
                    Equipped = renamedGuardian with { Name = newGuardianName },
                    Guardians = renameOwned.Guardians.Select(guardian => guardian.GuardianUid == renamedGuardian.GuardianUid ? guardian with { Name = newGuardianName } : guardian).ToArray(),
                };
                await SendGuardianFrameAsync(Opcode.S2C_GUARDIAN_RENAMED, tPoseidon.Core.Net.World.GuardianFrames.Renamed(
                    renamedGuardian.GuardianUid, newGuardianName, tPoseidon.Core.Net.World.EquipUpgradeOperations.ToBag(renamed.Items[0]), clientTextEncoding), renameCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [guardian] {renamedGuardian.GuardianUid} renamed '{renamedGuardian.Name}' -> '{newGuardianName}' with item {renameItem.ItemIndex}");
            }
            finally
            {
                renameSt.GuardianBusy = false;
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_GUN_SHOT, async (_, payload, gunCt) =>
        {
            if (!liveSessions.TryGetValue(ctx, out var gunSt)) return;
            async Task RefuseShotAsync(string reason)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [gun] 0x{gunSt.PlayerEntityId:X4} shot refused: {reason}");
                byte refuseKey;
                lock (moveRng) refuseKey = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_GUN_REFUSED, GunFrames.Refused(), randKey: refuseKey, gunCt).ConfigureAwait(false);
            }
            if (gunSt.IsDead || !GunFrames.TryReadShot(payload.Span, out var shot))
            {
                await RefuseShotAsync("dead or short request").ConfigureAwait(false);
                return;
            }
            var (shotGun, shotBullet) = GunGearOf(gunSt);
            if (shotGun is null || !gunParams.ContainsKey(shotGun.Item.ItemIndex))
            {
                await RefuseShotAsync("no gun worn").ConfigureAwait(false);
                return;
            }
            if (shotBullet is null || shot.BulletsPerShot == 0 || shotBullet.Stack < shot.BulletsPerShot)
            {
                await RefuseShotAsync($"bullets {shotBullet?.Stack ?? 0} for {shot.BulletsPerShot} per shot").ConfigureAwait(false);
                return;
            }
            if (monsterManager.Get(shot.TargetEntityId) is not { IsDead: false } shotTarget || shotTarget.MapId != gunSt.MapId)
            {
                await RefuseShotAsync($"no monster 0x{shot.TargetEntityId:X4}").ConfigureAwait(false);
                return;
            }
            byte keyRandKey;
            lock (moveRng) keyRandKey = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_GUN_SHOT_KEY, GunFrames.Key((ushort)Environment.TickCount64), randKey: keyRandKey, gunCt).ConfigureAwait(false);
            gunSt.PickupItemEntityId = 0;
            gunSt.PickupWalkEnd = null;
            gunSt.NextSwingAt = default;
            gunSt.CurrentTargetEntityId = shotTarget.EntityId;
            gunSt.GunShot = new GunOrder(shotTarget.EntityId, shot.BulletsPerShot, shot.Charged, shot.MoveType);
            if (gunSt.Engagements.Refresh(shotTarget.EntityId, DateTimeOffset.UtcNow) is { } gunCrowdedOut)
            {
                await ReleaseMonsterOwnerAsync(gunSt.MapId, gunCrowdedOut, gunSt.PlayerEntityId).ConfigureAwait(false);
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_OP_001D_TBD, async (_, payload, attackCt) =>
        {
            var hex = payload.Length == 0 ? "<empty>" : Convert.ToHexString(payload.Span);
            Console.WriteLine($"{LogTag} {ctx.Remote} TRACE 0x001D {payload.Length}B: {hex}");
            if (!liveSessions.TryGetValue(ctx, out var atkSt)) return;
            if (atkSt.IsDead || atkSt.InstantChatState is not null) return;
            atkSt.PickupItemEntityId = 0;
            atkSt.PickupWalkEnd = null;
            if (payload.Length >= 2)
            {
                var bytes = payload.ToArray();
                var targetEid = (ushort)(bytes[0] | (bytes[1] << 8));
                if (targetEid != 0)
                {
                    atkSt.CurrentTargetEntityId = targetEid;
                }
                if (bytes.Length > AttackRequestMoveTypeOffset)
                {
                    atkSt.AttackMoveType = bytes[AttackRequestMoveTypeOffset];
                }
            }
            if (atkSt.CurrentTargetEntityId == 0) return;
            var attackTarget = monsterManager.Get(atkSt.CurrentTargetEntityId);
            var playerTarget = attackTarget is null ? PvpTargetOf(atkSt, liveSessions.Values) : null;
            if ((attackTarget is null || attackTarget.IsDead) && playerTarget is null) return;

            var attackOrderType = tPoseidon.Core.Game.Combat.MeleeApproach.EffectiveMoveType(atkSt.AttackMoveType);
            atkSt.PlayerMoveType = attackOrderType;
            if (playerMoveSpeeds is { } attackSpeeds)
            {
                var attackSpeed = SpeedOfMoveType(attackSpeeds, attackOrderType, atkSt.GameMasterSpeed);
                var attackSpeedQuarter = ProgressFrames.SpeedQuarter(attackSpeed);
                if (attackSpeedQuarter != atkSt.AnnouncedSpeedQuarter)
                {
                    atkSt.AnnouncedSpeedQuarter = attackSpeedQuarter;
                    await SendToMapAsync(
                        atkSt.MapId, Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(atkSt.PlayerEntityId, attackSpeed)).ConfigureAwait(false);
                }
            }

            var (stoppedX, stoppedY) = PlayerPositionAt(atkSt, DateTimeOffset.UtcNow);
            atkSt.PlayerX = (ushort)stoppedX;
            atkSt.PlayerY = (ushort)stoppedY;
            atkSt.Walk = null;
            if (atkSt.NextSwingAt == default)
            {
                var attackStartedAt = DateTimeOffset.UtcNow;
                atkSt.NextSwingAt = attackStartedAt;
                atkSt.AttackStopPending = true;
                if (attackTarget is not null && atkSt.Engagements.Refresh(atkSt.CurrentTargetEntityId, attackStartedAt) is { } crowdedOut)
                {
                    await ReleaseMonsterOwnerAsync(atkSt.MapId, crowdedOut, atkSt.PlayerEntityId).ConfigureAwait(false);
                }
            }
            atkSt.SwingsOnTarget = 0;
            atkSt.FollowSettled = false;
            atkSt.SettlesOnArrival = false;
            atkSt.ChaseEndsAt = default;
            atkSt.LastChaseOrderAt = default;
            byte rkAttackAck;
            lock (moveRng) rkAttackAck = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_OP_001E_TBD, Array.Empty<byte>(), randKey: rkAttackAck, attackCt).ConfigureAwait(false);
        });

        void RegisterCombatTracer(Opcode op, string label)
        {
            ctx.Dispatcher.Register(op, (_, payload, _) =>
            {
                var hex = payload.Length == 0 ? "<empty>" : Convert.ToHexString(payload.Span);
                Console.WriteLine($"{LogTag} {ctx.Remote} TRACE 0x{(ushort)op:X4} {label} {payload.Length}B: {hex}");
                return Task.CompletedTask;
            });
        }

        RegisterCombatTracer(Opcode.C2S_OP_213A_TBD,         "C2S_SKILL_USE");
        RegisterCombatTracer(Opcode.C2S_OP_215A_TBD,      "C2S_SKILL_ACTION");
        RegisterCombatTracer(Opcode.C2S_OP_215F_TBD,      "C2S_SKILL_SINGLE");
        RegisterCombatTracer(Opcode.C2S_OP_217D_TBD,         "C2S_ADV_SKILL");
        RegisterCombatTracer(Opcode.C2S_OP_217F_TBD,      "C2S_SKILL_TOGGLE");
        RegisterCombatTracer(Opcode.C2S_OP_218A_TBD, "C2S_SKILL_MULTI_PARAM");
        RegisterCombatTracer(Opcode.C2S_OP_1B69_TBD,    "C2S_CHAR_ACTION_69");
        RegisterCombatTracer(Opcode.S2C_OP_1BA3_TBD,            "0x1BA3 (table: C2S_CHAR_ACTION_A3)");

        async Task RefuseSkillAsync(ushort code, CancellationToken refuseCt)
        {
            byte rkRefuse;
            lock (moveRng) rkRefuse = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_SKILL_REFUSED, SkillFrames.Refused(code), randKey: rkRefuse, refuseCt).ConfigureAwait(false);
        }

        ctx.Dispatcher.Register(Opcode.C2S_SKILL_LEVEL_UP, async (_, payload, levelCt) =>
        {
            if (!SkillFrames.TryReadRecord(payload.Span, out var levelSkillId, out var newLevel, out var spentPoints))
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} C2S 0x01E1 is {payload.Length} bytes, {SkillFrames.SkillRecordSize} expected; drop");
                return;
            }
            if (currentChar is null || statManager is null || !liveSessions.TryGetValue(ctx, out var levelSt)) return;

            var row = skillBook.Find(levelSkillId)?.Row;
            int? heldLevel = levelSt.Skills.TryGetValue(levelSkillId, out var held) ? held : null;
            var refusal = SkillLevelling.Check(row, SkillLevelling.HasCard(row, skillCards), heldLevel, newLevel, statManager.TmPoint);
            var record = SkillFrames.Record(levelSkillId, newLevel, spentPoints);
            byte rkLevel;
            lock (moveRng) rkLevel = (byte)moveRng.Next(1, 256);
            if (refusal is { } refused)
            {
                if (refused == SkillLevelRefusal.AboveMaxLevel)
                {
                    await ctx.SendFrameAsync(Opcode.S2C_SKILL_NOTICE,
                        SkillFrames.Notice(SkillLevelling.RegistrationNotice, levelSkillId, SkillLevelling.AboveMaxLevelNotice),
                        randKey: rkLevel, levelCt).ConfigureAwait(false);
                }
                await ctx.SendFrameAsync(Opcode.S2C_SKILL_LEVEL_UP_REFUSED, record, randKey: rkLevel, levelCt).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] level {newLevel} of skill {levelSkillId} refused: {refused} (TM points {statManager.TmPoint})");
                return;
            }

            var tmPointsLeft = statManager.SpendTmPoints(spentPoints);
            SetSkillLevel(levelSkillId, newLevel);
            await ctx.SendFrameAsync(Opcode.S2C_SKILL_LEVELLED, record, randKey: rkLevel, levelCt).ConfigureAwait(false);
            await SendToMapAsync(levelSt.MapId, Opcode.S2C_SKILL_LEVEL_SEEN,
                SkillFrames.LevelSeen(levelSt.PlayerEntityId, (ushort)StatGrowth.TypeIndexOf(currentChar.FType), newLevel)).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote} [skill] skill {levelSkillId} is now level {newLevel}, {spentPoints} TM points spent, {tmPointsLeft} left");
            if (skillBook.Find(levelSkillId) is { IsPassive: true } levelled && PassiveStats.IsStatPassive(levelled.ActionId))
            {
                await SendPassiveStatsAsync(levelCt).ConfigureAwait(false);
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_OP_00EE_TBD, async (_, payload, skillCt) =>
        {
            if (SkillFrames.ReadUse(payload.Span) is not { } use)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x00EE expected {SkillFrames.UseRequestSize}B got {payload.Length} — drop");
                return;
            }
            if (liveSessions.TryGetValue(ctx, out var stateSkillSt))
            {
                if (PlayerShopController.RefusesSkills(stateSkillSt))
                {
                    await RefuseSkillAsync(tPoseidon.Core.Game.PlayerShop.PlayerShopRules.KeeperRefusesSkills, skillCt).ConfigureAwait(false);
                    return;
                }
                if (stateSkillSt.CardIdentify)
                {
                    await RefuseSkillAsync(tPoseidon.Core.Net.World.CardIdentifyFrames.RefusesSkills, skillCt).ConfigureAwait(false);
                    return;
                }
                if (stateSkillSt.Recycle == tPoseidon.Core.Game.Items.RecycleKind.CouponExchange)
                {
                    await RefuseSkillAsync(tPoseidon.Core.Game.Items.RecycleRules.CouponExchangeRefusesSkills, skillCt).ConfigureAwait(false);
                    return;
                }
            }
            await UseSkillAsync(use, skillCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_SKILL_SCROLL_USE, async (_, payload, scrollCt) =>
        {
            if (payload.Length < 10 || !liveSessions.TryGetValue(ctx, out var scrollSt) || currentChar is null) return;
            var scrollUid = BitConverter.ToUInt64(payload.Span[..8]);
            var scrollSkillId = BitConverter.ToUInt16(payload.Span[8..10]);
            var scrollCondition = PlayerConditionOf(scrollSt);
            if (MonsterSkills.BlocksItems(scrollCondition) || MonsterSkills.BlocksSkill(scrollCondition, scrollSkillId))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] scroll skill {scrollSkillId} refused: 0x{scrollSt.PlayerEntityId:X4} is stunned");
                await RefuseSkillAsync(SkillFailure.General, scrollCt).ConfigureAwait(false);
                return;
            }
            var scrollItem = itemStore.GetForCharacterBySlotKind(currentChar.CharacterId, SlotKind.Inventory)
                .FirstOrDefault(held => BagItemIds.OnWire(held.ItemUid) == scrollUid || held.ItemUid == scrollUid);
            if (scrollItem is null
                || itemCatalog.GetById(scrollItem.ItemIndex)?.SubType != SkillScrolls.ScrollSubType
                || !skillScrolls.TryGetValue(scrollItem.ItemIndex, out var scrollRow)
                || scrollRow.SkillId != scrollSkillId)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] scroll skill {scrollSkillId} refused: item 0x{scrollUid:X} is not its scroll");
                scrollSt.ScrollCast = null;
                await RefuseSkillAsync(SkillFailure.General, scrollCt).ConfigureAwait(false);
                return;
            }
            scrollSt.ScrollCast = (scrollRow.SkillId, scrollRow.Level, scrollItem.ItemUid);
            Console.WriteLine($"{LogTag} {ctx.Remote} [skill] scroll item {scrollItem.ItemIndex} casts skill {scrollRow.SkillId} level {scrollRow.Level}");
            await UseSkillAsync(new SkillRequest((ushort)scrollRow.SkillId, (byte)scrollRow.Level), scrollCt).ConfigureAwait(false);
        });

        async Task UseSkillAsync(SkillRequest use, CancellationToken skillCt)
        {
            if (!liveSessions.TryGetValue(ctx, out var skillSt) || skillSt.IsDead || skillSt.Stats is not { } skillStats) return;
            if (skillSt.Cast is { IsUnderWay: true }) return;
            skillSt.Cast = null;
            var ownedLevel = skillSt.ScrollCast is { } useScroll && useScroll.SkillId == use.SkillId
                ? useScroll.Level
                : skillSt.Skills.TryGetValue(use.SkillId, out var useLearned) ? useLearned : -1;
            if (ownedLevel < 0 || skillBook.Find(use.SkillId) is not { } definition)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} [skill] 0x{skillSt.PlayerEntityId:X4} asked for skill {use.SkillId} which it does not have");
                return;
            }
            if (definition.IsPassive) return;
            if (MonsterSkills.BlocksSkill(PlayerConditionOf(skillSt), definition.Id))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] skill {use.SkillId} refused: 0x{skillSt.PlayerEntityId:X4} is stunned");
                skillSt.ScrollCast = null;
                await RefuseSkillAsync(SkillFailure.General, skillCt).ConfigureAwait(false);
                return;
            }
            if (use.Level < SkillDefinition.LowestLevel || use.Level > SkillDefinition.HighestLevel || use.Level != ownedLevel) return;
            if (definition.Has(SkillDefinition.NotUsableOption)) return;
            var selfEffect = (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(definition.ActionId) is { OnTarget: false } && !EventSkills.IsCoupleHeart(definition.ActionId))
                || EventSkills.IsSelf(definition.ActionId)
                || (SkillAreaShapes.Find(definition.ActionId) is not null && !definition.AsksForTarget)
                || (ObjectHits.IsSelf(definition.ActionId) && !definition.AsksForTarget)
                || GunSkills.HitsAround(definition.ActionId);
            if (GunSkills.HitsAround(definition.ActionId) && GunGearOf(skillSt) is not ({ }, { }))
            {
                await RefuseSkillAsync(SkillFailure.General, skillCt).ConfigureAwait(false);
                return;
            }
            if (!selfEffect && (!SkillActions.IsBuilt(definition.ActionId) || !definition.AsksForTarget))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] skill {use.SkillId} (action {definition.ActionId}) is not built yet — refused");
                await RefuseSkillAsync(SkillFailure.General, skillCt).ConfigureAwait(false);
                return;
            }

            ushort? failure;
            lock (moveRng)
            {
                failure = SkillChecks.Conditions(
                    definition.ConditionsAt(use.Level), skillStats.Level, skillStats.CurrentHp, skillStats.CurrentMp, moveRng);
            }
            if (failure is { } code)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] skill {use.SkillId} refused with code {code} (HP {skillStats.CurrentHp}, MP {skillStats.CurrentMp})");
                await RefuseSkillAsync(code, skillCt).ConfigureAwait(false);
                return;
            }

            if (selfEffect)
            {
                if (skillSt.Cooldowns.IsRunning(definition.Id))
                {
                    await RefuseSkillAsync(SkillFailure.OnCooldown, skillCt).ConfigureAwait(false);
                    return;
                }
                if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(definition.ActionId) is { HpLimitColumn: { } hpLimitColumn } limitedKind
                    && !tPoseidon.Core.Game.Skills.Effects.SkillEffectBook.AllowsCast(
                        limitedKind, definition.ActionAt(use.Level)?.Value(hpLimitColumn) ?? 0f, skillStats.CurrentHp, skillSt.PlayerMaxHp))
                {
                    Console.WriteLine($"{LogTag} {ctx.Remote} [skill] skill {use.SkillId} refused: HP {skillStats.CurrentHp}/{skillSt.PlayerMaxHp} is above its limit");
                    await RefuseSkillAsync(SkillFailure.General, skillCt).ConfigureAwait(false);
                    return;
                }
                var selfCast = new SkillCast(definition, use.Level);
                if (skillSt.CurrentTargetEntityId != 0 && skillSt.NextSwingAt != default)
                {
                    var selfAttacked = skillSt.CurrentTargetEntityId;
                    var selfHoldsGun = itemStore.GetForCharacterBySlotKind(currentChar?.CharacterId ?? 0, SlotKind.Equipment)
                        .Any(worn => itemCatalog.GetById(worn.ItemIndex)?.SubType == SkillActions.GunItemSubType);
                    selfCast.ResumeAttackOn = selfHoldsGun ? (ushort)0 : selfAttacked;
                    monsterManager.Get(selfAttacked)?.RemoveAttacker(skillSt.PlayerEntityId);
                    skillSt.CurrentTargetEntityId = 0;
                    skillSt.NextSwingAt = default;
                    var selfLeave = new tPoseidon.Core.Net.PacketWriter(MeleeSwingFrames.LeaveAttackSize);
                    selfLeave.WriteUInt16(skillSt.PlayerEntityId);
                    await SendToMapAsync(skillSt.MapId, Opcode.S2C_OP_0020_TBD, selfLeave.ToArray()).ConfigureAwait(false);
                }
                skillSt.ChaseEndsAt = default;
                skillSt.PickupItemEntityId = 0;
                skillSt.PickupWalkEnd = null;
                selfCast.StartCasting(skillSt.PlayerEntityId, DateTimeOffset.UtcNow);
                skillSt.Cast = selfCast;
                if (selfCast.DelayLeftMs > 0)
                {
                    await SendToMapAsync(skillSt.MapId, Opcode.S2C_OP_0019_TBD, new Packet0019Tbd
                    {
                        EntityId = skillSt.PlayerEntityId,
                        WorldX = skillSt.PlayerX,
                        WorldY = skillSt.PlayerY,
                        StateFlag = skillSt.PlayerMoveType,
                    }.EncodePayload()).ConfigureAwait(false);
                    await SendToMapAsync(skillSt.MapId, Opcode.S2C_OP_00F2_TBD,
                        SkillFrames.SelfCastStarted(skillSt.PlayerEntityId, use.SkillId, use.Level)).ConfigureAwait(false);
                }
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] 0x{skillSt.PlayerEntityId:X4} casts skill {use.SkillId} level {use.Level} on itself ({selfCast.DelayLeftMs} ms)");
                return;
            }

            skillSt.Cast = new SkillCast(definition, use.Level);
            byte rkAsk;
            lock (moveRng) rkAsk = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(
                Opcode.S2C_OP_00F1_TBD, SkillFrames.TargetAsked(use.SkillId, use.Level), randKey: rkAsk, skillCt).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote} [skill] 0x{skillSt.PlayerEntityId:X4} picks a target for skill {use.SkillId} level {use.Level}");
        }

        ctx.Dispatcher.Register(Opcode.S2C_OP_00E0_TBD, (_, payload, aimCt) => AimSkillAsync(payload, aimCt));
        ctx.Dispatcher.Register(Opcode.C2S_SKILL_SCROLL_TARGET, (_, payload, aimCt) => AimSkillAsync(payload, aimCt));

        async Task AimSkillAsync(ReadOnlyMemory<byte> payload, CancellationToken aimCt)
        {
            if (SkillFrames.ReadTarget(payload) is not { } aim)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x00E0 with {payload.Length}B is not a skill target request — drop");
                return;
            }
            if (!liveSessions.TryGetValue(ctx, out var aimSt) || aimSt.IsDead || aimSt.Stats is not { } aimStats) return;
            if (aimSt.Cast is { IsUnderWay: true }) return;
            aimSt.Cast = null;
            if (aim.Kind != SkillFrames.SkillKind) return;
            var ownedLevel = aimSt.ScrollCast is { } aimScroll && aimScroll.SkillId == aim.SkillId
                ? aimScroll.Level
                : aimSt.Skills.TryGetValue(aim.SkillId, out var aimLearned) ? aimLearned : -1;
            if (ownedLevel < 0
                || skillBook.Find(aim.SkillId) is not { } definition
                || definition.IsPassive)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} [skill] 0x{aimSt.PlayerEntityId:X4} aimed skill {aim.SkillId} which it cannot use");
                return;
            }
            if (aim.Level < SkillDefinition.LowestLevel || aim.Level > ownedLevel) return;
            var aimNeedsGun = (SkillHitShapes.Find(definition.ActionId) is { NeedsGun: true }
                    && !itemStore.GetForCharacterBySlotKind(currentChar?.CharacterId ?? 0, SlotKind.Equipment)
                        .Any(worn => itemCatalog.GetById(worn.ItemIndex)?.SubType == SkillActions.GunItemSubType))
                || (GunSkills.NeedsGun(definition.ActionId) && GunGearOf(aimSt) is not ({ }, { }));
            if (!definition.HasLevel(aim.Level)
                || aimNeedsGun
                || !(SkillActions.IsBuilt(definition.ActionId) || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(definition.ActionId) is { OnTarget: true }))
            {
                await RefuseSkillAsync(SkillFailure.General, aimCt).ConfigureAwait(false);
                return;
            }

            var conditions = definition.ConditionsAt(aim.Level);
            ushort? failure;
            lock (moveRng)
            {
                failure = SkillChecks.Conditions(conditions, aimStats.Level, aimStats.CurrentHp, aimStats.CurrentMp, moveRng);
            }
            if (failure is null && aimSt.Cooldowns.IsRunning(definition.Id)) failure = SkillFailure.OnCooldown;
            if (failure is null) failure = ChainHits.Refusal(definition.ActionId, definition.ActionAt(aim.Level), aimStats.Money, aimSt.Skills);
            if (failure is { } code)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] skill {aim.SkillId} refused with code {code} (HP {aimStats.CurrentHp}, MP {aimStats.CurrentMp})");
                await RefuseSkillAsync(code, aimCt).ConfigureAwait(false);
                return;
            }

            var aimedPoint = GroundSkills.UsesPoint(definition.Row.TargetInput) ? aim.TargetPoint : null;
            var aimedEntityId = aimedPoint is null ? aim.TargetEntityId ?? 0 : (ushort)0;
            var aimedPlayer = SupportSkills.TargetsPlayers(definition.ActionId) && aim.TargetEntityId is not null
                ? liveSessions.Values.FirstOrDefault(healSt => healSt.PlayerEntityId == aimedEntityId && healSt.MapId == aimSt.MapId
                    && healSt.IsDead == SupportSkills.TargetsDead(definition.ActionId))
                : null;
            var aimed = aimedPlayer is null && aimedPoint is null ? monsterManager.Get(aimedEntityId) : null;
            if (aimedPoint is null
                && (aim.TargetType is not (SkillFrames.ObjectTargetType or SkillFrames.ThrowTargetType or SkillFrames.GalderTargetType)
                    || aim.TargetEntityId is null
                    || (aimedPlayer is null && (aimed is null || aimed.MapId != aimSt.MapId))))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] skill {aim.SkillId}: no such target (type {aim.TargetType}, {aim.Data.Length}B) — refused");
                await RefuseSkillAsync(SkillFailure.General, aimCt).ConfigureAwait(false);
                return;
            }

            var cast = new SkillCast(definition, aim.Level);
            if (aimSt.CurrentTargetEntityId != 0 && aimSt.NextSwingAt != default)
            {
                var attacked = aimSt.CurrentTargetEntityId;
                var holdsGun = itemStore.GetForCharacterBySlotKind(currentChar?.CharacterId ?? 0, SlotKind.Equipment)
                    .Any(worn => itemCatalog.GetById(worn.ItemIndex)?.SubType == SkillActions.GunItemSubType);
                cast.ResumeAttackOn = holdsGun ? (ushort)0 : attacked;
                monsterManager.Get(attacked)?.RemoveAttacker(aimSt.PlayerEntityId);
                aimSt.CurrentTargetEntityId = 0;
                aimSt.NextSwingAt = default;
                var leaveAttack = new tPoseidon.Core.Net.PacketWriter(MeleeSwingFrames.LeaveAttackSize);
                leaveAttack.WriteUInt16(aimSt.PlayerEntityId);
                await SendToMapAsync(aimSt.MapId, Opcode.S2C_OP_0020_TBD, leaveAttack.ToArray()).ConfigureAwait(false);
            }
            aimSt.ChaseEndsAt = default;
            aimSt.PickupItemEntityId = 0;
            aimSt.PickupWalkEnd = null;

            var aimedAt = DateTimeOffset.UtcNow;
            cast.TargetPoint = aimedPoint;
            var (aimedX, aimedY) = aimedPoint is { } pointAimed
                ? (pointAimed.X, pointAimed.Y)
                : aimedPlayer is not null
                    ? (aimedPlayer.PlayerX, aimedPlayer.PlayerY)
                    : aimed!.GetInterpolatedPosition(aimedAt);
            var range = SkillChecks.RangeOf(conditions);
            if (SkillChecks.IsWithinRange(range, aimSt.PlayerX, aimSt.PlayerY, aimedX, aimedY))
            {
                cast.StartCasting(aimedEntityId, aimedAt);
                aimSt.Cast = cast;
                await SendToMapAsync(aimSt.MapId, Opcode.S2C_OP_0019_TBD, new Packet0019Tbd
                {
                    EntityId = aimSt.PlayerEntityId,
                    WorldX = aimSt.PlayerX,
                    WorldY = aimSt.PlayerY,
                    StateFlag = aimSt.PlayerMoveType,
                }.EncodePayload()).ConfigureAwait(false);
                await SendToMapAsync(
                    aimSt.MapId,
                    Opcode.S2C_OP_00F2_TBD,
                    aimedPoint is { } startedPoint
                        ? SkillFrames.CastStartedAt(aimSt.PlayerEntityId, aim.SkillId, aim.Level, startedPoint)
                        : SkillFrames.CastStarted(aimSt.PlayerEntityId, aim.SkillId, aim.Level, aimedEntityId)).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [skill] 0x{aimSt.PlayerEntityId:X4} starts skill {aim.SkillId} on 0x{aimedEntityId:X4}: in range {range}, cast {cast.DelayLeftMs} ms");
                return;
            }

            if (playerMoveSpeeds is { } castSpeeds && zoneGeometryByMap.TryGetValue(aimSt.MapId, out var castGeometry))
            {
                var walkSpeed = SpeedOfMoveType(castSpeeds, tPoseidon.Core.Game.Combat.MeleeApproach.RunMoveType, aimSt.GameMasterSpeed);
                var (stopX, stopY) = SkillChecks.ChaseEnd(range, walkSpeed, aimSt.PlayerX, aimSt.PlayerY, aimedX, aimedY);
                var walk = walkSpeed > 0
                    ? WanderRoute.FindToPoint(castGeometry, aimSt.PlayerX, aimSt.PlayerY, stopX, stopY)
                    : null;
                if (walk is not null)
                {
                    var walkFromX = aimSt.PlayerX;
                    var walkFromY = aimSt.PlayerY;
                    cast.Chase(
                        aimedEntityId, (ushort)walk.DestinationX, (ushort)walk.DestinationY,
                        aimedAt.AddMilliseconds(Math.Ceiling(walk.Length / walkSpeed * 1000.0)), aimedAt);
                    aimSt.Cast = cast;
                    aimSt.PlayerMoveType = tPoseidon.Core.Game.Combat.MeleeApproach.RunMoveType;
                    var walkSpeedQuarter = ProgressFrames.SpeedQuarter(walkSpeed);
                    if (walkSpeedQuarter != aimSt.AnnouncedSpeedQuarter)
                    {
                        aimSt.AnnouncedSpeedQuarter = walkSpeedQuarter;
                        await SendToMapAsync(
                            aimSt.MapId, Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(aimSt.PlayerEntityId, walkSpeed)).ConfigureAwait(false);
                    }
                    await SendToMapAsync(aimSt.MapId, Opcode.ENTITY_MOVE, new EntityMovementPacket
                    {
                        EntityId = aimSt.PlayerEntityId,
                        FromX = walkFromX,
                        FromY = walkFromY,
                        ToX = (ushort)walk.DestinationX,
                        ToY = (ushort)walk.DestinationY,
                        Flag = tPoseidon.Core.Game.Combat.MeleeApproach.RunMoveType,
                    }.EncodePayload()).ConfigureAwait(false);
                    Console.WriteLine($"{LogTag} {ctx.Remote} [skill] 0x{aimSt.PlayerEntityId:X4} walks into range {range} of 0x{aimedEntityId:X4} for skill {aim.SkillId}: ({walkFromX},{walkFromY}) -> ({walk.DestinationX},{walk.DestinationY})");
                    return;
                }
            }

            byte rkCancel;
            lock (moveRng) rkCancel = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.SKILL_CAST_CANCEL, Array.Empty<byte>(), randKey: rkCancel, aimCt).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote} [skill] 0x{aimSt.PlayerEntityId:X4} has no way into range of 0x{aimedEntityId:X4} for skill {aim.SkillId} — cancelled");
        }

        ctx.Dispatcher.Register(Opcode.SKILL_CAST_CANCEL, async (_, _, cancelCt) =>
        {
            if (!liveSessions.TryGetValue(ctx, out var cancelSt)) return;
            if (cancelSt.Cast is { IsUnderWay: true }) return;
            cancelSt.Cast = null;
            byte rkCancelled;
            lock (moveRng) rkCancelled = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.SKILL_CAST_CANCEL, Array.Empty<byte>(), randKey: rkCancelled, cancelCt).ConfigureAwait(false);
        });
        ctx.Dispatcher.Register(Opcode.C2S_SKILL_SELECTION_DROPPED, async (_, payload, droppedCt) =>
        {
            if (SkillFrames.ReadSelectionDroppedKind(payload.Span) is not { } droppedKind)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} C2S 0x0107 is {payload.Length} bytes, {SkillFrames.SelectionDroppedSize} expected; drop");
                return;
            }
            if (droppedKind != SkillFrames.SkillKind || !liveSessions.TryGetValue(ctx, out var droppedSt)) return;
            droppedSt.Cast = null;
            byte rkDropped;
            lock (moveRng) rkDropped = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.SKILL_CAST_CANCEL, Array.Empty<byte>(), randKey: rkDropped, droppedCt).ConfigureAwait(false);
        });
        ctx.Dispatcher.Register(Opcode.C2S_ITEM_PICKUP, async (_, payload, _) =>
        {
            if (liveSessions.TryGetValue(ctx, out var pickupHeldSt) && pickupHeldSt.InstantChatState is not null) return;
            if (payload.Length != PickupRules.RequestSize)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x0021 expected {PickupRules.RequestSize}B got {payload.Length} — drop");
                return;
            }
            if (!liveSessions.TryGetValue(ctx, out var pickSt) || pickSt.IsDead) return;
            var pickupRequest = payload.ToArray();
            var itemEntityId = (ushort)(pickupRequest[0] | (pickupRequest[1] << 8));
            var pickupMoveType = pickupRequest[PickupRules.RequestMoveTypeOffset];

            pickSt.PickupItemEntityId = 0;
            pickSt.PickupWalkEnd = null;
            pickSt.ChaseEndsAt = default;
            if (pickSt.CurrentTargetEntityId != 0)
            {
                monsterManager.Get(pickSt.CurrentTargetEntityId)?.RemoveAttacker(pickSt.PlayerEntityId);
                pickSt.CurrentTargetEntityId = 0;
                pickSt.NextSwingAt = default;
                var leaveAttack = new tPoseidon.Core.Net.PacketWriter(MeleeSwingFrames.LeaveAttackSize);
                leaveAttack.WriteUInt16(pickSt.PlayerEntityId);
                await SendToMapAsync(pickSt.MapId, Opcode.S2C_OP_0020_TBD, leaveAttack.ToArray()).ConfigureAwait(false);
            }

            var wanted = groundItems.Find(itemEntityId);
            if (wanted is null || wanted.MapId != pickSt.MapId)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [items] pickup of 0x{itemEntityId:X4} ignored: no such item on map {pickSt.MapId}");
                return;
            }
            if (wanted.IsHidden && !pickSt.SeesHiddenItems) return;
            if (itemCatalog.GetById(wanted.ItemId) is { OnlyDropperTakes: true } && wanted.DropperEntityId != pickSt.PlayerEntityId) return;

            var requestedAt = DateTimeOffset.UtcNow;
            var withinReach = PickupRules.IsWithinReach(pickSt.PlayerX, pickSt.PlayerY, wanted.X, wanted.Y);
            if (!withinReach && playerMoveSpeeds is { } pickupSpeeds && zoneGeometryByMap.TryGetValue(pickSt.MapId, out var pickupGeometry))
            {
                (ushort X, ushort Y) walkEnd;
                lock (moveRng) walkEnd = PickupRules.WalkEnd(pickupGeometry, wanted, moveRng);
                var walkType = tPoseidon.Core.Game.Combat.MeleeApproach.EffectiveMoveType(pickupMoveType);
                var walkSpeed = SpeedOfMoveType(pickupSpeeds, walkType, pickSt.GameMasterSpeed);
                var walk = walkSpeed > 0
                    ? WanderRoute.Find(pickupGeometry, pickSt.PlayerX, pickSt.PlayerY, walkEnd.X, walkEnd.Y)
                    : null;
                if (walk is not null)
                {
                    var walkFromX = pickSt.PlayerX;
                    var walkFromY = pickSt.PlayerY;
                    pickSt.PickupItemEntityId = itemEntityId;
                    pickSt.PickupWalkEnd = ((ushort)walk.DestinationX, (ushort)walk.DestinationY);
                    pickSt.PickupAt = requestedAt.AddMilliseconds(Math.Ceiling(walk.Length / walkSpeed * 1000.0));
                    pickSt.PlayerMoveType = walkType;
                    var walkSpeedQuarter = ProgressFrames.SpeedQuarter(walkSpeed);
                    if (walkSpeedQuarter != pickSt.AnnouncedSpeedQuarter)
                    {
                        pickSt.AnnouncedSpeedQuarter = walkSpeedQuarter;
                        await SendToMapAsync(
                            pickSt.MapId, Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(pickSt.PlayerEntityId, walkSpeed)).ConfigureAwait(false);
                    }
                    await SendToMapAsync(pickSt.MapId, Opcode.ENTITY_MOVE, new EntityMovementPacket
                    {
                        EntityId = pickSt.PlayerEntityId,
                        FromX = walkFromX,
                        FromY = walkFromY,
                        ToX = (ushort)walk.DestinationX,
                        ToY = (ushort)walk.DestinationY,
                        Flag = pickupMoveType,
                    }.EncodePayload()).ConfigureAwait(false);
                    Console.WriteLine($"{LogTag} {ctx.Remote} [items] 0x{pickSt.PlayerEntityId:X4} walks to item 0x{itemEntityId:X4}: ({walkFromX},{walkFromY}) -> ({walk.DestinationX},{walk.DestinationY}), move type {pickupMoveType}");
                    return;
                }
            }

            await SendToMapAsync(
                pickSt.MapId,
                Opcode.S2C_OP_0019_TBD,
                GroundItemFrames.Stopped(pickSt.PlayerEntityId, pickSt.PlayerX, pickSt.PlayerY, pickupMoveType)).ConfigureAwait(false);
            if (!withinReach)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} [items] 0x{pickSt.PlayerEntityId:X4} has no way to item 0x{itemEntityId:X4}");
                return;
            }
            pickSt.PickupItemEntityId = itemEntityId;
            pickSt.PickupAt = requestedAt;
        });

        ctx.Dispatcher.Register(Opcode.C2S_TARGET_SELECT, async (_, payload, targetCt) =>
        {
            var rawHex = payload.Length == 0 ? "<empty>" : Convert.ToHexString(payload.Span);
            Console.WriteLine($"{LogTag} {ctx.Remote} TRACE 0x006F C2S_TARGET_SELECT {payload.Length}B: {rawHex}");
            if (payload.Length != 2)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} C2S 0x006F expected 2B payload, got {payload.Length} — drop");
                return;
            }
            var bytes = payload.ToArray();
            targetedNpcEntityId = (ushort)(bytes[0] | (bytes[1] << 8));
            Console.WriteLine($"{LogTag} {ctx.Remote} target-select entity 0x{targetedNpcEntityId:X4}");

            var npcEntityForOpen = worldEntities.Get(targetedNpcEntityId);
            if (npcEntityForOpen is null)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote}   (entity 0x{targetedNpcEntityId:X4} not in world registry; ignored)");
                return;
            }
            if (currentChar is null)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote}   (no current char; ignored)");
                return;
            }
            var npcType = npcEntityForOpen.ChrType;

            scriptDialog.Close();
            if (await scriptDialog.OpenAsync(npcEntityForOpen, targetedNpcEntityId, targetCt).ConfigureAwait(false)) return;
            if (!await npcScript.OpenAsync(npcEntityForOpen, targetedNpcEntityId, targetCt).ConfigureAwait(false))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote}   (no dialog source for type {npcType} on map {npcEntityForOpen.MapId}; ignored)");
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_QUEST_CANCEL, async (_, payload, cancelCt) =>
        {
            if (payload.Length < 4)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} C2S 0x0340 expected 4 bytes, got {payload.Length}; drop");
                return;
            }
            await npcScript.GiveUpScenarioQuestAsync(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.Span), cancelCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_NPC_DIALOG_OPEN, async (_, payload, advanceCt) =>
        {
            if (payload.Length < 3)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} C2S 0x0072 expected ≥3B payload, got {payload.Length} — drop");
                return;
            }
            var bytes = payload.ToArray();
            var entityId = (ushort)(bytes[0] | (bytes[1] << 8));
            var page = bytes[2];
            Console.WriteLine($"{LogTag} {ctx.Remote} 0x0072 advance-page entity 0x{entityId:X4} page {page}");

            if (await scriptDialog.HandlePageLinkAsync(entityId, page, advanceCt).ConfigureAwait(false)) return;
            await npcScript.ShowMessageAsync(entityId, page, advanceCt).ConfigureAwait(false);
        });

        void TrackReturnMap(bool atLogin)
        {
            if (currentChar is null) return;
            var home = tPoseidon.Core.Game.Warp.RestartArrival.NormalizeReturnMap(currentChar.Home);
            if (mapKindById.TryGetValue(currentChar.MapId, out var mapKind)
                && (atLogin ? mapKind.IsReturnMapAtLogin : mapKind.IsReturnMapOnArrival)
                && home != currentChar.MapId)
            {
                home = currentChar.MapId;
                accountStore.SetCharacterHome(currentChar.CharacterId, home);
                Console.WriteLine($"{LogTag} {ctx.Remote} return map is now {home}");
            }
            returnMapId = home;
        }

        ctx.Dispatcher.Register(Opcode.C2S_RESTART_IN_TOWN, async (_, _, restartCt) =>
        {
            if (currentChar is null || !liveSessions.TryGetValue(ctx, out var restartSt) || restartSt.Stats is not { } restartStats) return;
            npcScript.DiscardHuntSavedAtDeath();
            byte rkRestart;
            if (beginnerRebirth is { } restartBeginner && restartStats.Level <= restartBeginner.Level)
            {
                var expAfter = restartStats.GiveBackDeathExp(restartBeginner.RestoreExpRatio);
                lock (moveRng) rkRestart = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_EXP_VALUE, PlayerDeathFrames.ExpValue(expAfter), randKey: rkRestart, restartCt).ConfigureAwait(false);
                lock (moveRng) rkRestart = (byte)moveRng.Next(1, 256);
                await ctx.SendFrameAsync(Opcode.S2C_OP_011D_TBD,
                    ProgressFrames.TmTotal((byte)StatGrowth.TypeIndexOf(currentChar.FType), restartStats.TmExp), randKey: rkRestart, restartCt).ConfigureAwait(false);
            }

            var restartHp = restartStats.Restart(restartSt.PlayerMaxHp);
            zoneGeometryByMap.TryGetValue(returnMapId, out var returnGeometry);
            ushort arriveX;
            ushort arriveY;
            lock (moveRng) (arriveX, arriveY) = tPoseidon.Core.Game.Warp.RestartArrival.Pick(returnGeometry, moveRng);
            FlushStatsToDb("restart");
            Console.WriteLine($"{LogTag} {ctx.Remote} [combat] player 0x{restartSt.PlayerEntityId:X4} restarts in town: map {returnMapId} at ({arriveX},{arriveY}), HP {restartHp}");

            lock (moveRng) rkRestart = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_RESTART_NOTICE,
                PlayerRevivalFrames.RestartNotice(returnMapId, restartHp), randKey: rkRestart, restartCt).ConfigureAwait(false);
            var leftPayload = PlayerRevivalFrames.LeftMap(restartSt.PlayerEntityId);
            foreach (var watcher in liveSessions.ToArray())
            {
                if (watcher.Value.MapId != restartSt.MapId || !watcher.Value.ClientReady) continue;
                lock (moveRng) rkRestart = (byte)moveRng.Next(1, 256);
                await watcher.Key.SendFrameAsync(Opcode.S2C_PLAYER_LEFT_MAP, leftPayload, randKey: rkRestart, restartCt).ConfigureAwait(false);
            }

            restartSt.IsDead = false;
            restartSt.ResurrectionCompletesAt = default;
            await systemLink.RevivedAsync(currentChar.CharacterId, restartCt).ConfigureAwait(false);
            var restartDestination = new PortalDestination
            {
                DestMapId = returnMapId,
                DestPortalId = 0,
                SpawnX = arriveX,
                SpawnY = arriveY,
            };
            if (!await SendZoneChangeAsync(restartDestination, tPoseidon.Core.Game.Warp.RestartArrival.ChangeZoneType, portalPreamble: false, "0x0016").ConfigureAwait(false))
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} 0x0016 zone change to map {returnMapId} was not issued");
            }
        });

        async Task BroadcastChatAsync(ushort replyOpcode, ReadOnlyMemory<byte> payload)
        {
            if (currentChar is null || !liveSessions.TryGetValue(ctx, out var speaker)) return;
            var text = tPoseidon.Core.Net.Social.ChatFrames.ReadText(payload.Span, clientTextEncoding);
            if (await TryHandleScriptChatAsync(speaker, text).ConfigureAwait(false)) return;
            if (gameMasters.IsCommand(text))
            {
                await systemLink.SendAsync(tPoseidon.Core.Net.Social.GameMasterFrames.LinkChatCommand,
                    tPoseidon.Core.Net.Social.GameMasterFrames.ChatLine(currentChar.CharacterId, text, clientTextEncoding), default).ConfigureAwait(false);
                Console.WriteLine($"{LogTag} {ctx.Remote} [chat] '{currentChar.Name}' GM command passed to the SystemServer");
                return;
            }
            await SendToMapAsync(speaker.MapId, (Opcode)replyOpcode,
                tPoseidon.Core.Net.Social.ChatFrames.Line(speaker.PlayerEntityId, currentChar.Name, text, clientTextEncoding)).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote} [chat] 0x{replyOpcode:X4} '{currentChar.Name}': {text}");
        }

        ctx.Dispatcher.Register(tPoseidon.Core.Net.Social.ChatFrames.Say, (_, payload, _) =>
            BroadcastChatAsync(tPoseidon.Core.Net.Social.ChatFrames.Said, payload));
        ctx.Dispatcher.Register(tPoseidon.Core.Net.Social.ChatFrames.ChatBar, (_, payload, _) =>
            BroadcastChatAsync(tPoseidon.Core.Net.Social.ChatFrames.ChatBarSaid, payload));
        ctx.Dispatcher.Register(tPoseidon.Core.Net.Social.ChatFrames.HeadMessage, async (_, payload, _) =>
        {
            if (!liveSessions.TryGetValue(ctx, out var speaker)) return;
            var text = tPoseidon.Core.Net.Social.ChatFrames.ReadText(payload.Span, clientTextEncoding);
            await SendToMapAsync(speaker.MapId, (Opcode)tPoseidon.Core.Net.Social.ChatFrames.HeadMessageShown,
                tPoseidon.Core.Net.Social.ChatFrames.Head(speaker.PlayerEntityId, text, clientTextEncoding)).ConfigureAwait(false);
        });
        ctx.Dispatcher.Register(tPoseidon.Core.Net.Social.ChatFrames.FindPlayer, async (_, payload, findCt) =>
        {
            var name = tPoseidon.Core.Net.Social.ChatFrames.ReadText(payload.Span, clientTextEncoding);
            var online = liveSessions.Values.Any(s => s.Character is { } other && string.Equals(other.Name, name, StringComparison.Ordinal));
            byte findRk;
            lock (moveRng) findRk = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(
                online ? tPoseidon.Core.Net.Social.ChatFrames.PlayerFound : tPoseidon.Core.Net.Social.ChatFrames.PlayerNotFound,
                tPoseidon.Core.Net.Social.ChatFrames.Name(name, clientTextEncoding), findRk, findCt).ConfigureAwait(false);
        });
        ctx.Dispatcher.Register(tPoseidon.Core.Net.Social.ChatFrames.CountPlayers, async (_, _, countCt) =>
        {
            var total = (ushort)Math.Min(ushort.MaxValue, liveSessions.Values.Count(s => s.Character is not null));
            byte countRk;
            lock (moveRng) countRk = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(tPoseidon.Core.Net.Social.ChatFrames.PlayerCount, tPoseidon.Core.Net.Social.ChatFrames.Count(total), countRk, countCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(tPoseidon.Core.Net.World.QuestFrames.HistoryRequest, async (_, payload, historyCt) =>
        {
            if (payload.Length < 1) return;
            await npcScript.SendQuestHistoryAsync(payload.Span[0], historyCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(tPoseidon.Core.Net.World.QuestFrames.ReturnWarpRequest, async (_, payload, returnCt) =>
        {
            if (payload.Length < sizeof(uint)) return;
            await npcScript.ReturnWarpAsync(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(payload.Span), returnCt).ConfigureAwait(false);
        });

        ctx.Dispatcher.Register(Opcode.C2S_RESTART_IN_PLACE, async (_, _, reviveCt) =>
        {
            if (!liveSessions.TryGetValue(ctx, out var reviveSt) || reviveSt.Stats is not { } reviveStats || currentChar is null) return;
            byte rkRevive;
            lock (moveRng) rkRevive = (byte)moveRng.Next(1, 256);
            var canRise = reviveSt.IsDead && reviveSt.ResurrectionCompletesAt == default;

            float hpRatio;
            float mpRatio;
            float expRatio;
            ushort reviverId;
            Item? usedItem = null;
            ItemParamEntry? usedDefinition = null;
            if (beginnerRebirth is { } beginner && reviveStats.Level <= beginner.Level)
            {
                if (!canRise)
                {
                    await ctx.SendFrameAsync(Opcode.S2C_RESURRECTION_REFUSED, Array.Empty<byte>(), randKey: rkRevive, reviveCt).ConfigureAwait(false);
                    return;
                }
                (hpRatio, mpRatio, expRatio) = (beginner.RestoreHpRatio, beginner.RestoreMpRatio, beginner.RestoreExpRatio);
                reviverId = PlayerRevivalFrames.NoReviver;
            }
            else
            {
                foreach (var held in itemStore.GetForCharacterBySlotKind(currentChar.CharacterId, SlotKind.Inventory))
                {
                    if (itemCatalog.GetById(held.ItemIndex) is { Effect: ItemParamEntry.RebirthEffect } heldDefinition)
                    {
                        usedItem = held;
                        usedDefinition = heldDefinition;
                    }
                }
                mapKindById.TryGetValue(reviveSt.MapId, out var reviveMap);
                if (usedItem is null
                    || usedDefinition is null
                    || usedDefinition.Type != ItemParamEntry.UseItemType
                    || !usedDefinition.IsUsable
                    || reviveStats.Level < usedDefinition.MinLevel
                    || !rebirthItemsById.TryGetValue(usedItem.ItemIndex, out var rebirth)
                    || !reviveMap.AllowsRebirthItem
                    || !canRise)
                {
                    Console.WriteLine($"{LogTag} {ctx.Remote} [combat] restart in place refused: item {(usedItem is null ? "none" : usedItem.ItemIndex.ToString())}, dead {reviveSt.IsDead}");
                    await ctx.SendFrameAsync(Opcode.S2C_ITEM_USE_REFUSED, Array.Empty<byte>(), randKey: rkRevive, reviveCt).ConfigureAwait(false);
                    return;
                }
                (hpRatio, mpRatio, expRatio) = (rebirth.HpRestoreRatio, rebirth.MpRestoreRatio, rebirth.ExpRestoreRatio);
                reviverId = reviveSt.PlayerEntityId;
            }

            reviveStats.Resurrect(reviveSt.PlayerMaxHp, reviveSt.PlayerMaxMp, hpRatio, mpRatio, expRatio);
            reviveSt.ResurrectionCompletesAt = DateTimeOffset.UtcNow + PlayerRevivalFrames.CompletionDelay;
            Console.WriteLine($"{LogTag} {ctx.Remote} [combat] player 0x{reviveSt.PlayerEntityId:X4} restarts in place ({(usedItem is null ? "beginner rule" : $"item {usedItem.ItemIndex}")}): HP {reviveStats.CurrentHp}, MP {reviveStats.CurrentMp}, exp {reviveStats.Exp}");
            await ctx.SendFrameAsync(Opcode.S2C_EXP_VALUE, PlayerDeathFrames.ExpValue(reviveStats.Exp), randKey: rkRevive, reviveCt).ConfigureAwait(false);
            lock (moveRng) rkRevive = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_OP_011D_TBD,
                ProgressFrames.TmTotal((byte)StatGrowth.TypeIndexOf(currentChar.FType), reviveStats.TmExp), randKey: rkRevive, reviveCt).ConfigureAwait(false);
            await SendToMapAsync(
                reviveSt.MapId, Opcode.S2C_RESURRECTED, PlayerRevivalFrames.Resurrected(reviveSt.PlayerEntityId, reviverId)).ConfigureAwait(false);

            if (usedItem is null || usedDefinition is null) return;
            lock (moveRng) rkRevive = (byte)moveRng.Next(1, 256);
            await ctx.SendFrameAsync(Opcode.S2C_ITEM_USED, PlayerRevivalFrames.ItemUsed(BagItemIds.OnWire(usedItem.ItemUid)), randKey: rkRevive, reviveCt).ConfigureAwait(false);
            if (usedDefinition.IsKeptWhenUsed) return;
            if (usedDefinition.Class == ItemParamEntry.StackableClass && usedItem.Stack > 1)
            {
                var left = itemStore.AdjustStack(usedItem.ItemUid, -1);
                Console.WriteLine($"{LogTag} {ctx.Remote} [combat] rebirth item {usedItem.ItemIndex} used, {left} left in the stack");
            }
            else
            {
                itemStore.Delete(usedItem.ItemUid);
                Console.WriteLine($"{LogTag} {ctx.Remote} [combat] rebirth item {usedItem.ItemIndex} used, the last one");
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_TRIGGER_75, (_, _, _) =>
        {
            if (targetedNpcEntityId != 0)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} dialog-dismiss — cleared target 0x{targetedNpcEntityId:X4}");
                targetedNpcEntityId = 0;
            }
            scriptDialog.Close();
            npcScript.Close();
            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    };

var primaryHost = new S2ServerHost
{
    LogTag = LogTag,
    Port = port,
    KeyTablePath = S2ServerHost.DefaultKeyTablePath(),
    SessionsLogIn = true,
    OnSessionAccepted = sessionHandler,
    OnSessionClosed = async closedCtx =>
    {
        if (sessionReleases.TryRemove(closedCtx, out var release)) await release().ConfigureAwait(false);
        await systemLink.ClosedAsync(closedCtx, CancellationToken.None).ConfigureAwait(false);
    },
};

using var stopSignal = S2ServerHost.WireCtrlC();

var swingLoopTask = Task.CompletedTask;
var partyQuestTimerTask = Task.Run(async () =>
{
    using var partyQuestTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    var timedItemTicks = 0;
    try
    {
        while (await partyQuestTimer.WaitForNextTickAsync(stopSignal.Token).ConfigureAwait(false))
        {
            try
            {
                await partyQuests.TickAsync(stopSignal.Token).ConfigureAwait(false);
                if (scriptEngine.ConsumeReloadRequest()) ReloadScripts();
                await scriptEngine.RunTimersAsync(scriptWorld, DateTimeOffset.Now, stopSignal.Token).ConfigureAwait(false);
                if (++timedItemTicks >= tPoseidon.Core.Game.Items.TimedItemRules.TickInterval.TotalSeconds)
                {
                    timedItemTicks = 0;
                    foreach (var holder in liveSessions.Values.Where(st => st.Character is not null && st.TimedItems.Count > 0).ToArray())
                    {
                        await TickTimedItemsAsync(holder).ConfigureAwait(false);
                    }
                }
                foreach (var mailReader in liveSessions.Values.Where(st => st.ClientReady && st.Character is not null).ToArray())
                {
                    await mail.SyncAsync(mailReader).ConfigureAwait(false);
                }
                var soulNow = DateTimeOffset.UtcNow;
                await couples.TickAsync(soulNow).ConfigureAwait(false);
                await fiesta.TickAsync().ConfigureAwait(false);
                await harcon.TickAsync(soulNow).ConfigureAwait(false);
                await guildGames.TickAsync(soulNow).ConfigureAwait(false);
                await puzzles.TickAsync(soulNow).ConfigureAwait(false);
                foreach (var missionRunner in liveSessions.Values.Where(st => st.Mission is not null).ToArray())
                {
                    await missions.TickAsync(missionRunner, soulNow).ConfigureAwait(false);
                }
                foreach (var spotOwner in liveSessions.Values.Where(st => st.SurpriseSpot is not null).ToArray())
                {
                    await drillController.TickSpotAsync(spotOwner, soulNow).ConfigureAwait(false);
                }
                foreach (var petCollector in liveSessions.Values.Where(st => st.ClientReady && petCollection.IsCollecting(st)).ToArray())
                {
                    await petCollection.TickAsync(petCollector, soulNow).ConfigureAwait(false);
                }
                foreach (var soulHolder in liveSessions.Values.Where(st => st.Soul is not null && st.Character is not null).ToArray())
                {
                    await soulSystem.TickAsync(soulHolder, soulNow).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                Console.WriteLine($"{LogTag} [party-quest] timer send failed: {ex.Message}");
            }
        }
    }
    catch (OperationCanceledException) when (stopSignal.IsCancellationRequested)
    {
    }
});

var timerNpcManager = new tPoseidon.Core.Net.World.TimerNpcManager(
    msg => Console.WriteLine($"{LogTag} [timer-npc] {msg}"));
var playerPositionRegistry = new tPoseidon.Core.Net.World.PlayerPositionRegistry();
var aggroScanner = new tPoseidon.Core.Net.World.ZoneAggroScanner(playerPositionRegistry);
var aggroDetector = new tPoseidon.Core.Game.Monsters.AggroDetector();
aggroDetector.TargetAcquired += (mob, target) =>
    Console.WriteLine($"{LogTag} [aggro] mob 0x{mob.EntityId:X4} ({mob.Name}) acquired target 0x{target:X4} on map {mob.MapId}");
var worldHeartbeat = new tPoseidon.Core.Net.World.WorldHeartbeat(
    zoneManager,
    timerNpcManager,
    aggroDetector,
    aggroScanner,
    msg => Console.WriteLine($"{LogTag} [heartbeat] {msg}"));
worldHeartbeat.Start();

async Task HandleBrokeredFrameAsync(
    tPoseidon.Core.InterServer.InterServerFrame frame,
    CancellationToken frameCt)
{
    if (frame.Opcode == tPoseidon.Core.InterServer.InterServerOpcode.McmdRequestZoneUserCount)
    {
        var (countedZone, _) = tPoseidon.Core.InterServer.ZoneUserCountMessages.DecodeRouted(frame.Body);
        var users = liveSessions.Values.Count(player => player.MapId == countedZone);
        if (masterClient is { IsConnected: true } countClient)
        {
            await countClient.SendAsync(tPoseidon.Core.InterServer.MasterZoneUserCount.Answer(frame, selfIdentity.ServerIndex, users), frameCt).ConfigureAwait(false);
        }
        Console.WriteLine($"{LogTag} {meshTag} zone {countedZone} has {users} users (asked by idx={frame.Header.SourceServerIndex})");
        return;
    }

    if (frame.Opcode == tPoseidon.Core.InterServer.InterServerOpcode.McmdLeaveZoneAckOk)
    {
        Console.WriteLine($"{LogTag} {meshTag} LeaveZone acknowledged by MasterServer for char 0x{frame.Header.CharUid:X16}");
        return;
    }

    if (frame.Opcode == tPoseidon.Core.InterServer.InterServerOpcode.McmdEnterZoneAckOk)
    {
        Console.WriteLine($"{LogTag} {meshTag} EnterZone acknowledged by MasterServer for char 0x{frame.Header.CharUid:X16}");
        return;
    }

    if (frame.Opcode == tPoseidon.Core.InterServer.InterServerOpcode.McmdEnterZoneAckCancel)
    {
        Console.Error.WriteLine($"{LogTag} {meshTag} EnterZone CANCELLED by MasterServer for char 0x{frame.Header.CharUid:X16}");
        return;
    }

    if (frame.Opcode == tPoseidon.Core.InterServer.InterServerOpcode.McmdUniqueItemIssue)
    {
        var (issueKey, issuedIds) = tPoseidon.Core.InterServer.UniqueItemMessages.DecodeIssue(frame.Body);
        uniqueItemPool.Issue(issueKey, issuedIds);
        Console.WriteLine($"{LogTag} [unique] Master issued items [{string.Join(",", issuedIds)}] for key {issueKey}");
        return;
    }

    if (frame.Opcode == tPoseidon.Core.InterServer.InterServerOpcode.CcmdUniqueDutyExpire)
    {
        var (dutyCharUid, expireFileTime, nowFileTime) = tPoseidon.Core.InterServer.UniqueItemMessages.DecodeDutyExpire(frame.Body);
        if (liveSessions.Values.FirstOrDefault(dutySt => dutySt.Character?.CharacterId == dutyCharUid) is { } dutyHolder)
        {
            await dutyHolder.SendFrameAsync(Opcode.S2C_UNIQUE_DUTY_EXPIRE,
                tPoseidon.Core.Net.World.UniqueItemFrames.DutyExpire(expireFileTime, nowFileTime), (byte)Random.Shared.Next(1, 256), frameCt).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} [unique] 0x{dutyHolder.PlayerEntityId:X4} duty renewed until {DateTimeOffset.FromFileTime(expireFileTime):u}");
        }
        return;
    }

    switch (frame.Opcode)
    {
        case tPoseidon.Core.InterServer.InterServerOpcode.TpCharStoreCallAckOk:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpCharStoreCallAckCancel:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpItemStoreCallAckOk:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpItemStoreCallAckCancel:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpQuestStoreCallAckOk:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpQuestStoreCallAckCancel:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpGuideHistoryLoadAckOk:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpGuideHistoryLoadAckCancel:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpGuideHistoryUpsertSystemAckOk:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpGuideHistoryUpsertSystemAckCancel:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpGuideHistoryUpsertTutorialAckOk:
        case tPoseidon.Core.InterServer.InterServerOpcode.TpGuideHistoryUpsertTutorialAckCancel:
            return;
    }

    if (frame.Opcode != tPoseidon.Core.InterServer.InterServerOpcode.McmdPrepareEnteringZone)
    {
        Console.WriteLine($"{LogTag} {meshTag} unhandled brokered frame 0x{(ushort)frame.Opcode:X4}");
        return;
    }

    tPoseidon.Core.InterServer.PrepareEnteringZone prepare;
    try
    {
        prepare = tPoseidon.Core.InterServer.PrepareEnteringZone.DecodeBody(frame.Body);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.Error.WriteLine($"{LogTag} {meshTag} bad PrepareEnteringZone body: {ex.Message}");
        return;
    }

    brokeredJoins.Register(new tPoseidon.Kr2014.GameServer.BrokeredJoin(
        prepare.CharUid, prepare.FromZone, prepare.ToZone, prepare.Token, DateTimeOffset.UtcNow,
        prepare.ChangeZoneType, prepare.StartX, prepare.StartY));
    Console.WriteLine($"{LogTag} {meshTag} PrepareEnteringZone char 0x{prepare.CharUid:X16} zone {prepare.FromZone}->{prepare.ToZone}; registered brokered join");

    if (masterClient is not null)
    {
        var ackOk = new tPoseidon.Core.InterServer.InterServerFrame
        {
            Opcode = tPoseidon.Core.InterServer.InterServerOpcode.McmdPrepareEnteringZoneAckOk,
            Header = new tPoseidon.Core.InterServer.InterServerHeader
            {
                SourceServerIndex = selfIdentity.ServerIndex,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = prepare.CharUid,
                RequestId = frame.Header.RequestId,
            },
            Body = Array.Empty<byte>(),
        };
        await masterClient.SendAsync(ackOk, frameCt).ConfigureAwait(false);
        Console.WriteLine($"{LogTag} {meshTag} sent McmdPrepareEnteringZoneAckOk char 0x{prepare.CharUid:X16}");
    }
}

try
{
    var meshPassword = kr2014GameIni?.GetString("Password") ?? string.Empty;
    masterClient = await tPoseidon.Core.InterServer.MasterServerRegistration.ConnectAndRegisterAsync(
        ServerEndpointConfig.MasterHost,
        ServerEndpointConfig.MasterPort,
        selfIdentity,
        msg => Console.WriteLine(msg),
        stopSignal.Token,
        onBrokeredFrame: HandleBrokeredFrameAsync,
        password: meshPassword).ConfigureAwait(false);

    if (Environment.GetEnvironmentVariable("MT_GUIDE_HISTORY_VIA_MASTER") != "0")
    {
        guideHistoryStore = new tPoseidon.Core.InterServer.MasterRoutedGuideHistoryStore(
            masterClient,
            new PostgresGuideHistoryStore(pg.DataSource),
            selfIdentity.ServerIndex,
            msg => Console.WriteLine($"{LogTag} {msg}"));
        Console.WriteLine($"{LogTag} guide-history store: master-routed (Pass 1) with PostgresGuideHistoryStore fallback");
    }
    else
    {
        Console.WriteLine($"{LogTag} guide-history store: direct PostgresGuideHistoryStore (MT_GUIDE_HISTORY_VIA_MASTER=0 opt-out)");
    }

    if (Environment.GetEnvironmentVariable("MT_QUEST_VIA_MASTER") != "0")
    {
        questStore = new tPoseidon.Core.InterServer.MasterRoutedQuestStore(
            masterClient,
            new PostgresQuestStore(pg.DataSource),
            selfIdentity.ServerIndex,
            msg => Console.WriteLine($"{LogTag} {msg}"));
        questHistoryStore = new tPoseidon.Core.InterServer.MasterRoutedQuestHistoryStore(
            masterClient,
            new PostgresQuestHistoryStore(pg.DataSource),
            selfIdentity.ServerIndex,
            msg => Console.WriteLine($"{LogTag} {msg}"));
        Console.WriteLine($"{LogTag} quest stores: master-routed (Pass 2) with PostgresQuest*Store fallback");
    }
    else
    {
        Console.WriteLine($"{LogTag} quest stores: direct PostgresQuest*Store (MT_QUEST_VIA_MASTER=0 opt-out)");
    }

    if (Environment.GetEnvironmentVariable("MT_ITEM_VIA_MASTER") != "0")
    {
        itemStore = new tPoseidon.Core.InterServer.MasterRoutedItemStore(
            masterClient,
            new PostgresItemStore(pg.DataSource),
            selfIdentity.ServerIndex,
            msg => Console.WriteLine($"{LogTag} {msg}"));
        itemStore = new tPoseidon.Core.Game.Items.MixSlotItemStore(itemStore, () => itemPropertyRules, itemPropertyStore, itemCatalog);
        Console.WriteLine($"{LogTag} item store: master-routed (Pass 3) with PostgresItemStore fallback");
    }
    else
    {
        Console.WriteLine($"{LogTag} item store: direct PostgresItemStore (MT_ITEM_VIA_MASTER=0 opt-out)");
    }

    if (Environment.GetEnvironmentVariable("MT_CHAR_VIA_MASTER") != "0")
    {
        var routedCharStore = new tPoseidon.Core.InterServer.MasterRoutedCharStore(
            masterClient,
            new PostgresCharStore(pg.DataSource),
            selfIdentity.ServerIndex,
            msg => Console.WriteLine($"{LogTag} {msg}"));
        accountStore = new PostgresAccountStore(pg.DataSource, charStore: routedCharStore);
        Console.WriteLine($"{LogTag} char store: master-routed (Pass 4) with PostgresCharStore fallback; accountStore rewrapped to delegate char ops");
    }
    else
    {
        Console.WriteLine($"{LogTag} char store: direct PostgresCharStore via IAccountStore (MT_CHAR_VIA_MASTER=0 opt-out)");
    }
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Console.Error.WriteLine($"{LogTag} MasterServer registration failed: {ex.Message} — continuing without mesh");
}

var npcIdleProfiles = new NpcIdleProfiles(
    npcMotionCatalog,
    shopNpcMotionCatalog,
    skillShopNpcMotionCatalog,
    shopZoneLookup.All.Select(e => e.NpcType).ToHashSet(),
    skillShopZoneLookup.All.Select(e => e.NpcType).ToHashSet());
var npcIdleCycle = new NpcIdleCycle(npcIdleProfiles.For);
var monsterWanderCycle = new tPoseidon.Core.Game.Monsters.MonsterWanderCycle(monsterCatalog);
var playerMeleeTicker = new tPoseidon.Core.Game.Combat.PlayerMeleeTicker(monsterManager, monsterCatalog);
var monsterBattleCycle = new tPoseidon.Core.Game.Combat.MonsterBattleCycle(
    monsterCatalog, combatInfoByType,
    tacticsOf: monsterParamId => monsterCatalog.GetById(monsterParamId)?.TacticsTable?.Trim() is { Length: > 0 } tacticsName
        && monsterSkillTactics.TryGetValue(tacticsName, out var tacticsList)
            ? tacticsList
            : Array.Empty<MonsterTacticEntry>(),
    castable: (monsterSkillId, monsterSkillLevel) => skillBook.Find(monsterSkillId) is { } monsterSkill
        && MonsterSkills.Castable(monsterSkill.ActionId, monsterSkillLevel));
var longestRecoveryTick = TimeSpan.FromSeconds(1);

string PartnerNameOf(ulong partnerId) =>
    partnerId != 0 && accountStore.GetCharacterById(partnerId) is { } partner ? partner.Name : string.Empty;

ulong CouplePartnerOf(ulong characterId) =>
    coupleStore.FindCoupleForCharacter(characterId) is { } couple
        ? coupleStore.GetJoins(couple.CoupleCode).Select(join => join.CharacterId).FirstOrDefault(member => member != characterId)
        : 0;

bool WearsSlot(InGameSessionState wearer, int slot) =>
    wearer.Character is { } wearing && itemStore.FindBySlot(wearing.CharacterId, SlotKind.Equipment, (byte)slot) is not null;

int[] EffectBasesOf(InGameSessionState state)
{
    var effectBase = state.CombatBase;
    return new[]
    {
        effectBase.Ap, effectBase.Ac, Math.Max(0, effectBase.Dx), effectBase.Mp, effectBase.Ma, effectBase.Md,
        effectBase.Wt, effectBase.Da, effectBase.Lk, effectBase.Hp, effectBase.Dp, effectBase.Hv,
    };
}

int PlayerConditionOf(InGameSessionState player) =>
    player.Effects.Snapshot().Where(held => held.IsApplied).Aggregate(0, (mask, held) => mask | held.Kind.PlayerCondition);

(tPoseidon.Core.Game.Items.WornItem? Gun, Item? Bullet) GunGearOf(InGameSessionState shooter)
{
    if (shooter.Character is not { } shooterCharacter) return (null, null);
    var worn = tPoseidon.Core.Game.Items.EquipmentRules.WornBy(itemStore, itemCatalog, shooterCharacter.CharacterId);
    return (
        worn.FirstOrDefault(held => held.Definition.SubType == GunSkills.GunSubType),
        worn.FirstOrDefault(held => held.Slot == EquipmentSlot.Common && held.Definition.Type == GunSkills.BulletItemType)?.Item);
}

int WeaponDaOf(ulong characterId) =>
    tPoseidon.Core.Game.Items.EquipmentRules.WornBy(itemStore, itemCatalog, characterId)
        .FirstOrDefault(worn => worn.Slot == EquipmentSlot.Weapon)?.Definition.StatBonuses is { Count: > tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatDa } weaponStats
        ? weaponStats[tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatDa]
        : 0;

static float SpeedOfMoveType(MoveSpeedParamEntry speeds, byte effectiveMoveType, float gameMasterSpeed = 1f) => gameMasterSpeed * effectiveMoveType switch
{
    tPoseidon.Core.Game.Combat.MeleeApproach.ChaseMoveType => speeds.ChaseSpeed,
    tPoseidon.Core.Game.Combat.MeleeApproach.WalkMoveType => speeds.WalkSpeed,
    _ => speeds.RunSpeed,
};

static byte ScaledSpeedQuarter(byte quarter, float gameMasterSpeed) =>
    (byte)Math.Min(byte.MaxValue, (int)Math.Round(quarter * gameMasterSpeed));

async Task AnnounceGameMasterSpeedAsync(InGameSessionState player)
{
    if (playerMoveSpeeds is not { } speeds) return;
    var speed = SpeedOfMoveType(speeds, tPoseidon.Core.Game.Combat.MeleeApproach.EffectiveMoveType(player.PlayerMoveType), player.GameMasterSpeed);
    player.AnnouncedSpeedQuarter = ProgressFrames.SpeedQuarter(speed);
    await SendToMapAsync(player.MapId, Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(player.PlayerEntityId, speed)).ConfigureAwait(false);
}

async Task PresentGameMasterItemAsync(InGameSessionState receiver, uint itemId, ushort count)
{
    if (receiver.Character is not { } character || itemCatalog.GetById((int)itemId) is not { } definition)
    {
        Console.WriteLine($"{LogTag} [gm] present item {itemId} x{count}: unknown item or receiver without a character");
        return;
    }
    var bag = itemStore.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
    var bagWeight = 0;
    Item? stack = null;
    foreach (var held in bag)
    {
        bagWeight += (itemCatalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits;
        if (stack is null && definition.IsStackable && held.ItemIndex == (int)itemId) stack = held;
    }
    var units = definition.ObjectClass == Item.RareClass ? 1 : (int)count;
    if (!PickupRules.Fits(receiver.PlayerMaxWt, bagWeight, bag.Count, stack is not null, definition.Weight, units))
    {
        Console.WriteLine($"{LogTag} [gm] present item {itemId} x{count} for 0x{character.CharacterId:X}: bag weight {bagWeight} of {receiver.PlayerMaxWt}, {bag.Count} items");
        return;
    }
    byte change;
    ulong itemUid;
    ushort bagCount;
    byte mixSlot = 0;
    if (stack is not null)
    {
        change = PickupRules.StackGrew;
        itemUid = stack.ItemUid;
        bagCount = itemStore.AdjustStack(stack.ItemUid, count);
    }
    else
    {
        var created = itemStore.GrantToCharacter(character.CharacterId, (int)itemId, definition.ObjectClass, SlotKind.Inventory, FreeBagPosition(bag), count);
        change = PickupRules.ItemCreated;
        itemUid = created.ItemUid;
        bagCount = created.Stack;
        mixSlot = created.ItemSlot;
    }
    await receiver.SendFrameAsync(
        Opcode.S2C_ADD_TO_INVENTORY,
        GroundItemFrames.AddedToBag(change, definition.ObjectClass, itemId, BagItemIds.OnWire(itemUid), bagCount, mixSlot),
        (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
    Console.WriteLine($"{LogTag} [gm] present item {itemId} x{count} for 0x{character.CharacterId:X}: bag item 0x{itemUid:X} now holds {bagCount} (change {change})");
}

async Task ApplyGameMasterVisibilityAsync(InGameSessionState gm)
{
    if (gm.Character is null) return;
    var now = DateTimeOffset.UtcNow;
    var (gmX, gmY) = PlayerPositionAt(gm, now);
    foreach (var other in liveSessions.Values.ToArray())
    {
        if (ReferenceEquals(other, gm) || other.MapId != gm.MapId || !other.ClientReady || other.Character is null) continue;
        var (otherX, otherY) = PlayerPositionAt(other, now);
        if (!InViewOn(gm.MapId, gmX, gmY, otherX, otherY)) continue;
        if (gm.GameMasterInvisible)
        {
            await SendSectorFrameAsync(other, Opcode.S2C_ENTITY_DESPAWN, DespawnOf(gm.PlayerEntityId)).ConfigureAwait(false);
            await SendSectorFrameAsync(gm, Opcode.S2C_ENTITY_DESPAWN, DespawnOf(other.PlayerEntityId)).ConfigureAwait(false);
            continue;
        }
        await SendSectorFrameAsync(other, Opcode.NPC_SPAWN, PlayerSpawnOf(gm)).ConfigureAwait(false);
        if (!other.GameMasterInvisible) await SendSectorFrameAsync(gm, Opcode.NPC_SPAWN, PlayerSpawnOf(other)).ConfigureAwait(false);
    }
}

tPoseidon.Core.Game.Stats.RecoveryAmounts ManaRecoveryOf(InGameSessionState recoveringState) =>
    recoveringState.Effects.Snapshot().FirstOrDefault(held => held.IsApplied && held.Kind.RaisesManaRecovery) is { } fastRecovery && fastRecovery.Amount > 0f
        ? recoveringState.RecoveryAmounts with
        {
            Mp = recoveringState.RecoveryAmounts.Mp + (int)Math.Truncate(recoveringState.RecoveryAmounts.Mp * (double)fastRecovery.Amount),
        }
        : recoveringState.RecoveryAmounts;

float DarkBonusOf(InGameSessionState bonusState) =>
    bonusState.Effects.Snapshot()
        .Where(held => held.IsApplied && held.Kind.ActionId == tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.BloodyOath)
        .Select(held => held.Amount)
        .DefaultIfEmpty(0f)
        .Max();

long ShieldPool(InGameSessionState shieldCaster, tPoseidon.Core.LibConfig.SkillTableRow shieldRow, tPoseidon.Core.Game.Skills.Effects.SkillEffectKind shieldKind) =>
    shieldKind.PoolFromDig
        ? FormSkills.BunkerPool(shieldCaster.PlayerDa, shieldRow.Value(shieldKind.AmountColumn) ?? 0f)
        : WireLimits.Saturate(Math.Truncate(shieldCaster.PlayerMa * tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.ShieldPoolScale * (double)(shieldRow.Value(shieldKind.AmountColumn) ?? 0f)));

int RangeBonusOf(InGameSessionState rangeState) =>
    rangeState.Effects.Snapshot().Where(held => held.IsApplied && held.Kind.RangeColumn is not null).Sum(held => (int)held.Amount);

tPoseidon.Core.Game.Skills.Effects.SkillEffectCaster? EffectCasterOf(ushort entityId) =>
    liveSessions.Values.FirstOrDefault(casterSt => casterSt.PlayerEntityId == entityId) is { } effectCaster
        ? new tPoseidon.Core.Game.Skills.Effects.SkillEffectCaster(
            effectCaster.PlayerAp, effectCaster.PlayerHv, effectCaster.PlayerMa, effectCaster.PlayerDa, effectCaster.PlayerAc, effectCaster.PlayerLk)
        : null;

int[] ApplyEffectDeltas(InGameSessionState state)
{
    var effectBase = state.CombatBase;
    var deltas = state.Effects.Deltas(EffectBasesOf(state), EffectCasterOf);
    var passiveBonuses = PassiveStats.Bonuses(state.Skills, skillBook);
    for (var stat = 0; stat < deltas.Length; stat++) deltas[stat] += passiveBonuses[stat];
    state.PlayerMaxHp = Math.Clamp((long)effectBase.Hp + deltas[tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatHp], 0L, ServerRates.CapsSection.Limit(serverRatesProvider.Current.Caps.MaxHpPool));
    state.PlayerMaxMp = Math.Clamp((long)effectBase.Mp + deltas[tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatMp], 0L, ServerRates.CapsSection.Limit(serverRatesProvider.Current.Caps.MaxMpPool));
    state.PlayerMaxWt = effectBase.Wt + deltas[tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatWt];
    state.PlayerAp = Math.Max(0L, (long)effectBase.Ap + deltas[MonsterStat.Ap]);
    state.PlayerMa = Math.Max(0L, (long)effectBase.Ma + deltas[MonsterStat.Ma]);
    state.PlayerAc = Math.Max(0L, (long)effectBase.Ac + deltas[MonsterStat.Ac]);
    state.PlayerDx = Math.Max(0, effectBase.Dx) + deltas[MonsterStat.Dx];
    state.PlayerLk = effectBase.Lk + deltas[MonsterStat.Lk];
    state.PlayerDp = effectBase.Dp + deltas[MonsterStat.Dp];
    state.PlayerHv = effectBase.Hv + deltas[MonsterStat.Hv];
    state.PlayerDa = effectBase.Da + deltas[MonsterStat.Da];
    state.PlayerMd = effectBase.Md + deltas[MonsterStat.Md];
    return deltas;
}

tPoseidon.Core.Game.Skills.Effects.SkillEffectBook MonsterEffectsOf(ushort monsterEntityId) =>
    monsterEffects.GetOrAdd(monsterEntityId, _ => new tPoseidon.Core.Game.Skills.Effects.SkillEffectBook(
        tPoseidon.Core.Game.Skills.Effects.SkillEffectBook.MonsterCapacities));

bool EvadeBlocked(ushort monsterEntityId) =>
    monsterEffects.TryGetValue(monsterEntityId, out var paralysisBook)
    && paralysisBook.Snapshot().Any(held => held.IsApplied && held.Kind.PreventsEvade);

bool HardenedMonster(ushort monsterEntityId) =>
    monsterEffects.TryGetValue(monsterEntityId, out var hardenBook)
    && hardenBook.Snapshot().Any(held => held.IsApplied && held.Kind.HalvesHits);

void SlowMonster(MonsterEntity monster) =>
    monster.SlowRatio = monsterEffects.TryGetValue(monster.EntityId, out var slowBook)
        ? slowBook.Snapshot().Where(held => held.IsApplied).Sum(held => held.Slow)
        : 0f;

void ApplyMonsterEffectDeltas(MonsterEntity monster)
{
    if (!monsterEffects.TryGetValue(monster.EntityId, out var book)) return;
    monster.SetStatDeltas(book.Deltas(MonsterEffectBases(monster), EffectCasterOf, ownerIsPlayer: false));
}

int[] MonsterEffectBases(MonsterEntity monster)
{
    var bases = new int[MonsterEntity.StatCount];
    if (monsterCatalog.GetById(monster.MonsterParamId) is not { } baseDefinition) return bases;
    bases[MonsterStat.Ap] = baseDefinition.ApLv;
    bases[MonsterStat.Ac] = baseDefinition.AcLv;
    bases[MonsterStat.Md] = baseDefinition.MdLv;
    bases[MonsterStat.Lk] = baseDefinition.LkLv;
    bases[MonsterStat.Dp] = baseDefinition.DpLv;
    bases[MonsterStat.Hv] = baseDefinition.HvLv;
    return bases;
}

void DropMonsterEffects(ushort monsterEntityId)
{
    if (!monsterEffects.TryRemove(monsterEntityId, out var book)) return;
    foreach (var dropped in book.Clear()) idAllocator.Release(dropped.ObjectId);
}

async Task RemovePetAsync(tPoseidon.Core.Game.Pets.PetActor pet)
{
    await SendToMapAsync(pet.MapId, Opcode.S2C_GROUND_ITEM_DESPAWN, PetFrames.Removed(pet.EntityId)).ConfigureAwait(false);
    idAllocator.Release(pet.EntityId);
}

static int PropertyValue(IReadOnlyList<ItemProperty> properties, int propertyId) =>
    properties.Where(property => property.Id == propertyId).Sum(property => (int)property.Value);

async Task SendPartyShareFramesAsync(
    KeyValuePair<S2SessionContext, InGameSessionState> member,
    IReadOnlyList<(Opcode Opcode, byte[] Payload, SwingAudience Audience)> frames)
{
    foreach (var (opcode, payload, audience) in frames)
    {
        if (audience == SwingAudience.Everyone)
        {
            await SendToMapAsync(member.Value.MapId, opcode, payload).ConfigureAwait(false);
            continue;
        }
        try
        {
            await member.Key.SendFrameAsync(opcode, payload, (byte)Random.Shared.Next(1, 256)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            liveSessions.TryRemove(member.Key, out _);
            return;
        }
    }
}

async Task SendToOthersOnMapAsync(InGameSessionState self, Opcode opcode, byte[] payload)
{
    if (self.GameMasterInvisible) return;
    foreach (var watcher in liveSessions.ToArray())
    {
        if (ReferenceEquals(watcher.Value, self) || watcher.Value.MapId != self.MapId || !watcher.Value.ClientReady) continue;
        try
        {
            await watcher.Value.SendFrameAsync(opcode, payload, (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            liveSessions.TryRemove(watcher.Key, out _);
        }
    }
}

byte[] PlayerSpawnOf(InGameSessionState player) =>
    tPoseidon.Kr2014.Wire.PlayerSpawnLoginPacket.FromCharacter(
        player.Character!, player.PlayerEntityId, itemStore, (player.PlayerX, player.PlayerY), player.AnnouncedSpeedQuarter,
        GameMasterController.SpawnFlagsOf(player)).EncodePayload();

tPoseidon.Core.Game.Monsters.SectorGrid? SectorGridOf(ushort mapId) =>
    sectorGrids.GetOrAdd(mapId, id => zoneGeometryByMap.TryGetValue(id, out var sectorGeometry) && sectorGeometry is not null
        ? new tPoseidon.Core.Game.Monsters.SectorGrid(sectorGeometry)
        : null);

bool InViewOn(ushort mapId, int viewerX, int viewerY, int x, int y) =>
    SectorGridOf(mapId) is not { } viewGrid || viewGrid.Sees(viewerX, viewerY, x, y);

(int X, int Y) PlayerPositionAt(InGameSessionState player, DateTimeOffset now) =>
    player.Walk is { } walk && walk.ToX == player.PlayerX && walk.ToY == player.PlayerY
        ? walk.PositionAt(now)
        : (player.PlayerX, player.PlayerY);

InGameSessionState? PvpTargetOf(InGameSessionState attacker, IEnumerable<InGameSessionState> players) =>
    tPoseidon.Core.Game.PvP.PvpHitRules.CanAttackPlayer(attacker.PvpMode)
        ? players.FirstOrDefault(player => player != attacker && player.PlayerEntityId == attacker.CurrentTargetEntityId
            && player.MapId == attacker.MapId && !player.IsDead && player.Stats is { CurrentHp: > 0 })
        : null;

byte[] DespawnOf(ushort entityId)
{
    var despawn = new tPoseidon.Core.Net.PacketWriter(sizeof(ushort));
    despawn.WriteUInt16(entityId);
    return despawn.ToArray();
}

const int SummonedMonsterRegion = -1;

Task<int> QueueNpcSkill(InGameSessionState player, int skillId)
{
    player.PendingNpcSkills.Enqueue(skillId);
    return Task.FromResult(tPoseidon.Core.Game.Quests.NpcScript.Silent);
}

async Task SummonNpcMonstersAsync(InGameSessionState player, int setId)
{
    if (!npcSummonSets.TryGetValue(setId, out var summons))
    {
        Console.WriteLine($"{LogTag} [npc] 0x{player.PlayerEntityId:X4} monster summon: no {tPoseidon.Core.Game.Quests.MonsterSummonRules.TableName} row {setId}");
        return;
    }
    var average = mapMonsterTables.FirstOrDefault(table => table.Id == player.MapId).MonsterAvrNum;
    var onMap = monsterManager.GetByMap(player.MapId).Count;
    if (!tPoseidon.Core.Game.Quests.MonsterSummonRules.HasRoom(onMap, average))
    {
        Console.WriteLine($"{LogTag} [npc] 0x{player.PlayerEntityId:X4} monster summon {setId}: map {player.MapId} has {onMap} monsters (MonsterAvrNum {average})");
        return;
    }
    var (summonX, summonY) = PlayerPositionAt(player, DateTimeOffset.UtcNow);
    var targetsSummoner = mapKindById.TryGetValue(player.MapId, out var summonMap) && tPoseidon.Core.Game.Quests.MonsterSummonRules.TargetsSummoner(summonMap.Class);
    foreach (var (monsterId, count) in summons)
    {
        for (var i = 0; i < count; i++)
        {
            if (monsterManager.Spawn(monsterId, player.MapId, (ushort)summonX, (ushort)summonY, null, null, SummonedMonsterRegion) is not { } born) break;
            if (targetsSummoner) born.AddAttacker(player.PlayerEntityId);
            await SendToMapAsync(player.MapId, Opcode.NPC_SPAWN, MobSpawnFrames.Encode(born, monsterCatalog.GetById(born.MonsterParamId))).ConfigureAwait(false);
        }
    }
    Console.WriteLine($"{LogTag} [npc] 0x{player.PlayerEntityId:X4} monster summon {setId} on map {player.MapId}: {string.Join(", ", summons.Select(entry => $"{entry.Count} x {entry.MonsterId}"))}");
}

async Task<bool> SummonGameMasterMonstersAsync(InGameSessionState gm, uint chrType, byte count)
{
    var (summonX, summonY) = PlayerPositionAt(gm, DateTimeOffset.UtcNow);
    for (var i = 0; i < count; i++)
    {
        var born = monsterManager.Spawn((int)chrType, gm.MapId, (ushort)summonX, (ushort)summonY, null, null, SummonedMonsterRegion);
        if (born is null)
        {
            Console.WriteLine($"{LogTag} [gm] summon: monster {chrType} is not in the monster table");
            return false;
        }
        await SendToMapAsync(gm.MapId, Opcode.NPC_SPAWN, MobSpawnFrames.Encode(born, monsterCatalog.GetById(born.MonsterParamId))).ConfigureAwait(false);
    }
    return true;
}

async Task SummonGameMasterNpcAsync(InGameSessionState gm, uint chrType, IReadOnlyList<string> lines)
{
    var (summonX, summonY) = PlayerPositionAt(gm, DateTimeOffset.UtcNow);
    if (npcManager.SummonNpc(gm.MapId, (int)chrType, NpcPoint.Xy(summonX, summonY), lines) is not { } npc) return;
    await SendToMapAsync(gm.MapId, Opcode.NPC_SPAWN, EnterWorldBatchSynthesizer.NpcSpawn(npc, characterInfoCatalog)).ConfigureAwait(false);
}

async Task<int> RemoveGameMasterObjectsAsync(ushort mapId, ushort objectId)
{
    var targets = objectId == 0
        ? monsterManager.GetByMap(mapId).ToArray()
        : monsterManager.Get(objectId) is { MapId: var foundMap } found && foundMap == mapId ? new[] { found } : Array.Empty<MonsterEntity>();
    foreach (var target in targets)
    {
        monsterManager.Remove(target.EntityId);
        monsterWanderCycle.Forget(target.EntityId);
        monsterBattleCycle.Forget(target.EntityId);
        DropMonsterEffects(target.EntityId);
        monsterRegenCycle.Release(target);
        await SendToMapAsync(mapId, Opcode.S2C_ENTITY_DESPAWN, DespawnOf(target.EntityId)).ConfigureAwait(false);
    }
    return targets.Length;
}

List<(int ChrType, SectorObject Object)> NpcObjectsOn(ushort mapId)
{
    var npcs = new List<(int ChrType, SectorObject Object)>();
    foreach (var npc in worldEntities.InMap(mapId))
    {
        npcs.Add((npc.ChrType, new SectorObject(npc.EntityId, (int)npc.Spawn.X, (int)npc.Spawn.Y, false, Opcode.NPC_SPAWN,
            () => EnterWorldBatchSynthesizer.NpcSpawn(npc, characterInfoCatalog))));
    }
    foreach (var shop in shopManager.GetByMap(mapId))
    {
        npcs.Add((shop.NpcType, new SectorObject(shop.EntityId, shop.SpawnX, shop.SpawnY, false, Opcode.NPC_SPAWN,
            () => EnterWorldBatchSynthesizer.ShopSpawn(shop, characterInfoCatalog))));
    }
    foreach (var skillShop in skillShopManager.GetByMap(mapId))
    {
        npcs.Add((skillShop.NpcType, new SectorObject(skillShop.EntityId, skillShop.SpawnX, skillShop.SpawnY, false, Opcode.NPC_SPAWN,
            () => EnterWorldBatchSynthesizer.SkillShopSpawn(skillShop, characterInfoCatalog))));
    }
    return npcs;
}

async Task ShowNpcAsync(ushort mapId, SectorObject npc, bool shown)
{
    if (shown) await SendToViewersAsync(mapId, npc.X, npc.Y, npc.Opcode, npc.Payload()).ConfigureAwait(false);
    else await SendToViewersAsync(mapId, npc.X, npc.Y, Opcode.S2C_ENTITY_DESPAWN, DespawnOf(npc.EntityId)).ConfigureAwait(false);
}

async Task<int?> SetGameMasterNpcActiveAsync(ushort mapId, ushort objectId, bool active)
{
    var found = NpcObjectsOn(mapId).Where(npc => npc.Object.EntityId == objectId).Select(npc => ((int ChrType, SectorObject Object)?)npc).FirstOrDefault();
    if (found is not { } npc) return null;
    if (npcActivation.Set(objectId, active)) await ShowNpcAsync(mapId, npc.Object, active).ConfigureAwait(false);
    return npc.ChrType;
}

bool SetGameMasterIssueItem(ushort mapId, ushort objectId, uint itemId, ushort count)
{
    if (!NpcObjectsOn(mapId).Any(npc => npc.Object.EntityId == objectId)) return false;
    npcIssuedItems.Set(objectId, itemId, count);
    return true;
}

async Task<uint> OxGiveItemAsync(InGameSessionState giver, uint itemId, byte count)
{
    if (!ItemGiveFrames.OxMaps.Contains(giver.MapId)) return tPoseidon.Core.Net.Social.GameMasterFrames.AckNotOxMap;
    if (giver.Character is not { } giverCharacter || itemCatalog.GetById((int)itemId) is not { } definition) return tPoseidon.Core.Net.Social.GameMasterFrames.AckGiveFailed;
    if (!itemStore.GetForCharacterBySlotKind(giverCharacter.CharacterId, SlotKind.Inventory).Any(held => held.ItemIndex == (int)itemId)) return tPoseidon.Core.Net.Social.GameMasterFrames.AckGiveFailed;
    var recipients = liveSessions.Values
        .Where(other => other != giver && other.MapId == giver.MapId && other.Character is not null)
        .Take(ItemGiveFrames.MaxRecipients)
        .ToArray();
    var handed = 0;
        foreach (var receiver in recipients)
        {
            var giverBag = itemStore.GetForCharacterBySlotKind(giverCharacter.CharacterId, SlotKind.Inventory);
            var source = giverBag.FirstOrDefault(held => held.ItemIndex == (int)itemId && held.Stack >= count);
            if (source is null)
            {
                Console.WriteLine($"{LogTag} [gm] OX give item {itemId}: the GM has fewer than {count} left, stopped after {handed} character(s)");
                break;
            }
            if (receiver.Character is not { } receiverCharacter || receiver.MapId != giver.MapId || !liveSessions.Values.Contains(receiver))
            {
                await RefuseOxGiftAsync(giver, itemId, source.ItemUid, count, string.Empty, ItemGiveFrames.RefusedReceiverGone).ConfigureAwait(false);
                continue;
            }
            if (receiver.IsDead)
            {
                await RefuseOxGiftAsync(giver, itemId, source.ItemUid, count, receiverCharacter.Name, ItemGiveFrames.RefusedReceiverBusy).ConfigureAwait(false);
                continue;
            }
            var bag = itemStore.GetForCharacterBySlotKind(receiverCharacter.CharacterId, SlotKind.Inventory);
            var bagWeight = bag.Sum(held => (itemCatalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits);
            var stack = definition.IsStackable ? bag.FirstOrDefault(held => held.ItemIndex == (int)itemId) : null;
            var units = definition.ObjectClass == Item.RareClass ? 1 : (int)count;
            if (!tPoseidon.Core.Game.Monsters.PickupRules.Fits(receiver.PlayerMaxWt, bagWeight, bag.Count, stack is not null, definition.Weight, units))
            {
                Console.WriteLine($"{LogTag} [gm] OX give item {itemId}: no room for 0x{receiverCharacter.CharacterId:X}");
                await RefuseOxGiftAsync(giver, itemId, source.ItemUid, count, receiverCharacter.Name, ItemGiveFrames.RefusedNoRoom).ConfigureAwait(false);
                continue;
            }
            BagOperation taken;
            if (source.Stack > count)
            {
                var left = itemStore.AdjustStack(source.ItemUid, -count);
                taken = new BagOperation(QuestResultFrames.StackChanged, definition.ObjectClass, itemId, BagItemIds.OnWire(source.ItemUid), left);
            }
            else
            {
                itemStore.Delete(source.ItemUid);
                taken = new BagOperation(QuestResultFrames.Removed, definition.ObjectClass, itemId, BagItemIds.OnWire(source.ItemUid), 0);
            }
            BagOperation added;
            if (stack is not null)
            {
                var total = itemStore.AdjustStack(stack.ItemUid, count);
                added = new BagOperation(QuestResultFrames.StackChanged, definition.ObjectClass, itemId, BagItemIds.OnWire(stack.ItemUid), total);
            }
            else
            {
                var created = itemStore.GrantToCharacter(receiverCharacter.CharacterId, (int)itemId, definition.ObjectClass, SlotKind.Inventory, FreeBagPosition(bag), count);
                added = new BagOperation(QuestResultFrames.Created, definition.ObjectClass, itemId, BagItemIds.OnWire(created.ItemUid), created.Stack, created.ItemSlot);
            }
            await receiver.SendFrameAsync(Opcode.S2C_ITEM_RECEIVED,
                ItemGiveFrames.Received(added, count, giver.PlayerEntityId, giverCharacter.Name, clientTextEncoding),
                (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
            await giver.SendFrameAsync(Opcode.S2C_ITEM_GIVEN,
                ItemGiveFrames.Given(taken, count, receiverCharacter.Name, clientTextEncoding),
                (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
            handed++;
        }
    Console.WriteLine($"{LogTag} [gm] OX give item {itemId} x{count} on map {giver.MapId}: {handed} of {recipients.Length} character(s)");
    return tPoseidon.Core.Net.Social.GameMasterFrames.AckOk;
}

Task RefuseOxGiftAsync(InGameSessionState giver, uint itemId, ulong itemUid, byte count, string receiverName, byte code) =>
    giver.SendFrameAsync(Opcode.S2C_ITEM_GIVE_REFUSED,
        ItemGiveFrames.Refused(itemId, BagItemIds.OnWire(itemUid), count, receiverName, code, clientTextEncoding),
        (byte)Random.Shared.Next(1, 256), default);

async Task<int> TakeIssuedItemAsync(InGameSessionState taker, ushort npcEntityId)
{
    if (npcIssuedItems.Get(npcEntityId) is not { ItemId: not 0 } issued) return NpcIssueRules.NoIssuedItem;
    if (issued.Count == 0) return NpcIssueRules.NoneLeft;
    if (taker.Character is not { } character || itemCatalog.GetById((int)issued.ItemId) is not { } definition) return NpcIssueRules.NoIssuedItem;
    var bag = itemStore.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
    var bagWeight = bag.Sum(held => (itemCatalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits);
    if (bag.Count >= tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity
        || !tPoseidon.Core.Game.Monsters.PickupRules.Fits(taker.PlayerMaxWt, bagWeight, bag.Count, false, definition.Weight, 1))
    {
        return NpcIssueRules.NoRoom;
    }
    if (bag.Any(held => held.ItemIndex == (int)issued.ItemId)) return NpcIssueRules.AlreadyHeld;
    if (!npcIssuedItems.TryTakeOne(npcEntityId, issued.ItemId)) return NpcIssueRules.NoneLeft;
    var created = itemStore.GrantToCharacter(character.CharacterId, (int)issued.ItemId, definition.ObjectClass, SlotKind.Inventory, FreeBagPosition(bag), 1);
    await taker.SendFrameAsync(
        Opcode.S2C_ADD_TO_INVENTORY,
        GroundItemFrames.AddedToBag(tPoseidon.Core.Game.Monsters.PickupRules.ItemCreated, definition.ObjectClass, issued.ItemId, BagItemIds.OnWire(created.ItemUid), created.Stack, created.ItemSlot),
        (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
    Console.WriteLine($"{LogTag} [npc] 0x{character.CharacterId:X} takes issued item {issued.ItemId} from NPC {npcEntityId} ({issued.Count - 1} left)");
    return NpcIssueRules.Taken;
}

async Task RefreshGameMasterNpcsAsync(ushort mapId)
{
    foreach (var npc in NpcObjectsOn(mapId))
    {
        if (npcActivation.Set(npc.Object.EntityId, true)) await ShowNpcAsync(mapId, npc.Object, true).ConfigureAwait(false);
    }
}

gameMasters.World = new GameMasterWorldActions(
    SummonGameMasterMonstersAsync,
    RemoveGameMasterObjectsAsync,
    SetGameMasterNpcActiveAsync,
    RefreshGameMasterNpcsAsync,
    SetGameMasterIssueItem,
    OxGiveItemAsync,
    playerShops.CloseAsync,
    myCamps.ForceCloseAsync,
    SummonGameMasterNpcAsync);

List<SectorObject> SectorObjectsOn(ushort mapId, DateTimeOffset now)
{
    var objects = new List<SectorObject>();
    foreach (var (_, npc) in NpcObjectsOn(mapId))
    {
        if (npcActivation.IsActive(npc.EntityId)) objects.Add(npc);
    }
    foreach (var mob in monsterManager.GetByMap(mapId))
    {
        if (mob.IsDead) continue;
        var (mobX, mobY) = mob.GetInterpolatedPosition(now);
        objects.Add(new SectorObject(mob.EntityId, mobX, mobY, true, Opcode.NPC_SPAWN,
            () => MobSpawnFrames.Encode(mob, monsterCatalog.GetById(mob.MonsterParamId))));
    }
    foreach (var owner in liveSessions.Values.ToArray())
    {
        if (owner.Pet is not { } pet || pet.MapId != mapId) continue;
        var (petX, petY) = pet.PositionAt(now);
        objects.Add(new SectorObject(pet.EntityId, petX, petY, true, Opcode.NPC_SPAWN,
            () => PetFrames.Spawn(pet, owner.PlayerEntityId, petX, petY)));
    }
    foreach (var house in myCamps.HousesOn(mapId))
    {
        objects.Add(new SectorObject(house.HouseId, house.X, house.Y, false, Opcode.S2C_PLAYER_SPAWN_POST_WARP, () => house.Spawn));
    }
    foreach (var lying in groundItems.OnMap(mapId))
    {
        objects.Add(new SectorObject(lying.EntityId, lying.X, lying.Y, false, Opcode.S2C_ITEM_ON_GROUND,
            () => GroundItemFrames.AlreadyThere(lying)));
    }
    return objects;
}

async Task<bool> SendSectorFrameAsync(InGameSessionState viewer, Opcode opcode, byte[] payload)
{
    try
    {
        await viewer.SendFrameAsync(opcode, payload, (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
        return true;
    }
    catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
    {
        foreach (var gone in liveSessions.Where(entry => ReferenceEquals(entry.Value, viewer)).ToArray()) liveSessions.TryRemove(gone.Key, out _);
        return false;
    }
}

async Task UpdateSectorViewsAsync(ushort mapId, IReadOnlyList<InGameSessionState> viewers, DateTimeOffset now)
{
    if (SectorGridOf(mapId) is not { } grid || grid.IsSingleSector) return;

    var previousSectors = movingObjectSectors.GetValueOrDefault(mapId);
    var currentSectors = new Dictionary<ushort, (int Column, int Row)>();
    foreach (var mover in SectorObjectsOn(mapId, now).Where(candidate => candidate.Moves))
    {
        var sector = grid.SectorOf(mover.X, mover.Y);
        currentSectors[mover.EntityId] = sector;
        if (previousSectors is null || !previousSectors.TryGetValue(mover.EntityId, out var was) || was == sector) continue;
        foreach (var viewer in viewers)
        {
            if (viewer.SeenSector is not { } seen) continue;
            var sawBefore = tPoseidon.Core.Game.Monsters.SectorGrid.Sees(seen, was);
            var seesNow = tPoseidon.Core.Game.Monsters.SectorGrid.Sees(seen, sector);
            if (sawBefore == seesNow) continue;
            await SendSectorFrameAsync(viewer, seesNow ? mover.Opcode : Opcode.S2C_ENTITY_DESPAWN,
                seesNow ? mover.Payload() : DespawnOf(mover.EntityId)).ConfigureAwait(false);
        }
    }

    movingObjectSectors[mapId] = currentSectors;

    List<SectorObject>? objects = null;
    foreach (var player in viewers)
    {
        if (player.Character is null) continue;
        var (playerX, playerY) = PlayerPositionAt(player, now);
        var sector = grid.SectorOf(playerX, playerY);
        if (player.SeenSector is not { } seen)
        {
            player.SeenSector = sector;
            continue;
        }
        if (seen == sector) continue;
        var (leaving, entering) = grid.Change(seen, sector);
        player.SeenSector = sector;
        objects ??= SectorObjectsOn(mapId, now);

        foreach (var gone in leaving)
        {
            foreach (var other in viewers)
            {
                if (ReferenceEquals(other, player) || other.SeenSector != gone) continue;
                await SendSectorFrameAsync(other, Opcode.S2C_ENTITY_DESPAWN, DespawnOf(player.PlayerEntityId)).ConfigureAwait(false);
                await SendSectorFrameAsync(player, Opcode.S2C_ENTITY_DESPAWN, DespawnOf(other.PlayerEntityId)).ConfigureAwait(false);
            }
        }
        foreach (var gone in leaving)
        {
            foreach (var item in objects)
            {
                if (grid.SectorOf(item.X, item.Y) != gone) continue;
                await SendSectorFrameAsync(player, Opcode.S2C_ENTITY_DESPAWN, DespawnOf(item.EntityId)).ConfigureAwait(false);
            }
        }
        foreach (var seenNow in entering)
        {
            foreach (var other in viewers)
            {
                if (ReferenceEquals(other, player) || other.SeenSector != seenNow || other.Character is null) continue;
                if (!player.GameMasterInvisible)
                {
                    await SendSectorFrameAsync(other, Opcode.NPC_SPAWN, PlayerSpawnOf(player)).ConfigureAwait(false);
                    await InstantChatSignController.ShowToAsync(other, player).ConfigureAwait(false);
                }
                if (!other.GameMasterInvisible)
                {
                    await SendSectorFrameAsync(player, Opcode.NPC_SPAWN, PlayerSpawnOf(other)).ConfigureAwait(false);
                    await InstantChatSignController.ShowToAsync(player, other).ConfigureAwait(false);
                }
            }
        }
        foreach (var seenNow in entering)
        {
            foreach (var item in objects)
            {
                if (grid.SectorOf(item.X, item.Y) != seenNow) continue;
                await SendSectorFrameAsync(player, item.Opcode, item.Payload()).ConfigureAwait(false);
            }
        }
    }
}

async Task SendToViewersAsync(ushort mapId, int x, int y, Opcode opcode, byte[] payload)
{
    var shownAt = DateTimeOffset.UtcNow;
    foreach (var viewer in liveSessions.Values.ToArray())
    {
        if (viewer.MapId != mapId || !viewer.ClientReady) continue;
        var (viewerX, viewerY) = PlayerPositionAt(viewer, shownAt);
        if (!InViewOn(mapId, viewerX, viewerY, x, y)) continue;
        await SendSectorFrameAsync(viewer, opcode, payload).ConfigureAwait(false);
    }
}

async Task ShowPlayersAsync(InGameSessionState arriving, Opcode arrivalOpcode)
{
    if (arriving.Character is null) return;
    arriving.SeenSector = SectorGridOf(arriving.MapId)?.SectorOf(arriving.PlayerX, arriving.PlayerY);
    var arrivingSpawn = PlayerSpawnOf(arriving);
    foreach (var present in liveSessions.Values.ToArray())
    {
        if (ReferenceEquals(present, arriving) || present.MapId != arriving.MapId || !present.ClientReady || present.Character is null) continue;
        var (presentX, presentY) = PlayerPositionAt(present, DateTimeOffset.UtcNow);
        if (!InViewOn(arriving.MapId, arriving.PlayerX, arriving.PlayerY, presentX, presentY)) continue;
        if (!present.GameMasterInvisible)
        {
            await arriving.SendFrameAsync(Opcode.NPC_SPAWN, PlayerSpawnOf(present), (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
            await InstantChatSignController.ShowToAsync(arriving, present).ConfigureAwait(false);
        }
        if (!arriving.GameMasterInvisible)
        {
            await SendSectorFrameAsync(present, arrivalOpcode, arrivingSpawn).ConfigureAwait(false);
            await InstantChatSignController.ShowToAsync(present, arriving).ConfigureAwait(false);
        }
    }
}

async Task ShowGuardiansAsync(InGameSessionState viewer)
{
    if (viewer.Guardians is not { Awakened: true } shown) return;
    await viewer.SendFrameAsync(Opcode.S2C_GUARDIAN_LIST, GuardianFrames.List(shown.SlotMaxCount, shown.Guardians, clientTextEncoding), (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
    await viewer.SendFrameAsync(Opcode.S2C_GUARDIAN_RECORD, GuardianFrames.Record(shown.Equipped, clientTextEncoding), (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
}

async Task<bool> ReviveWithItemAsync(S2SessionContext reviverCtx, InGameSessionState reviverSt, ushort targetId, Item rebirthItem, CancellationToken ct)
{
    if (!rebirthItemsById.TryGetValue(rebirthItem.ItemIndex, out var rebirth)) return false;
    mapKindById.TryGetValue(reviverSt.MapId, out var reviverMap);
    if (!reviverMap.AllowsRebirthItem) return false;
    var target = liveSessions.FirstOrDefault(entry => entry.Value.MapId == reviverSt.MapId && entry.Value.PlayerEntityId == targetId);
    if (target.Value is not { Stats: { } targetStats, Character: { } targetChar } targetSt
        || !targetSt.IsDead || targetSt.ResurrectionCompletesAt != default)
    {
        await reviverCtx.SendFrameAsync(Opcode.S2C_RESURRECTION_REFUSED, Array.Empty<byte>(), randKey: NextReviveKey(), ct).ConfigureAwait(false);
        Console.WriteLine($"{LogTag} {reviverCtx.Remote} [combat] rebirth item {rebirthItem.ItemIndex} on 0x{targetId:X4} refused: not a dead player waiting to rise");
        return false;
    }
    targetStats.Resurrect(targetSt.PlayerMaxHp, targetSt.PlayerMaxMp, rebirth.HpRestoreRatio, rebirth.MpRestoreRatio, rebirth.ExpRestoreRatio);
    targetSt.ResurrectionCompletesAt = DateTimeOffset.UtcNow + PlayerRevivalFrames.CompletionDelay;
    Console.WriteLine($"{LogTag} {reviverCtx.Remote} [combat] player 0x{reviverSt.PlayerEntityId:X4} revives 0x{targetId:X4} with item {rebirthItem.ItemIndex}: HP {targetStats.CurrentHp}, MP {targetStats.CurrentMp}, exp {targetStats.Exp}");
    await target.Key.SendFrameAsync(Opcode.S2C_EXP_VALUE, PlayerDeathFrames.ExpValue(targetStats.Exp), randKey: NextReviveKey(), ct).ConfigureAwait(false);
    await target.Key.SendFrameAsync(Opcode.S2C_OP_011D_TBD,
        ProgressFrames.TmTotal((byte)StatGrowth.TypeIndexOf(targetChar.FType), targetStats.TmExp), randKey: NextReviveKey(), ct).ConfigureAwait(false);
    await SendToMapAsync(targetSt.MapId, Opcode.S2C_RESURRECTED, PlayerRevivalFrames.Resurrected(targetId, reviverSt.PlayerEntityId)).ConfigureAwait(false);
    return true;
}

byte NextReviveKey()
{
    lock (reviveKeys) return (byte)reviveKeys.Next(1, 256);
}

const string ScriptNoticeName = "Notice";

tPoseidon.Core.Game.Scripting.Library.ScriptLoadResult ReloadScripts()
{
    lock (scriptReloadGate)
    {
        var loaded = scriptEngine.Reload();
        var change = scriptEngine.SyncNpcs(worldEntities, idAllocator, scriptedNpcs);
        foreach (var removed in change.Removed) _ = SendToMapAsync(removed.MapId, Opcode.S2C_ENTITY_DESPAWN, DespawnOf(removed.EntityId));
        foreach (var added in change.Added) _ = SendToMapAsync(added.MapId, Opcode.NPC_SPAWN, EnterWorldBatchSynthesizer.NpcSpawn(added, characterInfoCatalog));
        if (change.Added.Count + change.Removed.Count > 0)
        {
            Console.WriteLine($"{LogTag} [scripts] script NPCs: {change.Added.Count} placed, {change.Removed.Count} removed");
        }
        return loaded;
    }
}

Task TellScriptLineAsync(InGameSessionState target, string text, CancellationToken ct) =>
    target.SendFrameAsync(
        (Opcode)tPoseidon.Core.Net.Social.ChatFrames.Said,
        tPoseidon.Core.Net.Social.ChatFrames.Line(target.PlayerEntityId, ScriptNoticeName, text, clientTextEncoding),
        (byte)Random.Shared.Next(1, 256),
        ct);

async Task AnnounceScriptNoticeAsync(string text, CancellationToken ct)
{
    foreach (var listener in liveSessions.Values.ToArray())
    {
        if (listener.Character is null || !listener.ClientReady) continue;
        try
        {
            await TellScriptLineAsync(listener, text, ct).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine($"{LogTag} [scripts] a notice could not be delivered: the session ended");
        }
        catch (System.IO.IOException ex)
        {
            Console.WriteLine($"{LogTag} [scripts] a notice could not be delivered: {ex.Message}");
        }
    }
}

void TriggerScriptEvent(string eventName)
{
    foreach (var target in liveSessions.Values.ToArray())
    {
        if (target.ClientReady && target.RunScriptEvent is { } run) _ = run(tPoseidon.Core.Game.Scripting.Lang.EventKind.Named, null, eventName);
    }
}

async Task SendToMapAsync(ushort mapId, Opcode opcode, byte[] payload)
{
    foreach (var watcher in liveSessions.ToArray())
    {
        if (watcher.Value.MapId != mapId || !watcher.Value.ClientReady) continue;
        try
        {
            await watcher.Value.SendFrameAsync(opcode, payload, (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            liveSessions.TryRemove(watcher.Key, out _);
        }
        catch (System.IO.IOException)
        {
            liveSessions.TryRemove(watcher.Key, out _);
        }
    }
}

IReadOnlyList<ItemProperty> StoredItemProperties(Item item) =>
    itemCatalog.GetById(item.ItemIndex) is { } storedDefinition
        ? itemPropertyRules.Full(storedDefinition, itemPropertyStore.ForItem(item.ItemUid))
        : itemPropertyStore.ForItem(item.ItemUid);

async Task SendToMapInBlockAsync(ushort mapId, Opcode opcode, byte[] payload, ushort blockId)
{
    foreach (var watcher in liveSessions.ToArray())
    {
        if (watcher.Value.MapId != mapId || !watcher.Value.ClientReady) continue;
        try
        {
            await watcher.Key.SendFrameAsync(opcode, payload, (byte)Random.Shared.Next(1, 256), blockId, default).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            liveSessions.TryRemove(watcher.Key, out _);
        }
        catch (System.IO.IOException)
        {
            liveSessions.TryRemove(watcher.Key, out _);
        }
    }
}

async Task<bool> SendFromLoopAsync(
    IEnumerable<KeyValuePair<S2SessionContext, InGameSessionState>> receivers,
    Opcode opcode,
    byte[] payload,
    ushort blockId,
    Random rng,
    CancellationToken ct)
{
    foreach (var receiver in receivers)
    {
        try
        {
            await receiver.Key.SendFrameAsync(opcode, payload, (byte)rng.Next(1, 256), blockId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            liveSessions.TryRemove(receiver.Key, out _);
        }
        catch (System.IO.IOException)
        {
            liveSessions.TryRemove(receiver.Key, out _);
        }
    }
    return true;
}

static ItemRechargeCatalog LoadItemRechargeCatalog(string dataRoot)
{
    var goods = Path.Combine(dataRoot, ItemRechargeCatalog.GoodsFile);
    var recharges = Path.Combine(dataRoot, ItemRechargeCatalog.RechargeFile);
    if (!File.Exists(goods) || !File.Exists(recharges))
    {
        Console.WriteLine($"{LogTag} item recharge: {ItemRechargeCatalog.GoodsFile} or {ItemRechargeCatalog.RechargeFile} missing; no item can be recharged");
        return ItemRechargeCatalog.Empty;
    }
    var catalog = ItemRechargeCatalog.Load(goods, recharges);
    Console.WriteLine($"{LogTag} item recharge: {catalog.ItemCount} rechargeable items ({ItemRechargeCatalog.RechargeFile})");
    return catalog;
}

static byte FreeBagPosition(IReadOnlyList<Item> bag) => tPoseidon.Core.Game.Items.EquipmentRules.FirstFreeBagPosition(bag);

async Task TickTimedItemsAsync(InGameSessionState holder)
{
    foreach (var timed in holder.TimedItems.Tick(DateTimeOffset.UtcNow))
    {
        var noticeKey = (byte)Random.Shared.Next(1, 256);
        await holder.SendFrameAsync(Opcode.S2C_TIMED_ITEM_NOTICE,
            tPoseidon.Core.Net.World.TimedItemFrames.Notice(timed.Item.ItemUid, timed.MinutesLeft), noticeKey, default).ConfigureAwait(false);
        if (!timed.Expired) continue;
        var expiredKey = (byte)Random.Shared.Next(1, 256);
        await holder.SendFrameAsync(Opcode.S2C_TIMED_ITEM_EXPIRED,
            tPoseidon.Core.Net.World.TimedItemFrames.Expired(timed.Item.ItemUid), expiredKey, default).ConfigureAwait(false);
        Console.WriteLine($"{LogTag} [items] 0x{holder.PlayerEntityId:X4} timed item {timed.Item.ItemIndex} (0x{timed.Item.ItemUid:X}) expired");
        if (holder.ReleaseExpiredItem is { } release) await release(timed.Item.ItemUid).ConfigureAwait(false);
    }
}

async Task ReleaseMonsterOwnerAsync(ushort mapId, ushort monsterEntityId, ushort ownerEntityId)
{
    if (monsterManager.Get(monsterEntityId) is not { } owned || !owned.ReleaseOwner(ownerEntityId)) return;
    Console.WriteLine($"{LogTag} [combat] monster 0x{monsterEntityId:X4} is no longer owned by 0x{ownerEntityId:X4}");
    await SendToMapAsync(mapId, Opcode.S2C_OP_016E_TBD, ProgressFrames.OwnerReleased(monsterEntityId)).ConfigureAwait(false);
}

var npcLoopCt = stopSignal.Token;
var monsterEffectsTickedAt = new Dictionary<ushort, DateTimeOffset>();
var npcLoopTask = Task.Run(async () =>
{
    var interval = WorldPass.Interval;
    var npcLoopRng = new Random();
    ushort swingBlockId = MeleeSwingFrames.NoBlock;
    var corpseRemovalDelay = TimeSpan.FromMilliseconds(2000);
    var regenPassesPerTick = (int)(interval.TotalMilliseconds / RegenPassGapMs);
    var regenFillReported = false;
    var corpses = new List<(ushort EntityId, ushort MapId, DateTimeOffset RemoveAt)>();

    async Task<bool> SendStrikeFramesAsync(
        IReadOnlyList<SwingFrame> frames,
        ushort attackerEntityId,
        ushort targetEntityId,
        ushort ownerEntityId,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions)
    {
        foreach (var session in sessions)
        {
            try
            {
                foreach (var (opcode, payload, blockId, audience) in frames)
                {
                    var addressed = audience switch
                    {
                        SwingAudience.Attacker => session.Value.PlayerEntityId == attackerEntityId,
                        SwingAudience.Owner => session.Value.PlayerEntityId == ownerEntityId,
                        SwingAudience.Engaged => session.Value.Engagements.Contains(targetEntityId),
                        _ => true,
                    };
                    if (!addressed) continue;
                    var rk = (byte)npcLoopRng.Next(1, 256);
                    await session.Key.SendFrameAsync(opcode, payload, rk, blockId, npcLoopCt).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                liveSessions.TryRemove(session.Key, out _);
            }
            catch (System.IO.IOException)
            {
                liveSessions.TryRemove(session.Key, out _);
            }
        }
        return true;
    }

    async Task<bool> AreaSkillAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        AreaShape shape,
        MonsterEntity? selected,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if (caster.Stats is not { } areaStats) return true;
        AreaCandidate CandidateOf(MonsterEntity monster)
        {
            var (x, y) = monster.GetInterpolatedPosition(now);
            return new AreaCandidate(monster.EntityId, x, y, monster.IsDead, monsterCatalog.GetById(monster.MonsterParamId)?.Type ?? 0, monster.OwnerEntityId);
        }
        var mapMonsters = monsterManager.GetByMap(mapId).ToList();
        var selectedCandidate = selected is null ? (AreaCandidate?)null : CandidateOf(selected);
        var victims = SkillAreaShapes.Collect(
            shape, action, caster.PlayerEntityId, (caster.PlayerX, caster.PlayerY), selectedCandidate, mapMonsters.Select(CandidateOf));
        var targetId = selected?.EntityId ?? (ushort)0;
        var radius = action.Amount(SkillAreaShapes.RadiusColumn) ?? 0;
        var centre = shape.Centre == AreaCentre.Target && selectedCandidate is { } centreTarget
            ? (centreTarget.X, centreTarget.Y)
            : (caster.PlayerX, caster.PlayerY);
        var attacker = new SkillAttacker(
            caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, areaStats.Level,
            caster.PlayerDa, areaStats.CurrentHp, caster.PlayerMaxHp,
            caster.PlayerHv, caster.PlayerDx, caster.PlayerDp, DarkBonus: DarkBonusOf(caster), Weight: CarriedWeightOf(caster),
            ElementBonus: shape.ElementSlot >= 0 ? SkillHitShapes.DefaultElementStep * ElementTotalOf(caster, shape.ElementSlot) : 0);

        var blockId = commandBlockIds.Next();
        var riddenHits = new List<(MonsterEntity Victim, SkillHit Hit)>();
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaStart(caster.PlayerEntityId, castSkill, blockId, SkillFrames.AreaTailBytes(shape.Tail, targetId, victims, radius)),
            caster.PlayerEntityId, targetId, selected?.OwnerEntityId ?? MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }

        foreach (var victimId in victims)
        {
            if (monsterManager.Get(victimId) is not { IsDead: false } victim || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition) continue;
            var (victimX, victimY) = victim.GetInterpolatedPosition(now);
            SkillHit hit;
            lock (npcLoopRng)
            {
                hit = SkillAreaShapes.Hit(
                    shape, action, attacker,
                    new SkillTarget(
                        victimDefinition.DpLv + victim.StatDelta(MonsterStat.Dp),
                        victimDefinition.HvLv + victim.StatDelta(MonsterStat.Hv),
                        victimDefinition.MdLv + victim.StatDelta(MonsterStat.Md),
                        victimDefinition.LkLv + victim.StatDelta(MonsterStat.Lk),
                        victim.CurrentHp, victim.Level, SkillActions.ResistOf(victimDefinition, shape.Element) - victim.ResistLoss(shape.Element),
                        victim.MaxHp, victimDefinition.DpLv),
                    npcLoopRng);
            }
            if (shape.Falloff)
            {
                hit = hit with { Damage = SkillAreaRules.WithFalloff(hit.Damage, SkillAreaRules.Distance(victimX, victimY, centre.Item1, centre.Item2), radius) };
            }
            var hpBefore = victim.CurrentHp;
            victim.AddAttacker(caster.PlayerEntityId);
            victim.ApplyDamage(hit.Damage);
            var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
                AttackerEntityId: caster.PlayerEntityId,
                TargetEntityId: victim.EntityId,
                AttackerX: caster.PlayerX,
                AttackerY: caster.PlayerY,
                TargetX: victimX,
                TargetY: victimY,
                Damage: hit.Damage,
                MobHpAfter: victim.CurrentHp,
                HitFlags: 0,
                HitKind: (uint)hit.Kind,
                Killed: victim.IsDead,
                NextSwingAtUtc: now,
                MonsterParamId: victim.MonsterParamId,
                MobMaxHp: victim.MaxHp,
                MobHpBefore: hpBefore);
            if (strike.Killed && caster.CurrentTargetEntityId == victim.EntityId) caster.CurrentTargetEntityId = 0;
            var (additions, ownerEntityId) = await ResolveStrikeAsync(
                strike, caster, mapId, sessions, zoneGeometry, now, battleEvents,
                SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} hits 0x{victim.EntityId:X4}: kind={hit.Kind} damage={hit.Damage} hp={victim.CurrentHp}/{victim.MaxHp}");
            if (!await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaVictim(strike, hit, blockId, additions),
                strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false))
            {
                return false;
            }
            riddenHits.Add((victim, hit));
        }

        var tmFrames = caster.GainSkillTmExp is { } gainAreaTm && shape.Tm != AreaTm.None
            ? gainAreaTm(shape.Tm == AreaTm.Flat
                ? castSkill.Row.SkillExp
                : SkillActions.TmExpForUse(castSkill.Row.SkillExp, areaStats.Level, selected?.Level ?? areaStats.Level))
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} (action {castSkill.ActionId}) reaches {victims.Count} monster(s)");
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, tmFrames),
            caster.PlayerEntityId, targetId, selected?.OwnerEntityId ?? MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }
        foreach (var (riddenVictim, riddenHit) in riddenHits)
        {
            if (!await RideAsync(session, sessions, castSkill, action, riddenHit, riddenVictim, now).ConfigureAwait(false)) return false;
        }
        if (castSkill.ActionId == SkillRiders.ElectricSword
            && !await ElectricSwordStunAsync(session, sessions, castSkill, action, victims, now).ConfigureAwait(false))
        {
            return false;
        }
        if (shape.RestartsOnFirst && victims.Count > 0 && monsterManager.Get(victims[0]) is { IsDead: false } first)
        {
            return await StartAttackAfterSkillAsync(session, sessions, first.EntityId, SkillActions.AttackStartDelayMs, now).ConfigureAwait(false);
        }
        return true;
    }

    async Task<bool> CureAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        KeyValuePair<S2SessionContext, InGameSessionState>? curedPlayer,
        MonsterEntity? curedMonster)
    {
        var caster = session.Value;
        if (caster.Stats is not { } cureStats) return true;
        var ratio = action.Value(SkillHitShapes.RatioColumn) ?? 0f;
        ushort targetId;
        (ushort X, ushort Y) targetAt;
        int targetLevel;
        bool healed;
        if (curedPlayer is { } player && player.Value.Stats is { } curedStats)
        {
            var (raised, didHeal) = SkillActions.Heal(curedStats.CurrentHp, player.Value.PlayerMaxHp, ratio);
            curedStats.SetVitals(raised, curedStats.CurrentMp);
            healed = didHeal;
            targetId = player.Value.PlayerEntityId;
            targetAt = (player.Value.PlayerX, player.Value.PlayerY);
            targetLevel = curedStats.Level;
            if (!await SendFromLoopAsync(
                new[] { player }, Opcode.S2C_HEALTH_STATUS,
                tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(curedStats.CurrentHp).EncodePayload(),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
        }
        else if (curedMonster is not null)
        {
            var (raised, didHeal) = SkillActions.Heal(Math.Max(0L, curedMonster.CurrentHp), Math.Max(0L, curedMonster.MaxHp), ratio);
            curedMonster.RaiseHp((int)raised);
            healed = didHeal;
            targetId = curedMonster.EntityId;
            targetAt = curedMonster.GetInterpolatedPosition(DateTimeOffset.UtcNow);
            targetLevel = curedMonster.Level;
        }
        else
        {
            return true;
        }

        var tmFrames = healed && caster.GainSkillTmExp is { } gainCureTm
            ? gainCureTm(SkillActions.TmExpForUse(castSkill.Row.SkillExp, cureStats.Level, targetLevel))
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();
        var blockId = commandBlockIds.Next();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} cures 0x{targetId:X4} (ratio {ratio}, healed {healed})");
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeHeal(caster.PlayerEntityId, (caster.PlayerX, caster.PlayerY), targetId, targetAt, healed, castSkill, blockId, tmFrames),
            caster.PlayerEntityId, targetId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> TornadoBombAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity target,
        MonsterParamEntry targetDefinition,
        SkillCast cast,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if (caster.Stats is not { } tornadoStats) return true;
        TornadoOutcome outcome;
        lock (npcLoopRng)
        {
            outcome = SkillActions.ResolveTornado(
                action,
                new SkillAttacker(
                    caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, tornadoStats.Level,
                    caster.PlayerDa, tornadoStats.CurrentHp, caster.PlayerMaxHp, DarkBonus: DarkBonusOf(caster)),
                new SkillTarget(
                    targetDefinition.DpLv + target.StatDelta(MonsterStat.Dp),
                    targetDefinition.HvLv + target.StatDelta(MonsterStat.Hv),
                    targetDefinition.MdLv + target.StatDelta(MonsterStat.Md),
                    targetDefinition.LkLv + target.StatDelta(MonsterStat.Lk),
                    target.CurrentHp, target.Level, SkillActions.ResistOf(targetDefinition, SkillActions.NoElement) - target.ResistLoss(SkillActions.NoElement)),
                npcLoopRng);
        }
        var hpBefore = target.CurrentHp;
        target.AddAttacker(caster.PlayerEntityId);
        target.ApplyDamage(outcome.Sum);
        var (targetX, targetY) = target.GetInterpolatedPosition(now);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: caster.PlayerEntityId,
            TargetEntityId: target.EntityId,
            AttackerX: caster.PlayerX,
            AttackerY: caster.PlayerY,
            TargetX: targetX,
            TargetY: targetY,
            Damage: outcome.Sum,
            MobHpAfter: target.CurrentHp,
            HitFlags: 0,
            HitKind: SkillDamage.KindHit,
            Killed: target.IsDead,
            NextSwingAtUtc: now,
            MonsterParamId: target.MonsterParamId,
            MobMaxHp: target.MaxHp,
            MobHpBefore: hpBefore);
        if (strike.Killed) caster.CurrentTargetEntityId = 0;
        var (additions, ownerEntityId) = await ResolveStrikeAsync(
            strike, caster, mapId, sessions, zoneGeometry, now, battleEvents,
            SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
        var tmFrames = caster.GainSkillTmExp is { } gainTornadoTm
            ? gainTornadoTm(castSkill.Row.SkillExp)
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} Tornado Bomb -> 0x{target.EntityId:X4}: {string.Join("/", outcome.Slots.Select(slot => slot.Damage))} = {outcome.Sum}, landed {outcome.Landed}, hp {target.CurrentHp}/{target.MaxHp}");
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeTornado(strike, castSkill, outcome, commandBlockIds.Next(), additions, tmFrames),
            strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (cast.ResumeAttackOn != 0)
        {
            return await StartAttackAfterSkillAsync(session, sessions, cast.ResumeAttackOn, SkillActions.AttackResumeDelayMs, now).ConfigureAwait(false);
        }
        return strike.Killed
            || await StartAttackAfterSkillAsync(session, sessions, target.EntityId, SkillActions.AttackStartDelayMs, now).ConfigureAwait(false);
    }

    async Task<bool> ChainHitsAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        ChainShape shape,
        MonsterEntity target,
        SkillCast cast,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if (caster.Stats is not { } chainStats) return true;
        var attacker = new SkillAttacker(
            caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, chainStats.Level,
            caster.PlayerDa, chainStats.CurrentHp, caster.PlayerMaxHp,
            caster.PlayerHv, caster.PlayerDx, caster.PlayerDp, DarkBonus: DarkBonusOf(caster));
        AreaCandidate CandidateOf(MonsterEntity monster)
        {
            var (x, y) = monster.GetInterpolatedPosition(now);
            return new AreaCandidate(monster.EntityId, x, y, monster.IsDead, monsterCatalog.GetById(monster.MonsterParamId)?.Type ?? 0, monster.OwnerEntityId);
        }
        var (targetX, targetY) = target.GetInterpolatedPosition(now);
        IReadOnlyList<ushort> victimIds = shape.Tail == ChainTail.VictimsWithSlots
            ? SkillAreaShapes.Collect(
                ChainHits.BlazingArea, action, caster.PlayerEntityId, (caster.PlayerX, caster.PlayerY), CandidateOf(target),
                monsterManager.GetByMap(mapId).Select(CandidateOf))
            : new[] { target.EntityId };
        var resolved = new List<(MonsterEntity Monster, ChainVictim Victim)>();
        foreach (var victimId in victimIds)
        {
            if (monsterManager.Get(victimId) is not { IsDead: false } victim || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition) continue;
            IReadOnlyList<SkillHit> hits;
            lock (npcLoopRng)
            {
                hits = ChainHits.Resolve(
                    castSkill.ActionId, action, attacker, MonsterTarget(victim, victimDefinition, SkillActions.NoElement), caster.Skills, npcLoopRng);
            }
            resolved.Add((victim, new ChainVictim(victim.EntityId, hits)));
        }
        if (resolved.Count == 0) return true;

        var struck = new List<(tPoseidon.Core.Game.Combat.PlayerStrike Strike, ChainVictim Victim, SwingAdditions Additions, ushort OwnerEntityId)>();
        foreach (var (victim, chain) in resolved)
        {
            var hpBefore = victim.CurrentHp;
            victim.AddAttacker(caster.PlayerEntityId);
            victim.ApplyDamage(chain.Sum);
            var (victimX, victimY) = victim.GetInterpolatedPosition(now);
            var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
                AttackerEntityId: caster.PlayerEntityId,
                TargetEntityId: victim.EntityId,
                AttackerX: caster.PlayerX,
                AttackerY: caster.PlayerY,
                TargetX: victimX,
                TargetY: victimY,
                Damage: chain.Sum,
                MobHpAfter: victim.CurrentHp,
                HitFlags: 0,
                HitKind: SkillDamage.KindHit,
                Killed: victim.IsDead,
                NextSwingAtUtc: now,
                MonsterParamId: victim.MonsterParamId,
                MobMaxHp: victim.MaxHp,
                MobHpBefore: hpBefore);
            if (strike.Killed && caster.CurrentTargetEntityId == victim.EntityId) caster.CurrentTargetEntityId = 0;
            var (additions, ownerEntityId) = await ResolveStrikeAsync(
                strike, caster, mapId, sessions, zoneGeometry, now, battleEvents,
                SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
            struck.Add((strike, chain, additions, ownerEntityId));
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} (action {castSkill.ActionId}) -> 0x{victim.EntityId:X4}: {string.Join("/", chain.Hits.Select(hit => $"{hit.Damage}k{hit.Kind}"))} = {chain.Sum}, hp {victim.CurrentHp}/{victim.MaxHp}");
        }

        var tail = SkillFrames.ChainTailBytes(
            shape.Tail, target.EntityId, (caster.PlayerX, caster.PlayerY), (targetX, targetY), resolved.Select(entry => entry.Victim).ToList());
        var blockId = commandBlockIds.Next();
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeChainStart(
                caster.PlayerEntityId, target.EntityId, castSkill, shape, tail,
                struck.Where(entry => entry.Additions.OwnerTaken is not null).Select(entry => entry.Additions.OwnerTaken!).ToList(), blockId),
            caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }
        foreach (var (strike, chain, additions, ownerEntityId) in struck)
        {
            if (!await SendStrikeFramesAsync(
                SkillFrames.EncodeChainVictim(strike, chain, blockId, additions),
                strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false))
            {
                return false;
            }
        }
        var tmFrames = caster.GainSkillTmExp is { } gainChainTm
            ? gainChainTm(shape.ScaledTm ? SkillActions.TmExpForUse(castSkill.Row.SkillExp, chainStats.Level, target.Level) : castSkill.Row.SkillExp)
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, tmFrames),
            caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }

        if (castSkill.ActionId == ChainHits.FistFullOfGalders)
        {
            chainStats.GrantMoney(-ChainHits.GalderCost(ChainHits.GalderHits(action)));
            if (!await SendFromLoopAsync(
                new[] { session }, Opcode.S2C_MONEY, GroundItemFrames.Money(chainStats.Money),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
        }

        if (!shape.Restarts) return true;
        if (cast.ResumeAttackOn != 0)
        {
            return await StartAttackAfterSkillAsync(session, sessions, cast.ResumeAttackOn, SkillActions.AttackResumeDelayMs, now).ConfigureAwait(false);
        }
        return target.IsDead
            || await StartAttackAfterSkillAsync(session, sessions, target.EntityId, SkillActions.AttackStartDelayMs, now).ConfigureAwait(false);
    }

    async Task<bool> PolarBearHitsAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity target,
        MonsterParamEntry targetDefinition,
        SkillCast cast,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if (caster.Stats is not { } bearStats) return true;
        PolarBearOutcome outcome;
        lock (npcLoopRng)
        {
            outcome = PolarBearHits.Resolve(
                castSkill.ActionId,
                action,
                new SkillAttacker(
                    caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, bearStats.Level,
                    caster.PlayerDa, bearStats.CurrentHp, caster.PlayerMaxHp,
                    caster.PlayerHv, caster.PlayerDx, DarkBonus: DarkBonusOf(caster)),
                new SkillTarget(
                    targetDefinition.DpLv + target.StatDelta(MonsterStat.Dp),
                    targetDefinition.HvLv + target.StatDelta(MonsterStat.Hv),
                    targetDefinition.MdLv + target.StatDelta(MonsterStat.Md),
                    targetDefinition.LkLv + target.StatDelta(MonsterStat.Lk),
                    target.CurrentHp, target.Level, SkillActions.ResistOf(targetDefinition, SkillActions.NoElement) - target.ResistLoss(SkillActions.NoElement),
                    target.MaxHp, targetDefinition.DpLv),
                npcLoopRng);
        }
        var hpBefore = target.CurrentHp;
        target.AddAttacker(caster.PlayerEntityId);
        target.ApplyDamage(outcome.Sum);
        var (targetX, targetY) = target.GetInterpolatedPosition(now);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: caster.PlayerEntityId,
            TargetEntityId: target.EntityId,
            AttackerX: caster.PlayerX,
            AttackerY: caster.PlayerY,
            TargetX: targetX,
            TargetY: targetY,
            Damage: outcome.Sum,
            MobHpAfter: target.CurrentHp,
            HitFlags: 0,
            HitKind: SkillDamage.KindHit,
            Killed: target.IsDead,
            NextSwingAtUtc: now,
            MonsterParamId: target.MonsterParamId,
            MobMaxHp: target.MaxHp,
            MobHpBefore: hpBefore);
        if (strike.Killed) caster.CurrentTargetEntityId = 0;
        var (additions, ownerEntityId) = await ResolveStrikeAsync(
            strike, caster, mapId, sessions, zoneGeometry, now, battleEvents,
            SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: true).ConfigureAwait(false);
        var tmFrames = caster.GainSkillTmExp is { } gainBearTm
            ? gainBearTm(SkillActions.TmExpForUse(castSkill.Row.SkillExp, bearStats.Level, target.Level))
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} (action {castSkill.ActionId}) -> 0x{target.EntityId:X4}: {string.Join("/", outcome.Hits.Select(hit => $"{hit.Damage}k{hit.Kind}"))} = {outcome.Sum}, hp {target.CurrentHp}/{target.MaxHp}");
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodePolarBearHits(strike, castSkill, outcome, commandBlockIds.Next(), additions, tmFrames),
            strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (cast.ResumeAttackOn != 0)
        {
            return await StartAttackAfterSkillAsync(session, sessions, cast.ResumeAttackOn, SkillActions.AttackResumeDelayMs, now).ConfigureAwait(false);
        }
        return strike.Killed
            || await StartAttackAfterSkillAsync(session, sessions, target.EntityId, SkillActions.AttackStartDelayMs, now).ConfigureAwait(false);
    }

    async Task<bool> ManaRingAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity selected,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if (caster.Stats is not { } ringStats) return true;
        var boosterId = SkillActions.EnhancerOf(castSkill.ActionId);
        var booster = caster.Skills.TryGetValue(boosterId, out var boosterLevel) ? skillBook.Find(boosterId)?.ActionAt(boosterLevel) : null;
        var (power, coefficient, radius) = SkillAreaRules.Ring(action, booster);
        var (centreX, centreY) = selected.GetInterpolatedPosition(now);
        var attacker = new SkillAttacker(
            caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, ringStats.Level,
            caster.PlayerDa, ringStats.CurrentHp, caster.PlayerMaxHp, DarkBonus: DarkBonusOf(caster));

        var blockId = commandBlockIds.Next();
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaStart(caster.PlayerEntityId, selected.EntityId, castSkill, blockId),
            caster.PlayerEntityId, selected.EntityId, selected.OwnerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }

        var victims = 0;
        foreach (var victim in monsterManager.GetByMap(mapId))
        {
            if (victims >= SkillAreaRules.MostVictims) break;
            if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition || !SkillActions.MayTarget(victimDefinition)) continue;
            var (victimX, victimY) = victim.GetInterpolatedPosition(now);
            var distance = SkillAreaRules.Distance(victimX, victimY, centreX, centreY);
            if (!SkillAreaRules.Reaches(distance, radius)) continue;
            victims++;

            SkillHit hit;
            lock (npcLoopRng)
            {
                hit = SkillDamage.Magic(
                    attacker, power,
                    new SkillTarget(
                        victimDefinition.DpLv + victim.StatDelta(MonsterStat.Dp),
                        victimDefinition.HvLv + victim.StatDelta(MonsterStat.Hv),
                        victimDefinition.MdLv + victim.StatDelta(MonsterStat.Md),
                        victimDefinition.LkLv + victim.StatDelta(MonsterStat.Lk),
                        victim.CurrentHp, victim.Level, SkillActions.ResistOf(victimDefinition, SkillActions.NoPropertyElement) - victim.ResistLoss(SkillActions.NoPropertyElement)),
                    coefficient, 0, SkillActions.NoPropertyElement, npcLoopRng);
            }
            hit = hit with { Damage = SkillAreaRules.WithFalloff(hit.Damage, distance, radius) };
            var hpBefore = victim.CurrentHp;
            victim.AddAttacker(caster.PlayerEntityId);
            victim.ApplyDamage(hit.Damage);
            var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
                AttackerEntityId: caster.PlayerEntityId,
                TargetEntityId: victim.EntityId,
                AttackerX: caster.PlayerX,
                AttackerY: caster.PlayerY,
                TargetX: victimX,
                TargetY: victimY,
                Damage: hit.Damage,
                MobHpAfter: victim.CurrentHp,
                HitFlags: 0,
                HitKind: (uint)hit.Kind,
                Killed: victim.IsDead,
                NextSwingAtUtc: now,
                MonsterParamId: victim.MonsterParamId,
                MobMaxHp: victim.MaxHp,
                MobHpBefore: hpBefore);
            if (strike.Killed && caster.CurrentTargetEntityId == victim.EntityId) caster.CurrentTargetEntityId = 0;
            var (additions, ownerEntityId) = await ResolveStrikeAsync(
                strike, caster, mapId, sessions, zoneGeometry, now, battleEvents,
                SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} Mana Ring hits 0x{victim.EntityId:X4} at {distance:F1}/{radius}: kind={hit.Kind} damage={hit.Damage} hp={victim.CurrentHp}/{victim.MaxHp}");
            if (!await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaVictim(strike, hit, blockId, additions),
                strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false))
            {
                return false;
            }
        }

        var tmFrames = caster.GainSkillTmExp is { } gainRingTm
            ? gainRingTm(SkillActions.TmExpForUse(castSkill.Row.SkillExp, ringStats.Level, selected.Level))
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, tmFrames),
            caster.PlayerEntityId, selected.EntityId, selected.OwnerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<int?> PayForCastAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        SkillDefinition castSkill,
        int castLevel,
        tPoseidon.Core.LibConfig.SkillTableRow? castConditions,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        var payer = session.Value;
        if (payer.Stats is not { } payerStats) return 0;
        var payerOnly = new List<KeyValuePair<S2SessionContext, InGameSessionState>> { session };
        if (!await SendFromLoopAsync(
            payerOnly, Opcode.S2C_OP_00F0_TBD, SkillFrames.Completed((ushort)castSkill.Id, (byte)castLevel),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
        {
            return null;
        }

        var (hpCost, mpCost) = SkillChecks.Costs(castConditions, payerStats.CurrentHp, payerStats.CurrentMp);
        if (mpCost > 0 && payer.Effects.Snapshot().FirstOrDefault(held => held.IsApplied && held.Kind.LowersManaCost) is { } cheered)
        {
            var cheaper = (int)Math.Truncate(mpCost * (double)(cheered.Amount / (float)tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.CheerUpPercent));
            if (payerStats.CurrentMp >= cheaper) mpCost = cheaper;
        }
        if (hpCost > 0 || mpCost > 0)
        {
            payerStats.SetVitals(payerStats.CurrentHp - hpCost, payerStats.CurrentMp - mpCost);
            if (hpCost > 0 && !await SendFromLoopAsync(
                payerOnly, Opcode.S2C_HEALTH_STATUS,
                tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(payerStats.CurrentHp).EncodePayload(),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return null;
            }
            if (mpCost > 0 && !await SendFromLoopAsync(
                payerOnly, Opcode.S2C_MANA_STATUS,
                tPoseidon.Core.Net.Packets.ManaStatusPacket.Of(payerStats.CurrentMp).EncodePayload(),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return null;
            }
        }
        payer.Cooldowns.Start(castSkill.Id, castConditions?.Value(SkillDefinition.CoolTimeColumn) ?? 0f);
        if (payer.ScrollCast is { } paidScroll && paidScroll.SkillId == castSkill.Id)
        {
            payer.ScrollCast = null;
            var scrollsLeft = itemStore.AdjustStack(paidScroll.ItemUid, -1);
            Console.WriteLine($"{LogTag} [skill] 0x{payer.PlayerEntityId:X4} scroll for skill {castSkill.Id} used, {scrollsLeft} left");
            if (!await SendFromLoopAsync(
                payerOnly, Opcode.S2C_ITEM_USED, PlayerRevivalFrames.ItemUsed(BagItemIds.OnWire(paidScroll.ItemUid)),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return null;
            }
        }
        if (!await FillCrazyGaugeAsync(session, sessions, CrazyHammer.SkillPointColumn, mapId, now).ConfigureAwait(false)) return null;
        return mpCost;
    }

    async Task<bool> SendEffectChangesAsync(
        KeyValuePair<S2SessionContext, InGameSessionState>? ownerSession,
        MonsterEntity? ownerMonster,
        IReadOnlyCollection<int> stats,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions)
    {
        if (ownerMonster is not null)
        {
            ApplyMonsterEffectDeltas(ownerMonster);
            return true;
        }
        if (ownerSession is not { } owner) return true;
        var deltas = ApplyEffectDeltas(owner.Value);
        var changed = stats.Where(stat => stat != tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.NoStat).Distinct().Select(stat => (stat, deltas[stat])).ToList();
        if (changed.Count == 0) return true;
        return await SendFromLoopAsync(
            new[] { owner }, Opcode.S2C_OP_011B_TBD, SkillEffectFrames.StatsChanged(changed),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
    }

    async Task<bool> EndSkillEffectAsync(
        tPoseidon.Core.Game.Skills.Effects.SkillEffect ended,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions)
    {
        idAllocator.Release(ended.ObjectId);
        if (ended.Kind.SilentEnd) return true;
        return await SendFromLoopAsync(
            sessions, Opcode.S2C_SKILL_EFFECT_ENDED, SkillEffectFrames.Ended(ended.OwnerEntityId, ended.ObjectId),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
    }

    async Task<bool> StartNpcSkillAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        int skillId,
        DateTimeOffset now)
    {
        if (skillBook.Find(skillId) is not { } skill
            || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(skill.ActionId) is not { OnTarget: false } kind
            || skill.ActionAt(tPoseidon.Core.Game.Skills.SkillDefinition.LowestLevel) is not { } action)
        {
            Console.WriteLine($"{LogTag} [npc] 0x{session.Value.PlayerEntityId:X4} NPC skill {skillId}: no skill effect is built for it");
            return true;
        }
        var started = await StartSkillEffectAsync(session, sessions, skill, kind, action, null, now).ConfigureAwait(false);
        Console.WriteLine($"{LogTag} [npc] 0x{session.Value.PlayerEntityId:X4} NPC skill {skillId} (action {skill.ActionId}) started: {started}");
        return started;
    }

    async Task<bool> StartSkillEffectAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKind kind,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity? targetMonster,
        DateTimeOffset now,
        ushort castByEntityId = 0,
        int bestPartyDefence = 0,
        (ushort X, ushort Y)? groundPoint = null,
        long? magnitude = null,
        int? durationMs = null)
    {
        var caster = session.Value;
        if (kind.RequiresWornSlot is { } wornSlot && !WearsSlot(caster, wornSlot))
        {
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} needs an item worn in equipment slot {wornSlot}");
            return true;
        }
        if (kind.RequiresEffect != 0 && !caster.Effects.Snapshot().Any(held => held.IsApplied && held.Kind.ActionId == kind.RequiresEffect))
        {
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} needs effect {kind.RequiresEffect} first");
            return true;
        }
        if (kind.Instant != tPoseidon.Core.Game.Skills.Effects.SkillInstant.None)
        {
            return await InstantSkillAsync(session, sessions, castSkill, kind, action, now).ConfigureAwait(false);
        }
        if (kind.MonsterSpread != 0 && kind.SpreadOnly) return await SpreadToMonstersAsync(session, sessions, castSkill, kind, action, now).ConfigureAwait(false);
        if (kind.SpreadsAs != 0 && tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(kind.SpreadsAs) is { } memberKind)
        {
            var partyIds = caster.Character is { } spreadingCharacter && systemLink.MirrorOf(spreadingCharacter.CharacterId) is { } spreadingParty
                ? spreadingParty.Others.Select(member => member.CharacterId).ToHashSet()
                : new HashSet<ulong>();
            var spreadRadius = kind.SpreadRadiusColumn is { } spreadRadiusColumn ? action.Amount(spreadRadiusColumn) ?? 0 : int.MaxValue;
            var spreadTo = sessions
                .Where(entry => entry.Value != caster
                    && entry.Value.Character is { } memberCharacter && partyIds.Contains(memberCharacter.CharacterId) && !entry.Value.IsDead
                    && SkillAreaRules.Distance(caster.PlayerX, caster.PlayerY, entry.Value.PlayerX, entry.Value.PlayerY) <= spreadRadius)
                .Where(_ => kind.PartySpreadColumn is not { } partySpreadColumn || action.Amount(partySpreadColumn) == ScrollSkills.PartyScroll)
                .Take(kind.SpreadLimit)
                .Prepend(session)
                .ToList();
            var bestDefence = spreadTo.Max(member => member.Value.CombatBase.Dp);
            if (kind.AnnouncesSpread)
            {
                var spreadBlock = commandBlockIds.Next();
                if (!await SendStrikeFramesAsync(
                    [.. SkillFrames.EncodeAreaStart(caster.PlayerEntityId, castSkill, spreadBlock, Array.Empty<byte>()),
                     .. SkillFrames.EncodeAreaEnd(spreadBlock, FlatTm(caster, castSkill))],
                    caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
                {
                    return false;
                }
            }
            foreach (var member in spreadTo)
            {
                if (!await StartSkillEffectAsync(member, sessions, castSkill, memberKind, action, null, now, caster.PlayerEntityId, bestDefence).ConfigureAwait(false)) return false;
            }
            return !kind.TmOnSpread || await SendStrikeFramesAsync(
                FlatTm(caster, castSkill).Select(tmFrame => new SwingFrame(tmFrame.Opcode, tmFrame.Payload, MeleeSwingFrames.NoBlock, SwingAudience.Attacker)).ToList(),
                caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
        }
        if (targetMonster is not null)
        {
            var (resistSent, resisted) = await DebuffResistedAsync(castSkill, targetMonster, sessions).ConfigureAwait(false);
            if (!resistSent) return false;
            if (resisted) return true;
        }
        var ownerSession = kind.OnTarget ? (KeyValuePair<S2SessionContext, InGameSessionState>?)null : session;
        var ownerEntityId = targetMonster?.EntityId ?? caster.PlayerEntityId;
        var book = targetMonster is not null ? MonsterEffectsOf(targetMonster.EntityId) : caster.Effects;
        var resist = targetMonster is not null && monsterCatalog.GetById(targetMonster.MonsterParamId) is { } resistingDefinition
            ? tPoseidon.Core.Game.Skills.Effects.SkillResist.Of(
                monsterProtections.GetValueOrDefault(resistingDefinition.ProtectTable.Trim()), castSkill.Id)
            : 0;
        var mode = tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied;
        if (kind.RollsChance)
        {
            lock (npcLoopRng)
            {
                mode = tPoseidon.Core.Game.Skills.Effects.SkillEffectBook.RollMode(action.Value(kind.ChanceColumn) ?? 0f, resist, npcLoopRng);
            }
            if (mode != tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied
                && kind.Tail != tPoseidon.Core.Game.Skills.Effects.SkillEffectTail.FlagThenDuration)
            {
                Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} on 0x{ownerEntityId:X4}: {mode}, no effect");
                return true;
            }
        }
        else if (kind.CarriesMode && !kind.SkipsRoll)
        {
            lock (npcLoopRng)
            {
                mode = tPoseidon.Core.Game.Skills.Effects.SkillEffectBook.RollMode(action.Value(kind.ChanceColumn) ?? 0f, resist, npcLoopRng);
            }
        }
        SkillHit? openingHit = null;
        if (kind.StrikesOnStart && targetMonster is not null
            && monsterCatalog.GetById(targetMonster.MonsterParamId) is { } struckDefinition && caster.Stats is { } strikerStats)
        {
            SkillHit rolledHit;
            lock (npcLoopRng)
            {
                rolledHit = SkillDamage.Magic(
                    new SkillAttacker(caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, strikerStats.Level, DarkBonus: DarkBonusOf(caster)),
                    kind.StrikeAddsElement
                        ? ObjectHits.ElementPower(ElementTotalOf(caster, ElementSlots.AttackSlotOf(kind.StrikeElement)), action.Amount(kind.StrikePowerColumn ?? kind.AmountColumn) ?? 0)
                        : action.Amount(kind.StrikePowerColumn ?? kind.AmountColumn) ?? 0,
                    MonsterTarget(targetMonster, struckDefinition, kind.StrikeElement),
                    kind.FixedCoefficient ?? action.Value(kind.ChanceColumn) ?? 0f,
                    kind.StrikeLossLessLuck ? kind.StrikeMagicLoss - caster.PlayerLk : kind.StrikeMagicLoss, kind.StrikeElement, npcLoopRng);
            }
            openingHit = rolledHit;
            if (kind.StrikeGatesMode && rolledHit.Damage == 0 || (rolledHit.Kind & SkillDamage.KindMask) is SkillDamage.KindMiss
                    or tPoseidon.Core.Game.Skills.Effects.IncomingHits.KindGuarded or tPoseidon.Core.Game.Skills.Effects.IncomingHits.KindAbsorbed)
            {
                mode = tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Failed;
            }
        }
        if (castSkill.ActionId == SkillAreaShapes.HammerWheel
            && caster.Skills.TryGetValue(SkillAreaShapes.RampageWheelSkill, out var rampageLevel)
            && skillBook.Find(SkillAreaShapes.RampageWheelSkill)?.ActionAt(rampageLevel) is { } rampageAction)
        {
            action = rampageAction;
        }
        var effect = new tPoseidon.Core.Game.Skills.Effects.SkillEffect
        {
            ObjectId = idAllocator.NewId(),
            Action = action,
            TickMs = kind.TickSecondsColumn is { } tickSecondsColumn ? (int)((action.Value(tickSecondsColumn) ?? 0f) * 1000.0)
                : kind.TickColumn is { } tickColumn ? (action.Amount(tickColumn) ?? 0) * kind.TickScale : kind.PeriodMs,
            SecondAmount = kind.SecondAmountColumn is { } secondColumn ? action.Value(secondColumn) ?? 0f : 0f,
            Threshold = kind.ThresholdColumn is { } thresholdColumn ? (action.Amount(thresholdColumn) ?? 0) / 100.0 : 0.0,
            Point = groundPoint,
            Magnitude = magnitude ?? (kind.Tail is tPoseidon.Core.Game.Skills.Effects.SkillEffectTail.ModeRadiusPoint or tPoseidon.Core.Game.Skills.Effects.SkillEffectTail.RadiusOnly
                ? action.Amount(kind.AmountColumn) ?? 0
                : kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.BuffCancel
                    ? (int)((caster.PlayerAp + 16 * caster.PlayerHv) * (double)(action.Value(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.RatioColumn) ?? 0f) * 3.0 / 4.0)
                : kind.HealColumn is { } healColumn ? action.Amount(healColumn) ?? 0
                : kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.HealOverTime
                ? (int)Math.Truncate(caster.PlayerMaxHp * (double)(action.Value(kind.AmountColumn) ?? 0f))
                : kind.AbsorbsHits ? ShieldPool(caster, action, kind) : 0),
            Kind = kind,
            SkillId = castSkill.Id,
            EsClass = kind.HeaderClass ?? (sbyte)castSkill.Row.EsClass,
            CasterEntityId = castByEntityId != 0 ? castByEntityId : caster.PlayerEntityId,
            OwnerEntityId = ownerEntityId,
            Amount = kind.Rule == tPoseidon.Core.Game.Skills.Effects.SkillEffectRule.BestPartyDefence
                ? tPoseidon.Core.Game.Skills.Effects.SkillEffectBook.ProjectedDefence(
                    bestPartyDefence,
                    action.Value(castByEntityId == 0 || castByEntityId == caster.PlayerEntityId ? kind.AmountColumn : kind.MemberAmountColumn ?? kind.AmountColumn) ?? 0f)
                : action.Value(kind.AmountColumn) ?? 0f,
            Charges = kind.ChargesColumn is { } chargesColumn ? action.Amount(chargesColumn) ?? 0 : kind.AbsorbsHits ? ShieldPool(caster, action, kind) : 0,
            Slow = kind.SlowColumn is { } slowColumn ? (action.Value(slowColumn) ?? 0f) * kind.SlowScale : 0f,
            Flat = kind.FlatColumn is { } flatColumn ? action.Value(flatColumn) ?? 0f : 0f,
            DurationMs = durationMs ?? (kind.FixedDurationMs ?? (int)Math.Clamp((long)(action.Amount(kind.DurationColumn) ?? 0) * kind.MillisecondsPerUnit, 0, int.MaxValue))
                + kind.HoldDelayMs + kind.TrailingMs,
            ThirdAmount = kind.ThirdAmountColumn is { } thirdColumn ? action.Value(thirdColumn) ?? 0f : 0f,
            TailStrings = kind.Tail == tPoseidon.Core.Game.Skills.Effects.SkillEffectTail.ModeThenStrings ? ScrollTailStrings(action) : Array.Empty<byte[]>(),
            TailFloat = kind.TailFloatColumn is { } tailFloatColumn
                ? kind.TailFloatInverted && action.Value(tailFloatColumn) is { } inverted && inverted != 0f ? 1f / inverted : action.Value(tailFloatColumn) ?? 0f
                : 0f,
            Mode = mode,
        };
        if (book.Register(effect) is not { } registration)
        {
            idAllocator.Release(effect.ObjectId);
            Console.WriteLine($"{LogTag} [skill] 0x{ownerEntityId:X4} has no room for skill {castSkill.Id} (class {effect.EsClass})");
            return true;
        }
        foreach (var (removed, reason) in registration.Removed)
        {
            if (reason == tPoseidon.Core.Game.Skills.Effects.SkillEffectRemoval.Evicted && ownerSession is { } evictedOwner
                && !await SendFromLoopAsync(
                    new[] { evictedOwner }, Opcode.S2C_SKILL_NOTICE, SkillEffectFrames.Evicted(removed.SkillId),
                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
            if (!await EndSkillEffectAsync(removed, sessions).ConfigureAwait(false)
                || (removed.IsApplied && !await SendEffectChangesAsync(ownerSession, targetMonster, removed.Kind.Stats, sessions).ConfigureAwait(false))
                || (ownerSession is { } evictedHolder && !await AfterPlayerEffectEndsAsync(evictedHolder, removed, sessions).ConfigureAwait(false)))
            {
                return false;
            }
        }

        if (targetMonster is not null && effect.IsApplied && effect.Slow > 0f) SlowMonster(targetMonster);
        if (targetMonster is not null && effect.IsApplied && kind.BreaksBarrier
            && monsterCatalog.GetById(targetMonster.MonsterParamId) is { } barrierDefinition)
        {
            effect.ResistLoss = GunSkills.BarrierBreak(
                barrierDefinition.DaLv + targetMonster.StatDelta(MonsterStat.Da), action.Value(GunSkills.RatioColumn) ?? 0f);
            targetMonster.AddResistLoss(SkillActions.NoElement, effect.ResistLoss);
            targetMonster.AddResistLoss(SkillActions.GunElement, effect.ResistLoss);
            if (!(await RemoveEffectsAsync(null, targetMonster, kind.StripsActions, sessions).ConfigureAwait(false)).Sent) return false;
            if (GunGearOf(caster).Bullet is { } barrierBullet) SpendBullets(barrierBullet, 1);
        }
        if (targetMonster is not null && effect.IsApplied && kind.LowersResistElement != 0
            && monsterCatalog.GetById(targetMonster.MonsterParamId) is { } loweredDefinition)
        {
            effect.ResistLoss = kind.ResistDropByOwnerMagic
                ? ObjectEffects.IceResistDrop(loweredDefinition.MaLv + targetMonster.StatDelta(MonsterStat.Ma), action.Value(kind.AmountColumn) ?? 0f)
                : tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.ResistDrop(
                    caster.PlayerMa, action.Value(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.RegiDownColumn) ?? 0f,
                    SkillActions.ResistOf(loweredDefinition, kind.LowersResistElement));
            targetMonster.AddResistLoss(kind.LowersResistElement, effect.ResistLoss);
        }
        if (kind.CleansesFromRow > 0 && ownerSession is { } cleansedOwner)
        {
            var cleansed = SupportSkills.CleansedBy(action, kind.CleansesFromRow).Where(cleansedId => cleansedId != kind.ActionId).ToList();
            if (!(await RemoveEffectsAsync(cleansedOwner, null, cleansed, sessions).ConfigureAwait(false)).Sent) return false;
        }
        if (kind.Holds && kind.HoldDelayMs == 0 && effect.IsApplied && targetMonster is not null)
        {
            var (heldX, heldY) = targetMonster.HoldUntil(now.AddMilliseconds(effect.DurationMs), now);
            if (!await SendFromLoopAsync(
                    sessions, Opcode.S2C_OP_0019_TBD,
                    new Packet0019Tbd { EntityId = targetMonster.EntityId, WorldX = heldX, WorldY = heldY, StateFlag = targetMonster.MoveType }.EncodePayload(),
                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
        }

        var effectBlock = commandBlockIds.Next();
        effect.BlockId = effectBlock;
        if (!await SendFromLoopAsync(
                sessions, Opcode.S2C_OP_00E0_TBD, SkillEffectFrames.Started(effect, targetMonster?.EntityId ?? ownerEntityId),
                effectBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)
            || !await SendFromLoopAsync(
                sessions, Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), effectBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
        {
            return false;
        }
        if (openingHit is { } prisonHit && effect.IsApplied && targetMonster is not null
            && !await OpeningStrikeAsync(caster, targetMonster, prisonHit, sessions, now).ConfigureAwait(false))
        {
            return false;
        }
        if (kind.MonsterSpread != 0 && effect.IsApplied
            && !await SpreadToMonstersAsync(session, sessions, castSkill, kind, action, now).ConfigureAwait(false))
        {
            return false;
        }

        if (targetMonster is not null)
        {
            if (caster.Engagements.Refresh(targetMonster.EntityId, now) is { } crowdedOut)
            {
                await ReleaseMonsterOwnerAsync(targetMonster.MapId, crowdedOut, caster.PlayerEntityId).ConfigureAwait(false);
            }
            targetMonster.AddAttacker(caster.PlayerEntityId);
            if (targetMonster.OwnerEntityId == MonsterEntity.NoOwner)
            {
                targetMonster.TakeOwner(caster.PlayerEntityId, targetMonster.CurrentHp);
                if (!await SendFromLoopAsync(
                    sessions, Opcode.S2C_OP_016D_TBD, ProgressFrames.OwnerTaken(targetMonster.EntityId, caster.PlayerEntityId),
                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }

        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} (action {kind.ActionId}) on 0x{ownerEntityId:X4}: object 0x{effect.ObjectId:X4} mode {effect.Mode}, amount {effect.Amount}, {effect.DurationMs} ms");
        if (!effect.IsApplied) return true;
        if (kind.HidesOwner && ownerSession is { } hiddenOwner
            && !await SendVisibilityAsync(hiddenOwner.Value, UtilitySkills.HiddenMask, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (kind.ElementBoostSlots.Count > 0 && ownerSession is { } boostedOwner)
        {
            effect.ResistDeltas = kind.ElementBoostSlots
                .Select(slot => ((byte)slot, ElementSlots.Boost(ElementTotalOf(boostedOwner.Value, slot), effect.Amount)))
                .Where(entry => entry.Item2 != 0)
                .ToList();
            if (!await ChangeResistsAsync(boostedOwner, effect.ResistDeltas).ConfigureAwait(false)) return false;
        }
        if (kind.ScrollStats && ownerSession is { } scrolledOwner && scrolledOwner.Value.Stats is { } scrolledStats)
        {
            effect.FixedDeltas = ScrollSkills.StatDeltas(action, scrolledStats.Level);
            ApplyEffectDeltas(scrolledOwner.Value);
            if (!await SendEffectChangesAsync(scrolledOwner, null, effect.FixedDeltas.Select(entry => entry.Stat).ToList(), sessions).ConfigureAwait(false)) return false;
        }
        if (kind.GmStats && ownerSession is { } cheeredOwner)
        {
            effect.FixedDeltas = EventSkills.GmDeltas(action, EventSkills.IsGmPercent(castSkill.Id), EffectBasesOf(cheeredOwner.Value));
            ApplyEffectDeltas(cheeredOwner.Value);
            if (!await SendEffectChangesAsync(cheeredOwner, null, EventSkills.GmStats.ToList(), sessions).ConfigureAwait(false)) return false;
        }
        if (kind.Morph != MorphRule.None && ownerSession is { } morphOwner
            && !await StartMorphAsync(morphOwner, effect, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (kind.ResistKind != 0 && ownerSession is { } resistOwner
            && !await ChangeResistAsync(resistOwner, kind.ResistKind, action.Amount(kind.ResistColumn) ?? 0).ConfigureAwait(false))
        {
            return false;
        }
        if (!kind.SilentStart && !await SendEffectChangesAsync(ownerSession, targetMonster, kind.Stats, sessions).ConfigureAwait(false)) return false;

        if (kind.GrantsTm && (castByEntityId == 0 || castByEntityId == caster.PlayerEntityId) && caster.GainSkillTmExp is { } gainEffectTm && caster.Stats is { } effectCasterStats)
        {
            var effectTm = kind.OnTarget && targetMonster is not null
                ? SkillActions.TmExpForUse(castSkill.Row.SkillExp, effectCasterStats.Level, targetMonster.Level)
                : castSkill.Row.SkillExp;
            foreach (var (tmOpcode, tmPayload) in gainEffectTm(effectTm))
            {
                if (!await SendFromLoopAsync(
                    new[] { session }, tmOpcode, tmPayload, MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }
        return true;
    }

    async Task<bool> TickSkillEffectsAsync(
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        foreach (var session in sessions)
        {
            var owner = session.Value;
            var sinceOwnerTick = owner.EffectsTickedAt == default ? TimeSpan.Zero : now - owner.EffectsTickedAt;
            owner.EffectsTickedAt = now;
            foreach (var effect in owner.Effects.Snapshot())
            {
                if (effect.Kind.IsAuraChild
                    && (now - effect.RefreshedAt).TotalMilliseconds >= tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.AuraChildLifeMs)
                {
                    effect.EndNow();
                }
                var tick = effect.Tick((int)sinceOwnerTick.TotalMilliseconds);
                for (var due = effect.TakeDueTicks(); due > 0 && !effect.EndRequested; due--)
                {
                    if (!await AreaAroundOwnerTickAsync(session, effect, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
                    if (!await PlayerPeriodicAsync(session, effect, sessions, mapId, now).ConfigureAwait(false)) return false;
                    if (!await HitObjectTickAsync(session, effect, sessions, mapId, now).ConfigureAwait(false)) return false;
                    if (!await MonsterPeriodicOnPlayerAsync(session, effect, sessions, mapId).ConfigureAwait(false)) return false;
                }
                if (effect.EndRequested) tick = tPoseidon.Core.Game.Skills.Effects.SkillEffectTick.Ended;
                if (tick == tPoseidon.Core.Game.Skills.Effects.SkillEffectTick.Warning)
                {
                    if (!await SendFromLoopAsync(
                        new[] { session }, Opcode.S2C_SKILL_EFFECT_ENDING, SkillEffectFrames.Ending(effect.Kind.ActionId),
                        MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                    {
                        return false;
                    }
                }
                else if (tick == tPoseidon.Core.Game.Skills.Effects.SkillEffectTick.Ended && owner.Effects.Remove(effect))
                {
                    if (!await EndSkillEffectAsync(effect, sessions).ConfigureAwait(false)
                        || (effect.IsApplied && !await SendEffectChangesAsync(session, null, effect.Kind.Stats, sessions).ConfigureAwait(false))
                        || !await AfterPlayerEffectEndsAsync(session, effect, sessions).ConfigureAwait(false))
                    {
                        return false;
                    }
                }
            }
        }

        var since = monsterEffectsTickedAt.TryGetValue(mapId, out var mapTickedAt) ? now - mapTickedAt : TimeSpan.Zero;
        monsterEffectsTickedAt[mapId] = now;
        foreach (var (monsterId, book) in monsterEffects.ToArray())
        {
            if (monsterManager.Get(monsterId) is not { } monster)
            {
                DropMonsterEffects(monsterId);
                continue;
            }
            if (monster.MapId != mapId) continue;
            foreach (var effect in book.Snapshot())
            {
                var monsterTick = effect.Tick((int)since.TotalMilliseconds);
                for (var due = effect.TakeDueTicks(); due > 0 && !monster.IsDead; due--)
                {
                    if (!await DamageOverTimeTickAsync(effect, monster, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
                    if (!await MonsterPeriodicAsync(effect, monster, sessions, now).ConfigureAwait(false)) return false;
                    if (!await DelayedStrikeAsync(effect, monster, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
                }
                if (monsterTick != tPoseidon.Core.Game.Skills.Effects.SkillEffectTick.Ended || !book.Remove(effect)) continue;
                if (!await EndSkillEffectAsync(effect, sessions).ConfigureAwait(false)) return false;
                UndoMonsterEffect(monster, effect);
            }
        }
        return true;
    }

    async Task<bool> PlayerPeriodicAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect effect,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        var owner = session.Value;
        if (owner.IsDead || owner.Stats is not { } periodicStats) return true;
        var ownerOnly = new List<KeyValuePair<S2SessionContext, InGameSessionState>> { session };
        switch (effect.Kind.Periodic)
        {
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.Berserk:
            {
                effect.PeriodCount++;
                var radius = effect.Action?.Amount(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.RadiusColumn) ?? 0;
                if (owner.CurrentTargetEntityId == 0 && radius > 0)
                {
                    var partyIds = owner.Character is { } berserker && systemLink.MirrorOf(berserker.CharacterId) is { } berserkParty
                        ? berserkParty.Others.Select(member => member.CharacterId).ToHashSet()
                        : new HashSet<ulong>();
                    var partyEntityIds = sessions
                        .Where(entry => entry.Value.Character is { } member && partyIds.Contains(member.CharacterId))
                        .Select(entry => entry.Value.PlayerEntityId)
                        .ToHashSet();
                    var engaged = monsterManager.GetByMap(mapId).FirstOrDefault(monster =>
                    {
                        if (monster.IsDead || monsterCatalog.GetById(monster.MonsterParamId) is not { } engagedDefinition || !SkillActions.MayTarget(engagedDefinition)) return false;
                        if (monster.OwnerEntityId != MonsterEntity.NoOwner && monster.OwnerEntityId != owner.PlayerEntityId && !partyEntityIds.Contains(monster.OwnerEntityId)) return false;
                        var (monsterX, monsterY) = monster.GetInterpolatedPosition(now);
                        return SkillAreaRules.Distance(owner.PlayerX, owner.PlayerY, monsterX, monsterY) <= radius;
                    });
                    if (engaged is not null
                        && !await StartAttackAfterSkillAsync(session, sessions, engaged.EntityId, 0, now).ConfigureAwait(false))
                    {
                        return false;
                    }
                }
                if (effect.PeriodCount % tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.BerserkDrainEvery != 0) return true;
                var hpDrop = effect.Action?.Amount(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.HpDropColumn) ?? 0;
                var mpDrop = effect.Action?.Amount(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.MpDropColumn) ?? 0;
                var drainedHp = Math.Min((long)periodicStats.CurrentHp, (long)periodicStats.CurrentHp - hpDrop);
                var drainedMp = Math.Min((long)periodicStats.CurrentMp, (long)periodicStats.CurrentMp - mpDrop);
                if (drainedHp <= hpDrop || drainedMp <= mpDrop)
                {
                    effect.EndNow();
                    return true;
                }
                periodicStats.SetVitals((uint)drainedHp, (uint)drainedMp);
                return await SendFromLoopAsync(
                        ownerOnly, Opcode.S2C_HEALTH_STATUS,
                        tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(periodicStats.CurrentHp).EncodePayload(),
                        MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)
                    && await SendFromLoopAsync(
                        ownerOnly, Opcode.S2C_MANA_STATUS,
                        tPoseidon.Core.Net.Packets.ManaStatusPacket.Of(periodicStats.CurrentMp).EncodePayload(),
                        MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
            }
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.LowLifeCheck:
            {
                if (owner.PlayerMaxHp == 0) return true;
                effect.LifeRatio = periodicStats.CurrentHp / (double)owner.PlayerMaxHp;
                if (effect.Threshold < effect.LifeRatio)
                {
                    if (!effect.Active) return true;
                    effect.Active = false;
                }
                else
                {
                    effect.Active = true;
                }
                return await SendEffectChangesAsync(session, null, effect.Kind.Stats, sessions).ConfigureAwait(false);
            }
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.ShieldRegen:
            {
                if (effect.Charges < effect.Magnitude)
                {
                    effect.Charges = Math.Min(
                        effect.Magnitude,
                        effect.Charges + (int)(effect.Magnitude * (double)tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.ShieldRegenShare));
                }
                return await SendFromLoopAsync(
                    sessions, Opcode.S2C_SHIELD_GAUGE, SkillEffectFrames.ShieldGauge(effect.ObjectId, effect.Charges, effect.Magnitude),
                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
            }
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.AuraScan:
                return await AuraScanAsync(session, effect, sessions, now).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.FieldScan:
                return await FieldScanAsync(session, effect, sessions, mapId, now).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.CoupleHeart:
                return await CoupleHeartStepAsync(session, effect, sessions, now).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.HealOverTime:
            {
                if (periodicStats.CurrentHp < owner.PlayerMaxHp)
                {
                    periodicStats.SetVitals(
                        Math.Min(owner.PlayerMaxHp, periodicStats.CurrentHp + effect.Magnitude), periodicStats.CurrentMp);
                }
                return await SendFromLoopAsync(
                    ownerOnly, Opcode.S2C_HEALTH_STATUS,
                    tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(periodicStats.CurrentHp).EncodePayload(),
                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
            }
            default:
                return true;
        }
    }

    async Task<bool> AuraScanAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect parent,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        DateTimeOffset now)
    {
        var owner = session.Value;
        if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(parent.Kind.AuraChild) is not { } childKind || parent.Action is not { } auraRow) return true;
        var radius = auraRow.Amount(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.RadiusColumn) ?? 0;
        var partyIds = owner.Character is { } auraCharacter && systemLink.MirrorOf(auraCharacter.CharacterId) is { } auraParty
            ? auraParty.Others.Select(member => member.CharacterId).ToHashSet()
            : new HashSet<ulong>();
        var members = sessions
            .Where(entry => entry.Value == owner || (entry.Value.Character is { } memberCharacter && partyIds.Contains(memberCharacter.CharacterId)))
            .Where(entry => !(parent.Kind.AuraSkipsCaster && entry.Value == owner))
            .Where(entry => SkillAreaRules.Distance(owner.PlayerX, owner.PlayerY, entry.Value.PlayerX, entry.Value.PlayerY) <= radius)
            .Take(parent.Kind.AuraLimit)
            .ToList();
        var bolstered = parent.Kind.AuraBolsterSkill != 0 && owner.Skills.GetValueOrDefault(parent.Kind.AuraBolsterSkill) > 0 ? members.Count : 0;
        foreach (var member in members)
        {
            if (!await StartOrRefreshAuraChildAsync(member, owner, parent, childKind, auraRow, radius, bolstered, sessions, now).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> StartOrRefreshAuraChildAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> member,
        InGameSessionState caster,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect parent,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKind childKind,
        tPoseidon.Core.LibConfig.SkillTableRow auraRow,
        int radius,
        int bolstered,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        DateTimeOffset now)
    {
        var holder = member.Value;
        var extra = childKind.ActionId == tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.BigShieldOnMember
            ? tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.BigShieldBonus(bolstered)
            : 0f;
        if (holder.Effects.Snapshot().FirstOrDefault(held => held.Kind.ActionId == childKind.ActionId) is { } existing)
        {
            existing.Refresh(parent.RemainingMs);
            if (existing.CasterEntityId != caster.PlayerEntityId) return true;
            existing.RefreshedAt = now;
            if (existing.Extra == extra) return true;
            existing.Extra = extra;
            return await SendEffectChangesAsync(member, null, childKind.Stats, sessions).ConfigureAwait(false);
        }
        var child = new tPoseidon.Core.Game.Skills.Effects.SkillEffect
        {
            ObjectId = idAllocator.NewId(),
            Kind = childKind,
            SkillId = parent.SkillId,
            EsClass = 0,
            CasterEntityId = caster.PlayerEntityId,
            OwnerEntityId = holder.PlayerEntityId,
            Amount = auraRow.Value(childKind.AmountColumn) ?? 0f,
            Extra = extra,
            DurationMs = parent.RemainingMs,
            Mode = tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied,
            Action = auraRow,
            RefreshedAt = now,
            TailFloat = childKind.Tail == tPoseidon.Core.Game.Skills.Effects.SkillEffectTail.RadiusPercent
                ? tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.CheerUpTail(radius)
                : 0f,
        };
        if (holder.Effects.Register(child) is not { } registration)
        {
            idAllocator.Release(child.ObjectId);
            return true;
        }
        foreach (var (removed, reason) in registration.Removed)
        {
            if (reason == tPoseidon.Core.Game.Skills.Effects.SkillEffectRemoval.Evicted
                && !await SendFromLoopAsync(
                    new[] { member }, Opcode.S2C_SKILL_NOTICE, SkillEffectFrames.Evicted(removed.SkillId),
                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
            if (!await EndSkillEffectAsync(removed, sessions).ConfigureAwait(false)
                || (removed.IsApplied && !await SendEffectChangesAsync(member, null, removed.Kind.Stats, sessions).ConfigureAwait(false))
                || !await AfterPlayerEffectEndsAsync(member, removed, sessions).ConfigureAwait(false))
            {
                return false;
            }
        }
        var childBlock = commandBlockIds.Next();
        if (!await SendFromLoopAsync(
                sessions, Opcode.S2C_OP_00E0_TBD, SkillEffectFrames.Started(child, holder.PlayerEntityId),
                childBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)
            || !await SendFromLoopAsync(
                sessions, Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), childBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
        {
            return false;
        }
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} aura {parent.Kind.ActionId} reaches 0x{holder.PlayerEntityId:X4}: object 0x{child.ObjectId:X4}, amount {child.Amount}+{child.Extra}");
        return await SendEffectChangesAsync(member, null, childKind.Stats, sessions).ConfigureAwait(false);
    }

    async Task<(tPoseidon.Core.Game.Combat.MonsterBattleSwung Blow, bool Sent)> InterceptBlowAsync(
        tPoseidon.Core.Game.Combat.MonsterBattleSwung blow,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        if (sessions.FirstOrDefault(entry => entry.Value.PlayerEntityId == blow.TargetEntityId).Value is not { } guarded) return (blow, true);
        var interception = tPoseidon.Core.Game.Skills.Effects.IncomingHits.Intercept(
            new tPoseidon.Core.Game.Skills.Effects.IncomingHit(
                blow.Damage, tPoseidon.Core.Game.Skills.Effects.IncomingHits.BasicAttackType,
                blow.Result & tPoseidon.Core.Game.Combat.MeleeResolution.ResultKindMask, SkillActions.NoElement, 0),
            guarded.Effects.Snapshot(),
            new tPoseidon.Core.Game.Skills.Effects.HitDefender(guarded.PlayerEntityId, guarded.PlayerMa, guarded.PlayerAc, guarded.PlayerLk, guarded.PlayerHv),
            casterId => liveSessions.Values.FirstOrDefault(casterState => casterState.PlayerEntityId == casterId)?.PlayerHv ?? 0);
        var hit = interception.Hit;
        var fatal = hit.Damage > 0 && guarded.Stats is { } guardedStats && hit.Damage >= guardedStats.CurrentHp;
        var intercepted = hit.Damage == blow.Damage && hit.Kind == (blow.Result & tPoseidon.Core.Game.Combat.MeleeResolution.ResultKindMask)
            ? blow
            : blow with
            {
                Damage = hit.Damage,
                Result = (byte)(hit.Kind | (fatal ? tPoseidon.Core.Game.Combat.MeleeResolution.ResultFinishingFlag : 0)),
                Fatal = fatal,
                GuardLeft = hit.Extra,
            };
        if (!ReferenceEquals(intercepted, blow))
        {
            Console.WriteLine($"{LogTag} [skill] 0x{blow.TargetEntityId:X4} intercepts a hit from 0x{blow.EntityId:X4}: {blow.Damage} -> {hit.Damage} kind {hit.Kind}");
        }
        foreach (var shield in interception.ShieldsTouched)
        {
            if (!await SendFromLoopAsync(
                sessions, Opcode.S2C_SHIELD_GAUGE, SkillEffectFrames.ShieldGauge(shield.ObjectId, shield.Charges, shield.Magnitude),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return (intercepted, false);
            }
        }
        if (monsterManager.Get(blow.EntityId) is not { IsDead: false } sender || monsterCatalog.GetById(sender.MonsterParamId) is not { } senderDefinition)
        {
            return (intercepted, true);
        }
        foreach (var reflection in interception.Reflections)
        {
            var resist = SkillActions.ResistOf(senderDefinition, reflection.Element) - sender.ResistLoss(reflection.Element);
            var reflected = resist > 100 ? 0 : reflection.Damage - WireLimits.Saturate(reflection.Damage * resist * 0.01);
            var reflectedHit = new SkillHit(reflected, reflection.Type, reflection.Kind, reflection.Element, 0, (int)reflection.Flags);
            if (!await PeriodicStrikeAsync(guarded, sender, reflectedHit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false))
            {
                return (intercepted, false);
            }
            if (sender.IsDead) return (intercepted, true);
        }
        foreach (var counter in interception.Counters)
        {
            if (guarded.Stats is not { } counterStats) break;
            SkillHit counterHit;
            lock (npcLoopRng)
            {
                counterHit = SkillDamage.Magic(
                    new SkillAttacker(guarded.PlayerAp, guarded.PlayerAc, guarded.PlayerMa, guarded.PlayerLk, counterStats.Level),
                    counter.Power, MonsterTarget(sender, senderDefinition, counter.Element), counter.Coefficient, counter.MagicLoss, counter.Element,
                    npcLoopRng);
            }
            counterHit = counterHit with
            {
                Type = tPoseidon.Core.Game.Skills.Effects.IncomingHits.BasicAttackType,
                Element = SkillActions.NoElement,
                Kind = counterHit.Damage > 0 ? tPoseidon.Core.Game.Skills.Effects.IncomingHits.KindCounter : counterHit.Kind,
                Flags = counterHit.Flags | (int)tPoseidon.Core.Game.Skills.Effects.IncomingHits.FlagCounter,
            };
            if (!await PeriodicStrikeAsync(guarded, sender, counterHit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false))
            {
                return (intercepted, false);
            }
            if (sender.IsDead) break;
        }
        return (intercepted, true);
    }

    async Task<bool> ChangeResistAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> owner,
        byte resistKind,
        int delta) => await ChangeResistsAsync(owner, new[] { (resistKind, delta) }).ConfigureAwait(false);

    async Task<bool> ChangeResistsAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> owner,
        IReadOnlyList<(byte Kind, int Delta)> changes)
    {
        if (changes.Count == 0) return true;
        var resists = owner.Value.ResistBonus;
        var records = new List<(byte Kind, short Value)>(changes.Count);
        foreach (var (resistKind, delta) in changes)
        {
            var bonus = (short)Math.Clamp(resists.GetValueOrDefault(resistKind) + delta, short.MinValue, short.MaxValue);
            resists[resistKind] = bonus;
            records.Add((resistKind, (short)Math.Clamp(ElementTotalOf(owner.Value, resistKind), short.MinValue, short.MaxValue)));
        }
        return await SendFromLoopAsync(
            new[] { owner }, Opcode.S2C_RESIST_CHANGED, SkillEffectFrames.ResistChanged(records),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
    }

    int EquipmentElementOf(InGameSessionState player, int slot)
    {
        if (slot < 0 || player.Character is not { } wearer) return 0;
        var propertyId = tPoseidon.Core.Game.Items.ItemPropertyRules.AttributeId(slot);
        var sum = 0;
        foreach (var worn in tPoseidon.Core.Game.Items.EquipmentRules.WornBy(itemStore, itemCatalog, wearer.CharacterId))
        {
            var value = itemPropertyRules.StatSum(worn.Definition, itemPropertyStore.ForItem(worn.Item.ItemUid), propertyId);
            if (value != ItemStatCheckEntry.NoRequirement) sum += value;
        }
        return sum;
    }

    int ElementTotalOf(InGameSessionState player, int slot) =>
        slot < 0 ? 0 : ElementSlots.Total(slot, EquipmentElementOf(player, slot), player.ResistBonus.GetValueOrDefault((byte)slot));

    async Task<bool> AfterPlayerEffectEndsAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> owner,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect ended,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions)
    {
        if (!ended.IsApplied) return true;
        if (ended.Kind.HidesOwner && !await SendVisibilityAsync(owner.Value, UtilitySkills.VisibleMask, sessions).ConfigureAwait(false)) return false;
        if (ended.Kind.Morph != MorphRule.None && !await EndMorphAsync(owner, ended).ConfigureAwait(false)) return false;
        if (ended.ResistDeltas.Count > 0)
        {
            var undone = ended.ResistDeltas.Select(entry => (entry.Kind, -entry.Delta)).ToList();
            ended.ResistDeltas = Array.Empty<(byte Kind, int Delta)>();
            if (!await ChangeResistsAsync(owner, undone).ConfigureAwait(false)) return false;
        }
        if (ended.FixedDeltas.Count > 0)
        {
            var fixedStats = ended.FixedDeltas.Select(entry => entry.Stat).ToList();
            ended.FixedDeltas = Array.Empty<(int Stat, int Delta)>();
            ApplyEffectDeltas(owner.Value);
            if (!await SendEffectChangesAsync(owner, null, fixedStats, sessions).ConfigureAwait(false)) return false;
        }
        if (ended.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.RevealScan)
        {
            var stillSeen = ended.Tracked.Where(seenId => monsterManager.Get(seenId) is not null).ToList();
            ended.Tracked.Clear();
            if (!await SendRevealedAsync(owner, false, stillSeen).ConfigureAwait(false)) return false;
        }
        foreach (var trackedId in ended.Tracked)
        {
            if (monsterManager.Get(trackedId) is { } trackedMonster
                && !await EndMonsterEffectsAsync(
                    trackedMonster, held => held.Kind.ActionId == ended.Kind.FieldChild && held.CasterEntityId == ended.CasterEntityId, sessions).ConfigureAwait(false))
            {
                return false;
            }
        }
        if (ended.Kind.ResistKind != 0
            && !await ChangeResistAsync(owner, ended.Kind.ResistKind, -(ended.Action?.Amount(ended.Kind.ResistColumn) ?? 0)).ConfigureAwait(false))
        {
            return false;
        }
        if (ended.Kind.RemovesOnEnd == 0) return true;
        foreach (var linked in owner.Value.Effects.Snapshot().Where(held => held.Kind.ActionId == ended.Kind.RemovesOnEnd).ToList())
        {
            if (owner.Value.Effects.Remove(linked) && !await EndSkillEffectAsync(linked, sessions).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> SpreadToMonstersAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKind kind,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(kind.MonsterSpread) is not { } childKind) return true;
        var radius = kind.SpreadRadiusColumn is { } radiusColumn ? action.Amount(radiusColumn) ?? 0 : 0;
        var partyIds = caster.Character is { } spreader && systemLink.MirrorOf(spreader.CharacterId) is { } spreaderParty
            ? spreaderParty.Others.Select(member => member.CharacterId).ToHashSet()
            : new HashSet<ulong>();
        var partyEntityIds = sessions
            .Where(entry => entry.Value.Character is { } member && partyIds.Contains(member.CharacterId))
            .Select(entry => entry.Value.PlayerEntityId)
            .ToHashSet();
        var victims = monsterManager.GetByMap(caster.MapId)
            .Where(monster =>
            {
                if (monster.IsDead || monsterCatalog.GetById(monster.MonsterParamId) is not { } victimDefinition || !SkillActions.MayTarget(victimDefinition)) return false;
                if (monster.OwnerEntityId != MonsterEntity.NoOwner && monster.OwnerEntityId != caster.PlayerEntityId && !partyEntityIds.Contains(monster.OwnerEntityId)) return false;
                var (monsterX, monsterY) = monster.GetInterpolatedPosition(now);
                return SkillAreaRules.Distance(caster.PlayerX, caster.PlayerY, monsterX, monsterY) <= radius;
            })
            .Take(kind.MonsterSpreadLimit)
            .ToList();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} spreads {childKind.ActionId} to {victims.Count} monster(s) within {radius}");
        foreach (var victim in victims)
        {
            if (!await StartSkillEffectAsync(session, sessions, castSkill, childKind, action, victim, now).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> CleanseAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKind kind,
        tPoseidon.Core.LibConfig.SkillTableRow action)
    {
        var caster = session.Value;
        var chance = Math.Clamp((int)Math.Truncate((action.Value(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.ApplyRatioColumn) ?? 0f) * 100.0), 0, 100);
        foreach (var cleansed in kind.CleansesActions)
        {
            if (caster.Effects.Snapshot().FirstOrDefault(held => held.Kind.ActionId == cleansed) is not { } held) continue;
            bool removes;
            lock (npcLoopRng)
            {
                removes = npcLoopRng.Next(1, 100) <= chance;
            }
            if (!removes || !caster.Effects.Remove(held)) continue;
            if (!await EndSkillEffectAsync(held, sessions).ConfigureAwait(false)
                || (held.IsApplied && !await SendEffectChangesAsync(session, null, held.Kind.Stats, sessions).ConfigureAwait(false))
                || !await AfterPlayerEffectEndsAsync(session, held, sessions).ConfigureAwait(false))
            {
                return false;
            }
        }
        var cleanseBlock = commandBlockIds.Next();
        var cleanseTm = caster.GainSkillTmExp is { } gainCleanseTm
            ? gainCleanseTm(castSkill.Row.SkillExp)
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();
        return await SendStrikeFramesAsync(
            [.. SkillFrames.EncodeAreaStart(caster.PlayerEntityId, castSkill, cleanseBlock, Array.Empty<byte>()), .. SkillFrames.EncodeAreaEnd(cleanseBlock, cleanseTm)],
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> OpeningStrikeAsync(
        InGameSessionState striker,
        MonsterEntity victim,
        SkillHit hit,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        DateTimeOffset now)
    {
        var hpBefore = victim.CurrentHp;
        victim.AddAttacker(striker.PlayerEntityId);
        victim.ApplyDamage(hit.Damage);
        var (victimX, victimY) = victim.GetInterpolatedPosition(now);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: striker.PlayerEntityId,
            TargetEntityId: victim.EntityId,
            AttackerX: striker.PlayerX,
            AttackerY: striker.PlayerY,
            TargetX: victimX,
            TargetY: victimY,
            Damage: hit.Damage,
            MobHpAfter: victim.CurrentHp,
            HitFlags: 0,
            HitKind: (uint)hit.Kind,
            Killed: victim.IsDead,
            NextSwingAtUtc: now,
            MonsterParamId: victim.MonsterParamId,
            MobMaxHp: victim.MaxHp,
            MobHpBefore: hpBefore);
        if (strike.Killed && striker.CurrentTargetEntityId == victim.EntityId) striker.CurrentTargetEntityId = 0;
        var (additions, ownerEntityId) = await ResolveStrikeAsync(
            strike, striker, victim.MapId, sessions, zoneGeometryByMap.GetValueOrDefault(victim.MapId), now,
            new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>(), SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
        var strikeBlock = commandBlockIds.Next();
        Console.WriteLine($"{LogTag} [skill] 0x{striker.PlayerEntityId:X4} opening hit -> 0x{victim.EntityId:X4} damage {hit.Damage} kind {hit.Kind}, hp {victim.CurrentHp}/{victim.MaxHp}");
        return await SendStrikeFramesAsync(
            [.. SkillFrames.EncodeAreaVictim(strike, hit, strikeBlock, additions), .. SkillFrames.EncodeAreaEnd(strikeBlock, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<bool> MonsterPeriodicAsync(
        tPoseidon.Core.Game.Skills.Effects.SkillEffect effect,
        MonsterEntity monster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        DateTimeOffset now)
    {
        if (effect.Kind.Periodic != tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.DelayedHold || effect.PeriodCount > 0 || !effect.IsApplied) return true;
        effect.PeriodCount = 1;
        var (heldX, heldY) = monster.HoldUntil(now.AddMilliseconds(effect.RemainingMs), now);
        return await SendFromLoopAsync(
            sessions, Opcode.S2C_OP_0019_TBD,
            new Packet0019Tbd { EntityId = monster.EntityId, WorldX = heldX, WorldY = heldY, StateFlag = monster.MoveType }.EncodePayload(),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
    }

    async Task<bool> InstantSkillAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKind kind,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (caster.Stats is not { } casterStats) return true;
        var ratio = action.Value(SupportSkills.RatioColumn) ?? 0f;
        var casterOnly = new List<KeyValuePair<S2SessionContext, InGameSessionState>> { session };
        switch (kind.Instant)
        {
            case tPoseidon.Core.Game.Skills.Effects.SkillInstant.Cleanse:
                return await CleanseAsync(session, sessions, castSkill, kind, action).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillInstant.SelfCure:
                return tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(SupportSkills.SelfCureDo) is not { } cureKind
                    || await StartSkillEffectAsync(session, sessions, castSkill, cureKind, action, null, now).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillInstant.FirstAid:
            {
                var heal = SupportSkills.FirstAidHeal(caster.PlayerMa, caster.PlayerLk, ratio);
                var healed = SupportSkills.Heal(casterStats.CurrentHp, caster.PlayerMaxHp, heal);
                casterStats.SetVitals(healed.Hp, casterStats.CurrentMp);
                if (healed.Healed && !await SendFromLoopAsync(
                        casterOnly, Opcode.S2C_HEALTH_STATUS,
                        tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(casterStats.CurrentHp).EncodePayload(),
                        MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                {
                    return false;
                }
                Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} first aid heals {heal} (healed {healed.Healed}), HP {casterStats.CurrentHp}");
                return await SendStrikeFramesAsync(
                    SkillFrames.EncodeSupport(
                        caster.PlayerEntityId, caster.PlayerEntityId, kind.ActionId, castSkill,
                        SkillFrames.FlagAmountPointTail(healed.Healed, heal, (caster.PlayerX, caster.PlayerY)), commandBlockIds.Next(),
                        Array.Empty<(Opcode Opcode, byte[] Payload)>(), healed.Healed ? FlatTm(caster, castSkill) : Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                    caster.PlayerEntityId, caster.PlayerEntityId, caster.PlayerEntityId, sessions).ConfigureAwait(false);
            }
            case tPoseidon.Core.Game.Skills.Effects.SkillInstant.ManaConvert:
            {
                var conversion = SupportSkills.ConvertMana(
                    casterStats.CurrentHp, caster.PlayerMaxHp, casterStats.CurrentMp, caster.PlayerMaxMp,
                    action.Value(SupportSkills.HpRatioColumn) ?? 0f, ratio);
                if (conversion.Applied)
                {
                    casterStats.SetVitals(conversion.Hp, conversion.Mp);
                    if (!await SendFromLoopAsync(
                            casterOnly, Opcode.S2C_MANA_STATUS,
                            tPoseidon.Core.Net.Packets.ManaStatusPacket.Of(casterStats.CurrentMp).EncodePayload(),
                            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)
                        || !await SendFromLoopAsync(
                            casterOnly, Opcode.S2C_HEALTH_STATUS,
                            tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(casterStats.CurrentHp).EncodePayload(),
                            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                    {
                        return false;
                    }
                }
                return await SendStrikeFramesAsync(
                    SkillFrames.EncodeSupport(
                        caster.PlayerEntityId, caster.PlayerEntityId, kind.ActionId, castSkill,
                        SkillFrames.FlagPointTail(conversion.Applied, (caster.PlayerX, caster.PlayerY)), commandBlockIds.Next(),
                        Array.Empty<(Opcode Opcode, byte[] Payload)>(), conversion.Applied ? FlatTm(caster, castSkill) : Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                    caster.PlayerEntityId, caster.PlayerEntityId, caster.PlayerEntityId, sessions).ConfigureAwait(false);
            }
            case tPoseidon.Core.Game.Skills.Effects.SkillInstant.HeartsGrace:
            case tPoseidon.Core.Game.Skills.Effects.SkillInstant.MedCure:
            {
                var radius = action.Amount(SupportSkills.RadiusColumn) ?? 0;
                var partyIds = caster.Character is { } supporter && systemLink.MirrorOf(supporter.CharacterId) is { } supporterParty
                    ? supporterParty.Others.Select(member => member.CharacterId).ToHashSet()
                    : new HashSet<ulong>();
                var members = sessions
                    .Where(entry => (entry.Value == caster || (entry.Value.Character is { } memberCharacter && partyIds.Contains(memberCharacter.CharacterId)))
                        && !entry.Value.IsDead
                        && SkillAreaRules.Distance(caster.PlayerX, caster.PlayerY, entry.Value.PlayerX, entry.Value.PlayerY) <= radius)
                    .Take(kind.Instant == tPoseidon.Core.Game.Skills.Effects.SkillInstant.MedCure ? SupportSkills.MedCureMembers : int.MaxValue)
                    .ToList();
                foreach (var member in members)
                {
                    var holder = member.Value;
                    if (holder.Stats is not { } holderStats) continue;
                    if (kind.Instant == tPoseidon.Core.Game.Skills.Effects.SkillInstant.MedCure)
                    {
                        var (removed, cleanSent) = await RemoveEffectsAsync(member, null, SupportSkills.CleansedBy(action, SupportSkills.MedCureEntries), sessions).ConfigureAwait(false);
                        if (!cleanSent || !await SendStrikeFramesAsync(
                            SkillFrames.EncodeSupport(
                                caster.PlayerEntityId, holder.PlayerEntityId, SupportSkills.MedCureOnMember, castSkill,
                                SkillFrames.FlagTail(removed), commandBlockIds.Next(),
                                Array.Empty<(Opcode Opcode, byte[] Payload)>(), removed ? FlatTm(caster, castSkill) : Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                            caster.PlayerEntityId, holder.PlayerEntityId, holder.PlayerEntityId, sessions).ConfigureAwait(false))
                        {
                            return false;
                        }
                        continue;
                    }
                    var grace = SupportSkills.GraceHeal(holder.PlayerMaxHp, caster.PlayerMa, ratio);
                    var graced = SupportSkills.Heal(holderStats.CurrentHp, holder.PlayerMaxHp, grace);
                    holderStats.SetVitals(graced.Hp, holderStats.CurrentMp);
                    var inBlock = graced.Healed
                        ? new[] { (Opcode.S2C_HEALTH_STATUS, tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(holderStats.CurrentHp).EncodePayload()) }
                        : Array.Empty<(Opcode Opcode, byte[] Payload)>();
                    if (!await SendStrikeFramesAsync(
                        SkillFrames.EncodeSupport(
                            caster.PlayerEntityId, holder.PlayerEntityId, kind.ActionId, castSkill,
                            SkillFrames.FlagAmountTail(graced.Healed, grace), commandBlockIds.Next(),
                            inBlock, graced.Healed ? FlatTm(caster, castSkill) : Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                        caster.PlayerEntityId, holder.PlayerEntityId, holder.PlayerEntityId, sessions).ConfigureAwait(false))
                    {
                        return false;
                    }
                }
                Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} reaches {members.Count} member(s) within {radius}");
                return true;
            }
            default:
                return true;
        }
    }

    int CarriedWeightOf(InGameSessionState carrier) =>
        carrier.Character is { } carrierCharacter
            ? Math.Min(
                itemStore.GetForCharacterBySlotKind(carrierCharacter.CharacterId, SlotKind.Inventory)
                    .Sum(held => (itemCatalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits),
                carrier.PlayerMaxWt)
            : 0;

    IReadOnlyList<(Opcode Opcode, byte[] Payload)> FlatTm(InGameSessionState caster, SkillDefinition castSkill) =>
        caster.GainSkillTmExp is { } gainTm ? gainTm(castSkill.Row.SkillExp) : Array.Empty<(Opcode Opcode, byte[] Payload)>();

    async Task<(bool Removed, bool Sent)> RemoveEffectsAsync(
        KeyValuePair<S2SessionContext, InGameSessionState>? holder,
        MonsterEntity? monster,
        IReadOnlyCollection<int> actionIds,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions)
    {
        var book = holder is { } player ? player.Value.Effects : monster is not null ? MonsterEffectsOf(monster.EntityId) : null;
        if (book is null) return (false, true);
        var removed = false;
        foreach (var held in book.Snapshot().Where(held => actionIds.Contains(held.Kind.ActionId)).ToList())
        {
            if (!book.Remove(held)) continue;
            removed = true;
            if (!await EndSkillEffectAsync(held, sessions).ConfigureAwait(false)) return (removed, false);
            if (holder is { } owner)
            {
                if ((held.IsApplied && !await SendEffectChangesAsync(owner, null, held.Kind.Stats, sessions).ConfigureAwait(false))
                    || !await AfterPlayerEffectEndsAsync(owner, held, sessions).ConfigureAwait(false))
                {
                    return (removed, false);
                }
            }
            else if (monster is not null)
            {
                UndoMonsterEffect(monster, held);
            }
        }
        return (removed, true);
    }

    async Task<bool> BasicHealingAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        KeyValuePair<S2SessionContext, InGameSessionState>? healedPlayer,
        MonsterEntity? healedMonster)
    {
        var caster = session.Value;
        var targetId = healedPlayer?.Value.PlayerEntityId ?? healedMonster?.EntityId ?? 0;
        var (removed, sent) = await RemoveEffectsAsync(
            healedPlayer, healedMonster, SupportSkills.CleansedBy(action, SupportSkills.BasicHealingEntries), sessions).ConfigureAwait(false);
        if (!sent) return false;
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} basic healing on 0x{targetId:X4}: removed {removed}");
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeSupport(
                caster.PlayerEntityId, targetId, castSkill.ActionId, castSkill, SkillFrames.FlagTail(removed), commandBlockIds.Next(),
                Array.Empty<(Opcode Opcode, byte[] Payload)>(), FlatTm(caster, castSkill)),
            caster.PlayerEntityId, targetId, targetId, sessions).ConfigureAwait(false);
    }

    async Task<bool> RevivalAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        KeyValuePair<S2SessionContext, InGameSessionState> revived,
        DateTimeOffset now)
    {
        var caster = session.Value;
        var risen = revived.Value;
        if (!risen.IsDead || risen.Stats is not { } risenStats || risen.Character is not { } risenCharacter) return true;
        risenStats.Resurrect(
            risen.PlayerMaxHp, risen.PlayerMaxMp,
            action.Value(SupportSkills.HpRestoreColumn) ?? 0f, action.Value(SupportSkills.MpRestoreColumn) ?? 0f, action.Value(SupportSkills.ExpRestoreColumn) ?? 0f);
        risen.ResurrectionCompletesAt = now + PlayerRevivalFrames.CompletionDelay;
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} revives 0x{risen.PlayerEntityId:X4}: HP {risenStats.CurrentHp}, MP {risenStats.CurrentMp}, exp {risenStats.Exp}");
        var risenOnly = new List<KeyValuePair<S2SessionContext, InGameSessionState>> { revived };
        if (!await SendFromLoopAsync(
                risenOnly, Opcode.S2C_EXP_VALUE, PlayerDeathFrames.ExpValue(risenStats.Exp), MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)
            || !await SendFromLoopAsync(
                risenOnly, Opcode.S2C_OP_011D_TBD,
                ProgressFrames.TmTotal((byte)StatGrowth.TypeIndexOf(risenCharacter.FType), risenStats.TmExp),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)
            || !await SendFromLoopAsync(
                sessions, Opcode.S2C_RESURRECTED, PlayerRevivalFrames.Resurrected(risen.PlayerEntityId, PlayerRevivalFrames.NoReviver),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
        {
            return false;
        }
        foreach (var (tmOpcode, tmPayload) in FlatTm(caster, castSkill))
        {
            if (!await SendFromLoopAsync(new[] { session }, tmOpcode, tmPayload, MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> GroundSkillAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (EventSkills.IsGround(castSkill.ActionId))
        {
            return await GmRegionAsync(session, sessions, castSkill, action, point, mapId, now).ConfigureAwait(false);
        }
        if (ObjectHits.IsGround(castSkill.ActionId))
        {
            return await ObjectGroundAsync(session, sessions, castSkill, action, point, mapId, now).ConfigureAwait(false);
        }
        if (castSkill.ActionId == GroundSkills.DimensionCrack)
        {
            return await DimensionCrackAsync(session, sessions, castSkill, action, point, mapId, now).ConfigureAwait(false);
        }
        if (castSkill.ActionId == ThrowSkills.FourCard)
        {
            return await FourCardAsync(session, sessions, castSkill, action, point, mapId, now).ConfigureAwait(false);
        }
        if (castSkill.ActionId == GroundSkills.BuffCancler)
        {
            return await BuffCanclerAsync(session, sessions, castSkill, action, point, mapId, now).ConfigureAwait(false);
        }
        if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(castSkill.ActionId) is { FieldChild: not 0 } fieldKind)
        {
            if (!await StartSkillEffectAsync(session, sessions, castSkill, fieldKind, action, null, now, groundPoint: point).ConfigureAwait(false)) return false;
            return !fieldKind.StrikesOnStart || await FieldOpeningAsync(session, sessions, fieldKind, action, point, mapId, now).ConfigureAwait(false);
        }
        if (GroundSkills.SpawnOf(castSkill.ActionId) is not { } spawn
            || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(spawn.ObjectAction) is not { } objectKind)
        {
            return true;
        }
        var radius = action.Amount(SkillAreaShapes.RadiusColumn) ?? 0;
        var partyIds = caster.Character is { } grounder && systemLink.MirrorOf(grounder.CharacterId) is { } grounderParty
            ? grounderParty.Others.Select(member => member.CharacterId).ToHashSet()
            : new HashSet<ulong>();
        var partyEntityIds = sessions
            .Where(entry => entry.Value.Character is { } member && partyIds.Contains(member.CharacterId))
            .Select(entry => entry.Value.PlayerEntityId)
            .ToHashSet();
        var victims = monsterManager.GetByMap(mapId)
            .Where(monster =>
            {
                if (monster.IsDead || monsterCatalog.GetById(monster.MonsterParamId) is not { } victimDefinition) return false;
                if (victimDefinition.Type == SkillAreaShapes.UntouchableMonsterType) return false;
                if (spawn.SkipsFlyers && victimDefinition.Type == SkillAreaShapes.FlyerMonsterType) return false;
                if (monster.OwnerEntityId != MonsterEntity.NoOwner && monster.OwnerEntityId != caster.PlayerEntityId && !partyEntityIds.Contains(monster.OwnerEntityId)) return false;
                var (monsterX, monsterY) = monster.GetInterpolatedPosition(now);
                return SkillAreaRules.Distance(point.X, point.Y, monsterX, monsterY) <= radius;
            })
            .Take(spawn.Limit)
            .ToList();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} at ({point.X},{point.Y}) radius {radius}: {victims.Count} monster(s)");
        if (spawn.AnnouncesPoint)
        {
            if (victims.Count == 0) return true;
            var pointBlock = commandBlockIds.Next();
            if (!await SendStrikeFramesAsync(
                [.. SkillFrames.EncodeAreaStart(caster.PlayerEntityId, castSkill, pointBlock, SkillFrames.PointTail(point)),
                 .. SkillFrames.EncodeAreaEnd(pointBlock, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
                caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
            {
                return false;
            }
        }
        foreach (var victim in victims)
        {
            if (!await StartSkillEffectAsync(session, sessions, castSkill, objectKind, action, victim, now).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> DelayedStrikeAsync(
        tPoseidon.Core.Game.Skills.Effects.SkillEffect effect,
        MonsterEntity monster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        if (monster.IsDead) return true;
        if (effect.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.StoredBurn)
        {
            return await StoredBurnAsync(effect, monster, sessions, now).ConfigureAwait(false);
        }
        if (effect.Kind.Periodic is tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.MonsterWarp or tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.SelfDestruct)
        {
            return await MonsterObjectTickAsync(effect, monster, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
        }
        if (effect.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.HarconSlots)
        {
            return await HarconTickAsync(effect, monster, sessions, mapId).ConfigureAwait(false);
        }
        if (effect.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.KnockMove)
        {
            if (effect.Point is { } knockedTo && !monster.IsDead)
            {
                monster.InitPosition(knockedTo.X, knockedTo.Y);
                Console.WriteLine($"{LogTag} [skill] 0x{monster.EntityId:X4} lands at ({knockedTo.X},{knockedTo.Y})");
            }
            effect.EndNow();
            return true;
        }
        if (effect.Kind.Periodic is tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.PoisonTick or tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.FixedPoison)
        {
            return await PoisonTickAsync(effect, monster, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
        }
        if (effect.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.FixedBurn)
        {
            return await FixedBurnAsync(effect, monster, sessions, now).ConfigureAwait(false);
        }
        if (effect.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.BuffCancel)
        {
            return await BuffCancelPassAsync(effect, monster, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
        }
        var repeats = effect.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.RepeatingStrike;
        if (!repeats && (effect.Kind.Periodic != tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.DelayedStrike || effect.PeriodCount > 0)) return true;
        effect.PeriodCount++;
        if (effect.Action is not { } strikeRow
            || monsterCatalog.GetById(monster.MonsterParamId) is not { } struckDefinition
            || sessions.FirstOrDefault(entry => entry.Value.PlayerEntityId == effect.CasterEntityId).Value is not { } striker
            || striker.Stats is not { } strikerStats)
        {
            return true;
        }
        SkillHit hit;
        lock (npcLoopRng)
        {
            hit = SkillDamage.Magic(
                new SkillAttacker(striker.PlayerAp, striker.PlayerAc, striker.PlayerMa, striker.PlayerLk, strikerStats.Level, DarkBonus: DarkBonusOf(striker)),
                (strikeRow.Amount(effect.Kind.AmountColumn) ?? 0)
                    + (effect.Kind.StrikeAddsElement
                        ? SkillHitShapes.DefaultElementStep * ElementTotalOf(striker, ElementSlots.AttackSlotOf(effect.Kind.StrikeElement))
                        : 0),
                MonsterTarget(monster, struckDefinition, effect.Kind.StrikeElement),
                effect.Kind.FixedCoefficient ?? strikeRow.Value(effect.Kind.ChanceColumn) ?? 0f, effect.Kind.StrikeMagicLoss, effect.Kind.StrikeElement, npcLoopRng);
        }
        if (repeats) return await PeriodicStrikeAsync(striker, monster, hit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
        var hpBefore = monster.CurrentHp;
        monster.AddAttacker(striker.PlayerEntityId);
        monster.ApplyDamage(hit.Damage);
        var (monsterX, monsterY) = monster.GetInterpolatedPosition(now);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: striker.PlayerEntityId,
            TargetEntityId: monster.EntityId,
            AttackerX: striker.PlayerX,
            AttackerY: striker.PlayerY,
            TargetX: monsterX,
            TargetY: monsterY,
            Damage: hit.Damage,
            MobHpAfter: monster.CurrentHp,
            HitFlags: 0,
            HitKind: (uint)hit.Kind,
            Killed: monster.IsDead,
            NextSwingAtUtc: now,
            MonsterParamId: monster.MonsterParamId,
            MobMaxHp: monster.MaxHp,
            MobHpBefore: hpBefore);
        if (strike.Killed && striker.CurrentTargetEntityId == monster.EntityId) striker.CurrentTargetEntityId = 0;
        var (additions, ownerEntityId) = await ResolveStrikeAsync(
            strike, striker, mapId, sessions, zoneGeometry, now, battleEvents,
            SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
        Console.WriteLine($"{LogTag} [skill] 0x{striker.PlayerEntityId:X4} object {effect.Kind.ActionId} hits 0x{monster.EntityId:X4}: damage {hit.Damage} kind {hit.Kind}, hp {monster.CurrentHp}/{monster.MaxHp}");
        return await SendStrikeFramesAsync(
            [.. SkillFrames.EncodeAreaVictim(strike, hit, effect.BlockId, additions), .. SkillFrames.EncodeAreaEnd(effect.BlockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false);
    }

    List<MonsterEntity> MonstersAround(
        InGameSessionState caster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        (int X, int Y) centre,
        int radius,
        bool skipsFlyers,
        DateTimeOffset now)
    {
        var partyIds = caster.Character is { } scanner && systemLink.MirrorOf(scanner.CharacterId) is { } scannerParty
            ? scannerParty.Others.Select(member => member.CharacterId).ToHashSet()
            : new HashSet<ulong>();
        var partyEntityIds = sessions
            .Where(entry => entry.Value.Character is { } member && partyIds.Contains(member.CharacterId))
            .Select(entry => entry.Value.PlayerEntityId)
            .ToHashSet();
        return monsterManager.GetByMap(mapId)
            .Where(monster =>
            {
                if (monster.IsDead || monsterCatalog.GetById(monster.MonsterParamId) is not { } scannedDefinition) return false;
                if (scannedDefinition.Type == SkillAreaShapes.UntouchableMonsterType) return false;
                if (skipsFlyers && scannedDefinition.Type == SkillAreaShapes.FlyerMonsterType) return false;
                if (monster.OwnerEntityId != MonsterEntity.NoOwner && monster.OwnerEntityId != caster.PlayerEntityId && !partyEntityIds.Contains(monster.OwnerEntityId)) return false;
                var (monsterX, monsterY) = monster.GetInterpolatedPosition(now);
                return SkillAreaRules.Distance(centre.X, centre.Y, monsterX, monsterY) <= radius;
            })
            .ToList();
    }

    void UndoMonsterEffect(MonsterEntity monster, tPoseidon.Core.Game.Skills.Effects.SkillEffect ended)
    {
        if (ended.IsApplied) ApplyMonsterEffectDeltas(monster);
        if (ended.Slow > 0f) SlowMonster(monster);
        if (ended.ResistLoss != 0)
        {
            if (ended.Kind.BreaksBarrier)
            {
                monster.AddResistLoss(SkillActions.NoElement, -ended.ResistLoss);
                monster.AddResistLoss(SkillActions.GunElement, -ended.ResistLoss);
            }
            else
            {
                monster.AddResistLoss(ended.Kind.LowersResistElement, -ended.ResistLoss);
            }
            ended.ResistLoss = 0;
        }
    }

    async Task<bool> EndMonsterEffectsAsync(
        MonsterEntity monster,
        Func<tPoseidon.Core.Game.Skills.Effects.SkillEffect, bool> which,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions)
    {
        var book = MonsterEffectsOf(monster.EntityId);
        foreach (var held in book.Snapshot().Where(which).ToList())
        {
            if (!book.Remove(held)) continue;
            if (!await EndSkillEffectAsync(held, sessions).ConfigureAwait(false)) return false;
            UndoMonsterEffect(monster, held);
        }
        return true;
    }

    async Task<bool> FieldScanAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect field,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        var owner = session.Value;
        if (field.Action is not { } fieldRow
            || skillBook.Find(field.SkillId) is not { } fieldSkill
            || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(field.Kind.FieldChild) is not { } childKind)
        {
            return true;
        }
        (int X, int Y) centre = field.Kind.FollowsCaster || field.Point is not { } fieldPoint
            ? (owner.PlayerX, owner.PlayerY)
            : (fieldPoint.X, fieldPoint.Y);
        var radius = WireLimits.SaturateInt32(field.Magnitude);
        var limit = Math.Min(field.Kind.FieldLimitCap, field.Kind.FieldLimitColumn is { } limitColumn ? fieldRow.Amount(limitColumn) ?? 0 : int.MaxValue);
        if (field.Kind.FieldTracks)
        {
            foreach (var trackedId in field.Tracked.ToList())
            {
                if (monsterManager.Get(trackedId) is not { IsDead: false } tracked || tracked.MapId != mapId)
                {
                    field.Tracked.Remove(trackedId);
                    continue;
                }
                var (trackedX, trackedY) = tracked.GetInterpolatedPosition(now);
                if (SkillAreaRules.Distance(centre.X, centre.Y, trackedX, trackedY) <= radius) continue;
                field.Tracked.Remove(trackedId);
                if (!await EndMonsterEffectsAsync(tracked, held => held.Kind.ActionId == childKind.ActionId && held.CasterEntityId == owner.PlayerEntityId, sessions).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }
        var fresh = MonstersAround(owner, sessions, mapId, centre, radius, skipsFlyers: false, now)
            .Where(monster => !field.Tracked.Contains(monster.EntityId))
            .Take(Math.Max(0, limit - field.Tracked.Count))
            .ToList();
        foreach (var victim in fresh)
        {
            if (field.Kind.FieldTracks) field.Tracked.Add(victim.EntityId);
            if (!await StartSkillEffectAsync(session, sessions, fieldSkill, childKind, fieldRow, victim, now).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> DimensionCrackAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (caster.Stats is not { } crackStats) return true;
        var radius = action.Amount(SkillAreaShapes.RadiusColumn) ?? 0;
        var victims = MonstersAround(caster, sessions, mapId, (point.X, point.Y), radius, skipsFlyers: false, now)
            .Take(action.Amount(GroundSkills.KillNumColumn) ?? 0)
            .ToList();
        var tail = new tPoseidon.Core.Net.PacketWriter(2 + 4 * victims.Count);
        tail.WriteUInt16((ushort)victims.Count);
        foreach (var victim in victims)
        {
            var (victimX, victimY) = victim.GetInterpolatedPosition(now);
            tail.WriteUInt16((ushort)victimX);
            tail.WriteUInt16((ushort)victimY);
        }
        var crackBlock = commandBlockIds.Next();
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaStart(caster.PlayerEntityId, castSkill, crackBlock, tail.ToArray()),
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }
        var attacker = new SkillAttacker(caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, crackStats.Level, DarkBonus: DarkBonusOf(caster));
        foreach (var victim in victims)
        {
            if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } crackedDefinition) continue;
            SkillHit hit;
            lock (npcLoopRng)
            {
                hit = SkillDamage.Magic(
                    attacker, (int)(action.Value(GroundSkills.RatioColumn) ?? 0f), MonsterTarget(victim, crackedDefinition, SkillActions.NoPropertyElement),
                    action.Value(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.ApplyRatioColumn) ?? 0f,
                    GroundSkills.DimensionMagicLoss, SkillActions.NoPropertyElement, npcLoopRng);
            }
            var hpBefore = victim.CurrentHp;
            victim.AddAttacker(caster.PlayerEntityId);
            victim.ApplyDamage(hit.Damage);
            var (victimX, victimY) = victim.GetInterpolatedPosition(now);
            var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
                AttackerEntityId: caster.PlayerEntityId, TargetEntityId: victim.EntityId,
                AttackerX: caster.PlayerX, AttackerY: caster.PlayerY, TargetX: victimX, TargetY: victimY,
                Damage: hit.Damage, MobHpAfter: victim.CurrentHp, HitFlags: 0, HitKind: (uint)hit.Kind, Killed: victim.IsDead,
                NextSwingAtUtc: now, MonsterParamId: victim.MonsterParamId, MobMaxHp: victim.MaxHp, MobHpBefore: hpBefore);
            var (additions, ownerEntityId) = await ResolveStrikeAsync(
                strike, caster, mapId, sessions, zoneGeometryByMap.GetValueOrDefault(mapId), now,
                new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>(), SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
            if (!await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaVictim(strike, hit, crackBlock, additions),
                strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false))
            {
                return false;
            }
        }
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(crackBlock, FlatTm(caster, castSkill)),
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.DimensionOnMonster) is not { } holdKind) return true;
        foreach (var survivor in victims.Where(victim => !victim.IsDead))
        {
            if (!await StartSkillEffectAsync(session, sessions, castSkill, holdKind, action, survivor, now).ConfigureAwait(false)
                || !await SendFromLoopAsync(
                    sessions, Opcode.S2C_OP_016E_TBD, BitConverter.GetBytes(survivor.EntityId), MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
        }
        return true;
    }

    async Task<bool> BuffCanclerAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.BuffCanclerOnMonster) is not { } cancelKind) return true;
        var victims = MonstersAround(caster, sessions, mapId, (point.X, point.Y), action.Amount(SkillAreaShapes.RadiusColumn) ?? 0, skipsFlyers: false, now)
            .Take(action.Amount(GroundSkills.KillNumColumn) ?? 0)
            .Where(victim => MonsterEffectsOf(victim.EntityId).Snapshot().Any(held => held.IsApplied && held.EsClass == 0))
            .ToList();
        foreach (var victim in victims)
        {
            if (!await StartSkillEffectAsync(session, sessions, castSkill, cancelKind, action, victim, now).ConfigureAwait(false)) return false;
        }
        foreach (var (tmOpcode, tmPayload) in FlatTm(caster, castSkill))
        {
            if (!await SendFromLoopAsync(new[] { session }, tmOpcode, tmPayload, MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> BuffCancelPassAsync(
        tPoseidon.Core.Game.Skills.Effects.SkillEffect cancel,
        MonsterEntity monster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var book = MonsterEffectsOf(monster.EntityId);
        var buff = book.Snapshot().Where(held => held.IsApplied && held.EsClass == 0).MaxBy(held => held.Sequence);
        if (buff is null
            || sessions.FirstOrDefault(entry => entry.Value.PlayerEntityId == cancel.CasterEntityId).Value is not { } canceller
            || canceller.Stats is not { } cancellerStats
            || monsterCatalog.GetById(monster.MonsterParamId) is not { } cancelledDefinition)
        {
            cancel.EndNow();
            return true;
        }
        SkillHit hit;
        lock (npcLoopRng)
        {
            hit = SkillDamage.Physical(
                new SkillAttacker(canceller.PlayerAp, canceller.PlayerAc, canceller.PlayerMa, canceller.PlayerLk, cancellerStats.Level),
                cancel.Magnitude, MonsterTarget(monster, cancelledDefinition, SkillActions.NoElement),
                cancel.Action?.Value(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.ApplyRatioColumn) ?? 0f, SkillActions.NoElement, npcLoopRng);
        }
        hit = hit with { Flags = hit.Flags | tPoseidon.Core.Game.Skills.Effects.IncomingHits.BuffCancelFlag };
        if (!await PeriodicStrikeAsync(canceller, monster, hit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
        cancel.Charges--;
        if (cancel.Charges <= 0) cancel.EndNow();
        if (hit.Damage < 1 || (hit.Kind & SkillDamage.KindMask) is SkillDamage.KindMiss or tPoseidon.Core.Game.Skills.Effects.IncomingHits.KindGuarded) return true;
        if (!book.Remove(buff)) return true;
        if (!await EndSkillEffectAsync(buff, sessions).ConfigureAwait(false)) return false;
        UndoMonsterEffect(monster, buff);
        return true;
    }

    long GunPowerOf(tPoseidon.Core.Game.Items.WornItem gun)
    {
        var properties = itemPropertyRules.Full(gun.Definition, itemPropertyStore.ForItem(gun.Item.ItemUid));
        return properties.Any(property => property.Id == tPoseidon.Core.Game.Items.ItemPropertyRules.GunApId)
            ? PropertyValue(properties, tPoseidon.Core.Game.Items.ItemPropertyRules.GunApId)
            : gun.Definition.Ap;
    }

    float SharpSenseOf(InGameSessionState shooter) =>
        shooter.Effects.Snapshot().FirstOrDefault(held => held.IsApplied && held.Kind.ActionId == tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.SharpSense)?.Amount ?? 1f;

    float SharpShotOf(InGameSessionState shooter) =>
        shooter.Effects.Snapshot().FirstOrDefault(held => held.IsApplied && held.Kind.ActionId == tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.SharpSenseExtra)?.Amount ?? 1f;

    void SpendBullets(Item bullet, int count)
    {
        if (count <= 0) return;
        var left = itemStore.AdjustStack(bullet.ItemUid, -count);
        Console.WriteLine($"{LogTag} [skill] {count} bullet(s) of item {bullet.ItemIndex} used, {left} left");
    }

    bool RollsApplyRatio(float applyRatio)
    {
        lock (npcLoopRng)
        {
            return npcLoopRng.Next(100) < Math.Clamp((int)Math.Truncate(applyRatio * 100.0), 0, 100);
        }
    }

    async Task<bool> GunShotAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity target,
        MonsterParamEntry targetDefinition,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        var (gun, bullet) = GunGearOf(caster);
        if (caster.Stats is not { } shooterStats || gun is null || bullet is null)
        {
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} has no gun or no bullet");
            return true;
        }
        var shots = castSkill.ActionId == GunSkills.DoubleShot ? GunSkills.DoubleShotHits : 1;
        var hits = new List<SkillHit>(shots);
        var attacker = new SkillAttacker(caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, shooterStats.Level);
        var weaponPower = GunPowerOf(gun);
        var victim = MonsterTarget(target, targetDefinition, SkillActions.GunElement);
        for (var shot = 0; shot < shots; shot++)
        {
            lock (npcLoopRng)
            {
                hits.Add(GunSkills.Hit(
                    attacker, weaponPower, victim, targetIsPlayer: false,
                    action.Value(GunSkills.RatioColumn) ?? 0f, action.Value(GunSkills.ApplyRatioColumn) ?? 0f, SharpShotOf(caster), npcLoopRng));
            }
        }
        var chain = new ChainVictim(target.EntityId, hits);
        var hpBefore = target.CurrentHp;
        target.AddAttacker(caster.PlayerEntityId);
        target.ApplyDamage(chain.Sum);
        var (targetX, targetY) = target.GetInterpolatedPosition(now);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: caster.PlayerEntityId, TargetEntityId: target.EntityId,
            AttackerX: caster.PlayerX, AttackerY: caster.PlayerY, TargetX: targetX, TargetY: targetY,
            Damage: chain.Sum, MobHpAfter: target.CurrentHp, HitFlags: 0, HitKind: (uint)hits[0].Kind, Killed: target.IsDead,
            NextSwingAtUtc: now, MonsterParamId: target.MonsterParamId, MobMaxHp: target.MaxHp, MobHpBefore: hpBefore);
        if (strike.Killed && caster.CurrentTargetEntityId == target.EntityId) caster.CurrentTargetEntityId = 0;
        if (shots == 1) SpendBullets(bullet, shots);
        var (additions, ownerEntityId) = await ResolveStrikeAsync(
            strike, caster, mapId, sessions, zoneGeometry, now, battleEvents, SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
        var tmFrames = caster.GainSkillTmExp is { } gainGunTm
            ? gainGunTm(SkillActions.TmExpForUse(castSkill.Row.SkillExp, shooterStats.Level, target.Level))
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} shoots 0x{target.EntityId:X4}: {string.Join("/", hits.Select(hit => $"{hit.Damage}k{hit.Kind}"))}, hp {target.CurrentHp}/{target.MaxHp}");
        var blockId = commandBlockIds.Next();
        if (shots == 1)
        {
            return await SendStrikeFramesAsync(
                SkillFrames.EncodeHit(
                    strike, castSkill, hits[0], blockId, additions, tmFrames, casterTwice: true,
                    tail: SkillFrames.ChainTailBytes(ChainTail.TargetPointsThenSlots, target.EntityId, (caster.PlayerX, caster.PlayerY), (targetX, targetY), Array.Empty<ChainVictim>())),
                strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false);
        }
        var shape = new ChainShape(ChainTail.SlotsOnly, TargetInHeader: true, ScaledTm: true, Restarts: false);
        var sent = await SendStrikeFramesAsync(
            [.. SkillFrames.EncodeChainStart(
                    caster.PlayerEntityId, target.EntityId, castSkill, shape,
                    SkillFrames.ChainTailBytes(ChainTail.SlotsOnly, target.EntityId, (caster.PlayerX, caster.PlayerY), (targetX, targetY), new[] { chain }),
                    additions.OwnerTaken is { } shotOwner ? new[] { shotOwner } : Array.Empty<byte[]>(), blockId),
             .. SkillFrames.EncodeChainVictim(strike, chain, blockId, additions, GunSkills.GunType, SkillActions.GunElement),
             .. SkillFrames.EncodeAreaEnd(blockId, tmFrames)],
            strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false);
        SpendBullets(bullet, shots);
        return sent;
    }

    async Task<bool> GunAreaAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        var (gun, bullet) = GunGearOf(caster);
        if (caster.Stats is not { } shooterStats || gun is null || bullet is null) return true;
        var ok = RollsApplyRatio(action.Value(GunSkills.ApplyRatioColumn) ?? 0f);
        var radius = action.Amount(GunSkills.RadiusColumn) ?? 0;
        var limit = Math.Min(action.Amount(GunSkills.MonNumColumn) ?? 0, bullet.Stack);
        var victims = castSkill.ActionId == GunSkills.ShootingSpree || ok
            ? MonstersAround(caster, sessions, mapId, (caster.PlayerX, caster.PlayerY), radius, skipsFlyers: castSkill.ActionId == GunSkills.ShootingSpree, now)
                .Take(Math.Min(limit, castSkill.ActionId == GunSkills.ShootingSpree ? GunSkills.ShootingSpreeVictims : GunSkills.RecklessVictims))
                .ToList()
            : new List<MonsterEntity>();
        if (castSkill.ActionId == GunSkills.Reckless) ok = ok && victims.Count > 0;
        SpendBullets(bullet, victims.Count);
        var tail = new tPoseidon.Core.Net.PacketWriter(3 + 2 * victims.Count);
        tail.WriteByte(ok ? (byte)1 : (byte)0);
        tail.WriteUInt16((ushort)victims.Count);
        foreach (var victim in victims) tail.WriteUInt16(victim.EntityId);
        var blockId = commandBlockIds.Next();
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaStart(caster.PlayerEntityId, castSkill, blockId, tail.ToArray()),
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }
        var attacker = new SkillAttacker(caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, shooterStats.Level);
        var weaponPower = GunPowerOf(gun);
        foreach (var victim in ok ? victims : new List<MonsterEntity>())
        {
            if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition) continue;
            SkillHit hit;
            lock (npcLoopRng)
            {
                hit = GunSkills.Hit(
                    attacker, weaponPower, MonsterTarget(victim, victimDefinition, SkillActions.GunElement), targetIsPlayer: false,
                    action.Value(GunSkills.RatioColumn) ?? 0f, action.Value(GunSkills.ApplyRatioColumn) ?? 0f, SharpShotOf(caster), npcLoopRng);
            }
            var hpBefore = victim.CurrentHp;
            victim.AddAttacker(caster.PlayerEntityId);
            victim.ApplyDamage(hit.Damage);
            var (victimX, victimY) = victim.GetInterpolatedPosition(now);
            var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
                AttackerEntityId: caster.PlayerEntityId, TargetEntityId: victim.EntityId,
                AttackerX: caster.PlayerX, AttackerY: caster.PlayerY, TargetX: victimX, TargetY: victimY,
                Damage: hit.Damage, MobHpAfter: victim.CurrentHp, HitFlags: 0, HitKind: (uint)hit.Kind, Killed: victim.IsDead,
                NextSwingAtUtc: now, MonsterParamId: victim.MonsterParamId, MobMaxHp: victim.MaxHp, MobHpBefore: hpBefore);
            if (strike.Killed && caster.CurrentTargetEntityId == victim.EntityId) caster.CurrentTargetEntityId = 0;
            var (additions, ownerEntityId) = await ResolveStrikeAsync(
                strike, caster, mapId, sessions, zoneGeometry, now, battleEvents, SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
            if (!await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaVictim(strike, hit, blockId, additions),
                strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false))
            {
                return false;
            }
        }
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} {(ok ? "fires" : "misfires")} at {victims.Count} monster(s)");
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, ok ? FlatTm(caster, castSkill) : Array.Empty<(Opcode Opcode, byte[] Payload)>()),
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> RideAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow castAction,
        SkillHit hit,
        MonsterEntity target,
        DateTimeOffset now)
    {
        var caster = session.Value;
        foreach (var rider in SkillRiders.Of(castSkill.ActionId))
        {
            if (!caster.Skills.TryGetValue(rider.PassiveSkill, out var passiveLevel) || (rider.NeedsLevel && passiveLevel <= 0)) continue;
            if (!SkillRiders.Gates(hit, target.CurrentHp)) continue;
            if (skillBook.Find(rider.PassiveSkill) is not { } passive || passive.ActionAt(passiveLevel) is not { } passiveRow
                || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(rider.EffectAction) is not { } riderKind)
            {
                continue;
            }
            long? stored = null;
            if (rider.EffectAction == SkillRiders.CruelFlame)
            {
                if (caster.Stats is not { } burnerStats || monsterCatalog.GetById(target.MonsterParamId) is not { } burnedDefinition) continue;
                SkillHit burn;
                lock (npcLoopRng)
                {
                    burn = SkillDamage.Magic(
                        new SkillAttacker(caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, burnerStats.Level, DarkBonus: DarkBonusOf(caster)),
                        castAction.Amount(SkillRiders.PowerColumn) ?? 0, MonsterTarget(target, burnedDefinition, SkillActions.DarkElement),
                        passiveRow.Value(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.ApplyRatioColumn) ?? 0f,
                        SkillRiders.BurnMagicLoss, SkillActions.DarkElement, npcLoopRng);
                }
                if (burn.Damage <= 0) continue;
                stored = burn.Damage;
            }
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} rides {rider.PassiveSkill} -> {rider.EffectAction} on 0x{target.EntityId:X4}");
            if (!await StartSkillEffectAsync(session, sessions, passive, riderKind, passiveRow, target, now, magnitude: stored).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> ElectricSwordStunAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        IReadOnlyList<ushort> victims,
        DateTimeOffset now)
    {
        if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(SkillRiders.ElectricSwordStun) is not { } stunKind) return true;
        var stunRatio = action.Value(SkillRiders.StunRatioColumn) ?? 0f;
        foreach (var victimId in victims)
        {
            if (monsterManager.Get(victimId) is not { IsDead: false } victim) continue;
            int roll;
            lock (npcLoopRng)
            {
                roll = npcLoopRng.Next(1, 100);
            }
            if (!SkillRiders.Stuns(stunRatio, roll)) continue;
            if (!await StartSkillEffectAsync(session, sessions, castSkill, stunKind, action, victim, now).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> StoredBurnAsync(
        tPoseidon.Core.Game.Skills.Effects.SkillEffect burn,
        MonsterEntity monster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        DateTimeOffset now)
    {
        if (burn.Magnitude < 1
            || sessions.FirstOrDefault(entry => entry.Value.PlayerEntityId == burn.CasterEntityId).Value is not { } burner
            || monsterCatalog.GetById(monster.MonsterParamId) is not { } burnedDefinition
            || !RollsApplyRatio(burn.Action?.Value(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.ApplyRatioColumn) ?? 0f))
        {
            return true;
        }
        var whole = burnedDefinition.Type == SkillRiders.WholeBurnMonsterType;
        if (whole) burn.EndNow();
        var damage = whole ? burn.Magnitude : (int)(burn.Magnitude * (double)burn.Amount);
        return await OpeningStrikeAsync(
            burner, monster, new SkillHit(damage, burn.Kind.StrikeType, SkillDamage.KindHit, burn.Kind.StrikeElement, 0), sessions, now).ConfigureAwait(false);
    }

    (Item Item, ItemParamEntry Definition)? ThrowStackOf(InGameSessionState thrower)
    {
        if (thrower.Character is not { } throwerCharacter) return null;
        return tPoseidon.Core.Game.Items.EquipmentRules.WornBy(itemStore, itemCatalog, throwerCharacter.CharacterId)
            .FirstOrDefault(held => held.Slot == EquipmentSlot.Common && ThrowSkills.IsThrowable(held.Definition)) is { } stack
            ? (stack.Item, stack.Definition)
            : null;
    }

    void SpendStack(Item stack, int count)
    {
        if (count <= 0) return;
        var left = itemStore.AdjustStack(stack.ItemUid, -count);
        Console.WriteLine($"{LogTag} [skill] {count} of item {stack.ItemIndex} thrown, {left} left");
    }

    byte[] StackTail(Item stack)
    {
        var w = new tPoseidon.Core.Net.PacketWriter(10);
        w.WriteUInt16((ushort)stack.ItemIndex);
        w.WriteUInt64(stack.ItemUid);
        return w.ToArray();
    }

    async Task<bool> ThrowSkillAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity target,
        MonsterParamEntry targetDefinition,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if (caster.Stats is not { } throwerStats) return true;
        var attacker = new SkillAttacker(caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, throwerStats.Level, caster.PlayerDa, Hv: caster.PlayerHv);
        var coefficient = action.Value(ThrowSkills.ApplyRatioColumn) ?? 0f;
        var (targetX, targetY) = target.GetInterpolatedPosition(now);
        var tm = caster.GainSkillTmExp is { } gainThrowTm
            ? gainThrowTm(SkillActions.TmExpForUse(castSkill.Row.SkillExp, throwerStats.Level, target.Level))
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();

        if (castSkill.ActionId == ThrowSkills.FanOfKnives)
        {
            if (ThrowStackOf(caster) is not { } knives) return true;
            var angle = action.Value(ThrowSkills.AngleColumn) ?? 0f;
            var limit = Math.Min(action.Amount(ThrowSkills.MonNumColumn) ?? 0, ThrowSkills.FanOfKnivesCap);
            var fanned = MonstersAround(caster, sessions, mapId, (caster.PlayerX, caster.PlayerY), action.Amount(ThrowSkills.RadiusColumn) ?? 0, skipsFlyers: false, now)
                .Where(monster => monster.EntityId != target.EntityId)
                .Where(monster =>
                {
                    var (monsterX, monsterY) = monster.GetInterpolatedPosition(now);
                    return SkillAreaShapes.InCone(caster.PlayerX, caster.PlayerY, targetX, targetY, monsterX, monsterY, angle);
                })
                .Take(Math.Max(0, limit - 1))
                .Prepend(target)
                .ToList();
            var power = ThrowSkills.ThrownPower(knives.Definition, caster.PlayerDa, PowerThrowMultiplierOf(caster), action.Value(ThrowSkills.RatioColumn) ?? 0f);
            SpendStack(knives.Item, fanned.Count);
            var fanTail = new tPoseidon.Core.Net.PacketWriter(12 + 2 * fanned.Count);
            fanTail.WriteUInt16((ushort)fanned.Count);
            foreach (var victim in fanned) fanTail.WriteUInt16(victim.EntityId);
            fanTail.WriteUInt16((ushort)knives.Item.ItemIndex);
            fanTail.WriteUInt64(knives.Item.ItemUid);
            var fanBlock = commandBlockIds.Next();
            if (!await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaStart(caster.PlayerEntityId, castSkill, fanBlock, fanTail.ToArray()),
                caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false))
            {
                return false;
            }
            foreach (var victim in fanned)
            {
                if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } fannedDefinition) continue;
                SkillHit fanHit;
                lock (npcLoopRng)
                {
                    fanHit = SkillDamage.Physical(attacker, power, MonsterTarget(victim, fannedDefinition, SkillActions.NoElement), coefficient, SkillActions.NoElement, npcLoopRng);
                }
                if (!await AreaStrikeAsync(caster, victim, fanHit, fanBlock, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
            }
            return await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaEnd(fanBlock, tm), caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false);
        }

        SkillHit hit;
        byte[] extraTail;
        if (castSkill.ActionId == ThrowSkills.OnePair)
        {
            if (ThrowStackOf(caster) is not { } cards) return true;
            var ladyLuck = caster.Skills.TryGetValue(ThrowSkills.LadyLuck, out var luckLevel)
                ? skillBook.Find(ThrowSkills.LadyLuck)?.ActionAt(luckLevel)
                : null;
            int random;
            lock (npcLoopRng)
            {
                random = npcLoopRng.Next(32768);
            }
            var count = ThrowSkills.CardsFor(ladyLuck, random);
            SpendStack(cards.Item, count);
            var power = ThrowSkills.OnePairPower(action, caster.PlayerAp, caster.PlayerHv, count, ladyLuck is not null);
            lock (npcLoopRng)
            {
                hit = SkillDamage.Physical(attacker, power, MonsterTarget(target, targetDefinition, SkillActions.NoElement), coefficient, SkillActions.NoElement, npcLoopRng);
            }
            var cardTail = new tPoseidon.Core.Net.PacketWriter(12);
            cardTail.WriteBytes(StackTail(cards.Item));
            cardTail.WriteUInt16((ushort)count);
            extraTail = cardTail.ToArray();
        }
        else
        {
            if (throwerStats.Money < ThrowSkills.GalderToll) return true;
            throwerStats.GrantMoney(-ThrowSkills.GalderToll);
            if (!await SendFromLoopAsync(
                new[] { session }, Opcode.S2C_MONEY, GroundItemFrames.Money(throwerStats.Money), MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
            int random;
            lock (npcLoopRng)
            {
                random = npcLoopRng.Next(32768);
            }
            var pay = ThrowSkills.GalderPay(random);
            var power = ThrowSkills.GalderPower(pay, action.Value(ThrowSkills.RatioColumn) ?? 0f);
            lock (npcLoopRng)
            {
                hit = SkillDamage.Physical(
                    attacker, power, MonsterTarget(target, targetDefinition, ThrowSkills.GalderElement) with { Dp = 0 }, coefficient, ThrowSkills.GalderElement, npcLoopRng);
            }
            extraTail = BitConverter.GetBytes((ushort)pay);
        }

        var hpBefore = target.CurrentHp;
        target.AddAttacker(caster.PlayerEntityId);
        target.ApplyDamage(hit.Damage);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: caster.PlayerEntityId, TargetEntityId: target.EntityId,
            AttackerX: caster.PlayerX, AttackerY: caster.PlayerY, TargetX: targetX, TargetY: targetY,
            Damage: hit.Damage, MobHpAfter: target.CurrentHp, HitFlags: 0, HitKind: (uint)hit.Kind, Killed: target.IsDead,
            NextSwingAtUtc: now, MonsterParamId: target.MonsterParamId, MobMaxHp: target.MaxHp, MobHpBefore: hpBefore);
        if (strike.Killed && caster.CurrentTargetEntityId == target.EntityId) caster.CurrentTargetEntityId = 0;
        var (additions, ownerEntityId) = await ResolveStrikeAsync(
            strike, caster, mapId, sessions, zoneGeometry, now, battleEvents, SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
        var positions = SkillFrames.ChainTailBytes(ChainTail.TargetPointsThenSlots, target.EntityId, (caster.PlayerX, caster.PlayerY), (targetX, targetY), Array.Empty<ChainVictim>());
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} throws at 0x{target.EntityId:X4}: damage {hit.Damage} kind {hit.Kind}, hp {target.CurrentHp}/{target.MaxHp}");
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeHit(strike, castSkill, hit, commandBlockIds.Next(), additions, tm, casterTwice: true, tail: [.. positions, .. extraTail]),
            strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<bool> AreaStrikeAsync(
        InGameSessionState striker,
        MonsterEntity victim,
        SkillHit hit,
        ushort blockId,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var hpBefore = victim.CurrentHp;
        victim.AddAttacker(striker.PlayerEntityId);
        victim.ApplyDamage(hit.Damage);
        var (victimX, victimY) = victim.GetInterpolatedPosition(now);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: striker.PlayerEntityId, TargetEntityId: victim.EntityId,
            AttackerX: striker.PlayerX, AttackerY: striker.PlayerY, TargetX: victimX, TargetY: victimY,
            Damage: hit.Damage, MobHpAfter: victim.CurrentHp, HitFlags: 0, HitKind: (uint)hit.Kind, Killed: victim.IsDead,
            NextSwingAtUtc: now, MonsterParamId: victim.MonsterParamId, MobMaxHp: victim.MaxHp, MobHpBefore: hpBefore);
        if (strike.Killed && striker.CurrentTargetEntityId == victim.EntityId) striker.CurrentTargetEntityId = 0;
        var (additions, ownerEntityId) = await ResolveStrikeAsync(
            strike, striker, mapId, sessions, zoneGeometry, now, battleEvents, SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaVictim(strike, hit, blockId, additions),
            strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<bool> FourCardAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (caster.Stats is not { } dealerStats || ThrowStackOf(caster) is not { } cards
            || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(ThrowSkills.FourCardDo) is not { } cardKind)
        {
            return true;
        }
        var victims = MonstersAround(caster, sessions, mapId, (point.X, point.Y), action.Amount(ThrowSkills.RadiusColumn) ?? 0, skipsFlyers: false, now)
            .Take(ThrowSkills.FourCardVictims)
            .ToList();
        var full = victims.Count >= ThrowSkills.FourCardVictims;
        SpendStack(cards.Item, ThrowSkills.FourCardVictims);
        var card = new tPoseidon.Core.Game.Skills.Effects.SkillEffect
        {
            ObjectId = idAllocator.NewId(), Kind = cardKind, SkillId = castSkill.Id, EsClass = -1,
            CasterEntityId = caster.PlayerEntityId, OwnerEntityId = caster.PlayerEntityId, Amount = 0f,
            DurationMs = ThrowSkills.FourCardLifeMs, Mode = tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied, Action = action,
        };
        caster.Effects.Register(card);
        var tail = new tPoseidon.Core.Net.PacketWriter(19 + 2 * victims.Count);
        tail.WriteByte(full ? (byte)1 : (byte)0);
        tail.WriteBytes(StackTail(cards.Item));
        tail.WriteUInt16(point.X);
        tail.WriteUInt16(point.Y);
        tail.WriteUInt16((ushort)victims.Count);
        foreach (var victim in victims) tail.WriteUInt16(victim.EntityId);
        var blockId = commandBlockIds.Next();
        var header = SkillFrames.EffectHeader(
            caster.PlayerEntityId, caster.PlayerEntityId, ThrowSkills.FourCardDo, -1, (ushort)castSkill.Id, 0, card.ObjectId);
        if (!await SendFromLoopAsync(sessions, Opcode.S2C_OP_00E0_TBD, [.. header, .. tail.ToArray()], blockId, npcLoopRng, npcLoopCt).ConfigureAwait(false))
        {
            return false;
        }
        if (full)
        {
            var attacker = new SkillAttacker(caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, dealerStats.Level, caster.PlayerDa);
            var power = ThrowSkills.FourCardPower(action, caster.PlayerAp, caster.PlayerDa);
            foreach (var victim in victims)
            {
                if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } dealtDefinition) continue;
                SkillHit hit;
                lock (npcLoopRng)
                {
                    hit = SkillDamage.Physical(
                        attacker, power, MonsterTarget(victim, dealtDefinition, SkillActions.NoElement),
                        action.Value(ThrowSkills.ApplyRatioColumn) ?? 0f, SkillActions.NoElement, npcLoopRng);
                }
                if (!await AreaStrikeAsync(caster, victim, hit, blockId, sessions, mapId, zoneGeometryByMap.GetValueOrDefault(mapId), now,
                        new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>()).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} four card at ({point.X},{point.Y}): {victims.Count} monster(s), full {full}");
        var tm = full && caster.GainSkillTmExp is { } gainCardTm
            ? gainCardTm(SkillActions.TmExpForUse(castSkill.Row.SkillExp, dealerStats.Level, dealerStats.Level))
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, tm), caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    AreaCandidate ObjectCandidateOf(MonsterEntity monster, DateTimeOffset now)
    {
        var (x, y) = monster.GetInterpolatedPosition(now);
        return new AreaCandidate(monster.EntityId, x, y, monster.IsDead, monsterCatalog.GetById(monster.MonsterParamId)?.Type ?? 0, monster.OwnerEntityId);
    }

    SkillAttacker ObjectAttackerOf(InGameSessionState caster, tPoseidon.Core.Game.Stats.StatManager stats) => new(
        caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, stats.Level,
        caster.PlayerDa, stats.CurrentHp, caster.PlayerMaxHp,
        caster.PlayerHv, caster.PlayerDx, caster.PlayerDp, DarkBonus: DarkBonusOf(caster));

    tPoseidon.Core.Game.Skills.Effects.SkillEffectMode ObjectModeAgainst(
        SkillDefinition castSkill, tPoseidon.Core.LibConfig.SkillTableRow action, MonsterEntity? target)
    {
        var resist = target is not null && castSkill.Row.TargetInput == ObjectHits.ResistedTargetInput
            && monsterCatalog.GetById(target.MonsterParamId) is { } resistingDefinition
            ? tPoseidon.Core.Game.Skills.Effects.SkillResist.Of(monsterProtections.GetValueOrDefault(resistingDefinition.ProtectTable.Trim()), castSkill.Id)
            : 0;
        lock (npcLoopRng)
        {
            return tPoseidon.Core.Game.Skills.Effects.SkillEffectBook.RollMode(action.Value(ObjectHits.ApplyRatioColumn) ?? 0f, resist, npcLoopRng);
        }
    }

    float PowerThrowMultiplierOf(InGameSessionState thrower) =>
        thrower.Effects.Snapshot().Any(held => held.IsApplied && held.Kind.ActionId == tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.PowerThrow)
        && thrower.Skills.TryGetValue(ThrowSkills.PowerThrowSkill, out var throwLevel) && throwLevel > 0
        && skillBook.Find(ThrowSkills.PowerThrowSkill)?.ActionAt(throwLevel) is { } powerThrowRow
            ? powerThrowRow.Value(ThrowSkills.RatioColumn) ?? 1f
            : 1f;

    async Task<tPoseidon.Core.Game.Skills.Effects.SkillEffect?> StartHitObjectAsync(
        InGameSessionState owner,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        int lifeMs,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectMode mode,
        tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit periodic = tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.None,
        int tickMs = 0,
        (ushort X, ushort Y)? point = null,
        long magnitude = 0,
        ushort trackedEntityId = 0,
        sbyte? limiterClass = null)
    {
        var hitObject = new tPoseidon.Core.Game.Skills.Effects.SkillEffect
        {
            ObjectId = idAllocator.NewId(), Kind = ObjectHits.LifeOf(castSkill.ActionId, periodic), SkillId = castSkill.Id,
            EsClass = limiterClass ?? (sbyte)castSkill.Row.EsClass, CasterEntityId = owner.PlayerEntityId, OwnerEntityId = owner.PlayerEntityId,
            Amount = 0f, DurationMs = lifeMs, Mode = mode, Action = action, TickMs = tickMs, Point = point, Magnitude = magnitude,
        };
        if (trackedEntityId != 0) hitObject.Tracked.Add(trackedEntityId);
        if (owner.Effects.Register(hitObject) is not { } registration)
        {
            idAllocator.Release(hitObject.ObjectId);
            Console.WriteLine($"{LogTag} [skill] 0x{owner.PlayerEntityId:X4} has no room for skill {castSkill.Id} (class {hitObject.EsClass})");
            return null;
        }
        foreach (var (removed, _) in registration.Removed)
        {
            if (!await EndSkillEffectAsync(removed, sessions).ConfigureAwait(false)) return null;
        }
        return hitObject;
    }

    byte[] TargetPositions(InGameSessionState caster, MonsterEntity target, (int X, int Y) targetAt) =>
        SkillFrames.ChainTailBytes(ChainTail.TargetPointsThenSlots, target.EntityId, (caster.PlayerX, caster.PlayerY), targetAt, Array.Empty<ChainVictim>());

    static void WriteVictimIds(tPoseidon.Core.Net.PacketWriter tail, IReadOnlyList<ushort> victims)
    {
        tail.WriteUInt16((ushort)victims.Count);
        foreach (var victim in victims) tail.WriteUInt16(victim);
    }

    async Task<bool> ObjectVictimsAsync(
        InGameSessionState caster,
        IReadOnlyList<ushort> victims,
        Func<MonsterEntity, MonsterParamEntry, SkillHit> hitOf,
        ushort blockId,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        foreach (var victimId in victims)
        {
            if (monsterManager.Get(victimId) is not { IsDead: false } victim || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition) continue;
            SkillHit hit;
            lock (npcLoopRng)
            {
                hit = hitOf(victim, victimDefinition);
            }
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} object hit -> 0x{victim.EntityId:X4}: kind={hit.Kind} damage={hit.Damage}");
            if (!await AreaStrikeAsync(caster, victim, hit, blockId, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
        }
        return true;
    }

    IReadOnlyList<(Opcode Opcode, byte[] Payload)> ObjectTmOf(InGameSessionState caster, SkillDefinition castSkill, int casterLevel, int? targetLevel) =>
        caster.GainSkillTmExp is { } gainObjectTm
            ? gainObjectTm(targetLevel is { } scaledAgainst
                ? SkillActions.TmExpForUse(castSkill.Row.SkillExp, casterLevel, scaledAgainst)
                : castSkill.Row.SkillExp)
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();

    SwingFrame ObjectHeaderFrame(
        InGameSessionState caster, ushort secondEntityId, SkillDefinition castSkill, int actionId, ushort objectId, byte[] tail, ushort blockId) =>
        new(Opcode.S2C_OP_00E0_TBD,
            [.. SkillFrames.EffectHeader(caster.PlayerEntityId, secondEntityId, (ushort)actionId, (sbyte)castSkill.Row.EsClass, (ushort)castSkill.Id, (ushort)castSkill.Row.EState, objectId), .. tail],
            blockId, SwingAudience.Everyone);

    async Task<bool> ObjectTargetAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity target,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if (caster.Stats is not { } objectStats) return true;
        var actionId = castSkill.ActionId;
        if (FormSkills.IsTargeted(actionId))
        {
            return await FormTargetAsync(session, sessions, castSkill, action, target, objectStats, mapId, now).ConfigureAwait(false);
        }
        if (ObjectEffects.IsTargeted(actionId))
        {
            return await EffectTargetAsync(session, sessions, castSkill, action, target, objectStats, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
        }
        if (actionId == ObjectHits.FireBurn)
        {
            return await FireBurnAsync(session, sessions, castSkill, action, target, objectStats, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
        }
        if (actionId is ObjectHits.FlashAttacking or ObjectHits.PowerFlashAttack)
        {
            return await FlashAttackAsync(session, sessions, castSkill, action, target, objectStats, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
        }
        (Item Item, ItemParamEntry Definition)? thrown = null;
        if (ObjectHits.ThrowsItem(actionId))
        {
            if (ThrowStackOf(caster) is not { } stack) return true;
            thrown = stack;
            SpendStack(stack.Item, 1);
        }
        if (ObjectHits.PaysGalders(actionId))
        {
            var cost = ObjectHits.GalderCostOf(actionId);
            if (objectStats.Money < cost)
            {
                Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} needs {cost} galders, has {objectStats.Money}");
                return true;
            }
            objectStats.GrantMoney(-cost);
            if (!await SendFromLoopAsync(
                new[] { session }, Opcode.S2C_MONEY, GroundItemFrames.Money(objectStats.Money), MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
        }

        var attacker = ObjectAttackerOf(caster, objectStats);
        var targetAt = target.GetInterpolatedPosition(now);
        var selected = ObjectCandidateOf(target, now);
        var radius = action.Amount(ObjectHits.RadiusColumn) ?? 0;
        var ratio = action.Value(ObjectHits.RatioColumn) ?? 0f;
        var coefficient = action.Value(ObjectHits.ApplyRatioColumn) ?? 0f;
        var tail = new tPoseidon.Core.Net.PacketWriter(32);
        var secondEntityId = caster.PlayerEntityId;
        var mode = tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied;
        var lifeMs = 0;
        var periodic = tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.None;
        var tickMs = 0;
        int? tmAgainst = target.Level;
        IReadOnlyList<ushort> victims;
        Func<MonsterEntity, MonsterParamEntry, SkillHit> hitOf;
        var thunderLanded = new List<ushort>();
        List<AreaCandidate> Around((int X, int Y) centre, bool skipsFlyers) =>
            MonstersAround(caster, sessions, mapId, centre, radius, skipsFlyers, now).Select(monster => ObjectCandidateOf(monster, now)).ToList();

        switch (actionId)
        {
            case ObjectHits.SharpBrandish:
            {
                mode = ObjectModeAgainst(castSkill, action, target);
                victims = ObjectHits.SharpBrandishVictims(
                    caster.PlayerEntityId, (caster.PlayerX, caster.PlayerY), selected, radius, Around((caster.PlayerX, caster.PlayerY), skipsFlyers: true));
                tail.WriteByte((byte)mode);
                tail.WriteBytes(TargetPositions(caster, target, targetAt));
                WriteVictimIds(tail, victims);
                var power = ObjectHits.RatioPower(caster.PlayerAp, ratio);
                hitOf = (victim, definition) => SkillDamage.Physical(
                    attacker, power, MonsterTarget(victim, definition, SkillActions.NoElement), coefficient, SkillActions.NoElement, npcLoopRng);
                lifeMs = ObjectHits.SharpBrandishLifeMs;
                break;
            }
            case ObjectHits.ThunderBolt:
            {
                victims = ObjectHits.TargetThenAround(
                    caster.PlayerEntityId, selected, radius, ObjectHits.LimitOf(action, ObjectHits.ThunderBoltVictims), Around(targetAt, skipsFlyers: true));
                WriteVictimIds(tail, victims);
                secondEntityId = target.EntityId;
                var power = ObjectHits.ElementPower(ElementTotalOf(caster, ElementSlots.Electric), action.Amount(ObjectHits.PowerColumn) ?? 0);
                hitOf = (victim, definition) => SkillDamage.Magic(
                    attacker, power, MonsterTarget(victim, definition, SkillActions.ElectricElement), coefficient,
                    ObjectHits.LightMagicLoss, SkillActions.ElectricElement, npcLoopRng);
                lifeMs = ObjectHits.ThunderBoltLifeMs;
                if (caster.Skills.TryGetValue(ObjectHits.ElectroShockSkill, out var shockLevel) && shockLevel > 0)
                {
                    periodic = tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.ThunderRider;
                    tickMs = ObjectHits.ElectroShockDelayMs;
                    var boltHit = hitOf;
                    hitOf = (victim, definition) =>
                    {
                        var shocked = boltHit(victim, definition);
                        if (shocked.Damage > 0) thunderLanded.Add(victim.EntityId);
                        return shocked;
                    };
                }
                break;
            }
            case ObjectHits.SkyRay:
            {
                mode = ObjectModeAgainst(castSkill, action, target);
                victims = mode == tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied
                    ? ObjectHits.SkyRayVictims(caster.PlayerEntityId, selected, radius, Around(targetAt, skipsFlyers: false))
                    : Array.Empty<ushort>();
                tail.WriteByte((byte)mode);
                WriteVictimIds(tail, victims);
                var power = action.Amount(ObjectHits.PowerColumn) ?? 0;
                hitOf = (victim, definition) => SkillDamage.Magic(
                    attacker, power, MonsterTarget(victim, definition, SkillActions.LightElement), coefficient,
                    ObjectHits.LightMagicLoss, SkillActions.LightElement, npcLoopRng);
                lifeMs = ObjectHits.SkyRayLifeMs;
                tmAgainst = null;
                break;
            }
            case ObjectHits.WeaponThrow when thrown is { } knife:
            {
                victims = new[] { target.EntityId };
                tail.WriteBytes(TargetPositions(caster, target, targetAt));
                tail.WriteBytes(StackTail(knife.Item));
                var power = ThrowSkills.ThrownPower(knife.Definition, caster.PlayerDa, PowerThrowMultiplierOf(caster), ratio);
                hitOf = (victim, definition) => SkillDamage.Physical(
                    attacker, power, MonsterTarget(victim, definition, SkillActions.NoElement), coefficient, SkillActions.NoElement, npcLoopRng);
                lifeMs = ObjectHits.WeaponThrowLifeMs;
                break;
            }
            case ObjectHits.CardThrow when thrown is { } card:
            {
                victims = ObjectHits.Around(caster.PlayerEntityId, targetAt, radius, ObjectHits.CardThrowVictims, skipsFlyers: true, Around(targetAt, skipsFlyers: true));
                tail.WriteBytes(TargetPositions(caster, target, targetAt));
                tail.WriteBytes(StackTail(card.Item));
                var power = ObjectHits.CardPower(caster.PlayerAp, caster.PlayerHv, ratio);
                hitOf = (victim, definition) =>
                {
                    var cardHit = SkillDamage.Physical(
                        attacker, power, MonsterTarget(victim, definition, SkillActions.NoElement), coefficient, SkillActions.NoElement, npcLoopRng);
                    var (victimX, victimY) = victim.GetInterpolatedPosition(now);
                    return cardHit with { Damage = ObjectHits.CardFalloff(cardHit.Damage, SkillAreaRules.Distance(targetAt.x, targetAt.y, victimX, victimY), radius) };
                };
                lifeMs = ObjectHits.UntilTargetGone;
                periodic = tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.WatchTarget;
                tickMs = ObjectHits.WatchTickMs;
                break;
            }
            case ObjectHits.GellderHit or ObjectHits.MultiGellder:
            {
                victims = actionId == ObjectHits.GellderHit
                    ? new[] { target.EntityId }
                    : ObjectHits.Around(caster.PlayerEntityId, targetAt, radius, ObjectHits.MultiGellderVictims, skipsFlyers: true, Around(targetAt, skipsFlyers: true));
                tail.WriteBytes(TargetPositions(caster, target, targetAt));
                var power = ObjectHits.GalderPower(ObjectHits.GalderCostOf(actionId), ratio);
                hitOf = (victim, definition) => SkillDamage.Physical(
                    attacker, power, MonsterTarget(victim, definition, ObjectHits.GellderElement) with { Dp = 0 }, coefficient,
                    ObjectHits.GellderElement, npcLoopRng);
                lifeMs = ObjectHits.GellderLifeMs;
                break;
            }
            default:
                return true;
        }

        if (await StartHitObjectAsync(caster, sessions, castSkill, action, lifeMs, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied,
                periodic, tickMs, trackedEntityId: target.EntityId).ConfigureAwait(false) is not { } hitObject)
        {
            return true;
        }
        var blockId = commandBlockIds.Next();
        if (!await SendStrikeFramesAsync(
            new[] { ObjectHeaderFrame(caster, secondEntityId, castSkill, actionId, hitObject.ObjectId, tail.ToArray(), blockId) },
            caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (!await ObjectVictimsAsync(caster, victims, hitOf, blockId, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
        if (periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.ThunderRider)
        {
            hitObject.Tracked.Clear();
            foreach (var landed in thunderLanded) hitObject.Tracked.Add(landed);
        }
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} (action {actionId}) mode {mode}: {victims.Count} monster(s)");
        var tm = actionId == ObjectHits.SkyRay && mode != tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied
            ? Array.Empty<(Opcode Opcode, byte[] Payload)>()
            : ObjectTmOf(caster, castSkill, objectStats.Level, tmAgainst);
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, tm), caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (actionId == ObjectHits.SharpBrandish && !target.IsDead)
        {
            return await StartAttackAfterSkillAsync(session, sessions, target.EntityId, SkillActions.AttackStartDelayMs, now).ConfigureAwait(false);
        }
        return true;
    }

    async Task<bool> FireBurnAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity target,
        tPoseidon.Core.Game.Stats.StatManager burnerStats,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if (monsterCatalog.GetById(target.MonsterParamId) is not { } targetDefinition
            || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(ObjectHits.FireBurnDo) is not { } burnKind)
        {
            return true;
        }
        var targetAt = target.GetInterpolatedPosition(now);
        var radius = action.Amount(ObjectHits.RadiusColumn) ?? 0;
        var victims = MonstersAround(caster, sessions, mapId, targetAt, radius, skipsFlyers: false, now).Take(ObjectHits.FireBurnVictims).ToList();
        if (victims.Count == 0) return true;
        bool landed;
        lock (npcLoopRng)
        {
            landed = ObjectHits.Lands(
                caster.PlayerLk, targetDefinition.LkLv + target.StatDelta(MonsterStat.Lk),
                action.Value(ObjectHits.ApplyRatioColumn) ?? 0f, npcLoopRng.Next(100));
        }
        var tail = new tPoseidon.Core.Net.PacketWriter(7);
        tail.WriteByte(landed ? (byte)1 : (byte)0);
        tail.WriteUInt16((ushort)radius);
        tail.WriteUInt16((ushort)targetAt.x);
        tail.WriteUInt16((ushort)targetAt.y);
        var blockId = commandBlockIds.Next();
        var frames = new List<SwingFrame> { ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, SkillFrames.NoEffectObject, tail.ToArray(), blockId) };
        if (!landed)
        {
            foreach (var missed in victims)
            {
                frames.Add(new(Opcode.S2C_OP_002A_TBD,
                    SkillFrames.HitResult(missed.EntityId, new SkillHit(0, ObjectHits.DefaultResultType, SkillDamage.KindMiss, SkillActions.NoElement, 0)),
                    blockId, SwingAudience.Everyone));
            }
        }
        frames.AddRange(SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>()));
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} fire burn on 0x{target.EntityId:X4}: {victims.Count} monster(s), landed {landed}");
        if (!await SendStrikeFramesAsync(frames, caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false)) return false;
        if (!landed) return true;

        var attacker = ObjectAttackerOf(caster, burnerStats);
        var power = ObjectHits.ElementPower(ElementTotalOf(caster, ElementSlots.Fire), action.Amount(ObjectHits.PowerColumn) ?? 0);
        foreach (var victim in victims)
        {
            if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } burnedDefinition) continue;
            SkillHit rolled;
            lock (npcLoopRng)
            {
                rolled = SkillDamage.Magic(
                    attacker, power, MonsterTarget(victim, burnedDefinition, SkillActions.FireElement), 1f, ObjectHits.MagicLoss, SkillActions.FireElement, npcLoopRng);
            }
            if (!await StartSkillEffectAsync(session, sessions, castSkill, burnKind, action, victim, now, magnitude: rolled.Damage).ConfigureAwait(false)) return false;
            if (!await OpeningStrikeAsync(
                caster, victim, new SkillHit(rolled.Damage, SkillDamage.MagicType, SkillDamage.KindHit, SkillActions.FireElement, 0), sessions, now).ConfigureAwait(false))
            {
                return false;
            }
        }
        return await SendStrikeFramesAsync(
            ObjectTmOf(caster, castSkill, burnerStats.Level, null)
                .Select(tmFrame => new SwingFrame(tmFrame.Opcode, tmFrame.Payload, MeleeSwingFrames.NoBlock, SwingAudience.Attacker))
                .ToList(),
            caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<bool> FixedBurnAsync(
        tPoseidon.Core.Game.Skills.Effects.SkillEffect burn,
        MonsterEntity monster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        DateTimeOffset now)
    {
        if (burn.ElapsedMs >= burn.DurationMs
            || sessions.FirstOrDefault(entry => entry.Value.PlayerEntityId == burn.CasterEntityId).Value is not { } burner
            || monsterCatalog.GetById(monster.MonsterParamId) is not { } burnedDefinition)
        {
            return true;
        }
        if (burnedDefinition.Type == SkillRiders.WholeBurnMonsterType) burn.EndNow();
        return await OpeningStrikeAsync(
            burner, monster, new SkillHit(burn.Magnitude, SkillDamage.MagicType, SkillDamage.KindHit, SkillActions.FireElement, 0), sessions, now).ConfigureAwait(false);
    }

    async Task<bool> FlashAttackAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity target,
        tPoseidon.Core.Game.Stats.StatManager flashStats,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if ((PlayerConditionOf(caster) & MonsterSkills.StunnedCondition) != 0)
        {
            return await SendFromLoopAsync(
                new[] { session }, Opcode.S2C_SKILL_REFUSED, SkillFrames.Refused(0), MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
        }
        var targetAt = target.GetInterpolatedPosition(now);
        var restoredSpeed = playerMoveSpeeds is { } flashSpeeds
            ? SpeedOfMoveType(flashSpeeds, tPoseidon.Core.Game.Combat.MeleeApproach.RunMoveType, caster.GameMasterSpeed)
            : 0f;
        var distance = SkillAreaRules.Distance(caster.PlayerX, caster.PlayerY, targetAt.x, targetAt.y);
        var arrivalMs = distance >= ObjectHits.FlashArrivalDistance
            ? (int)Math.Ceiling((distance - ObjectHits.FlashArrivalDistance) / ObjectHits.FlightSpeed(castSkill.ActionId) * 1000.0)
            : 0;
        if (await StartHitObjectAsync(caster, sessions, castSkill, action, arrivalMs, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied,
                tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.FlashArrival, arrivalMs, ((ushort)targetAt.x, (ushort)targetAt.y),
                trackedEntityId: target.EntityId).ConfigureAwait(false) is not { } flight)
        {
            return true;
        }
        var tail = new tPoseidon.Core.Net.PacketWriter(8);
        tail.WriteUInt32(BitConverter.SingleToUInt32Bits(restoredSpeed));
        tail.WriteUInt16((ushort)targetAt.x);
        tail.WriteUInt16((ushort)targetAt.y);
        var blockId = commandBlockIds.Next();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} flash attack at 0x{target.EntityId:X4}: distance {distance:F0}, arrives in {arrivalMs} ms");
        if (!await SendStrikeFramesAsync(
            [ObjectHeaderFrame(caster, target.EntityId, castSkill, castSkill.ActionId, flight.ObjectId, tail.ToArray(), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, ObjectTmOf(caster, castSkill, flashStats.Level, null))],
            caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (arrivalMs > 0) return true;
        if (!await FlashArriveAsync(session, flight, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
        return !caster.Effects.Remove(flight) || await EndSkillEffectAsync(flight, sessions).ConfigureAwait(false);
    }

    async Task<bool> FlashArriveAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect flight,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        flight.EndNow();
        if (flight.Action is not { } flashRow || flight.Point is not { } aimedAt || caster.Stats is not { } flashStats
            || skillBook.Find(flight.SkillId) is not { } flashSkill
            || flight.Tracked.FirstOrDefault() is var trackedId && monsterManager.Get(trackedId) is not { IsDead: false } target
            || monsterCatalog.GetById(target.MonsterParamId) is not { } targetDefinition)
        {
            return true;
        }
        var (stopX, stopY) = SkillChecks.ChaseEnd(ObjectHits.FlashArrivalDistance, 0f, caster.PlayerX, caster.PlayerY, aimedAt.X, aimedAt.Y);
        caster.PlayerX = (ushort)stopX;
        caster.PlayerY = (ushort)stopY;
        var targetAt = target.GetInterpolatedPosition(now);
        if (SkillAreaRules.Distance(stopX, stopY, targetAt.x, targetAt.y) > ObjectHits.FlashArrivalDistance) return true;
        var hits = RollsApplyRatio(flashRow.Value(ObjectHits.ApplyRatioColumn) ?? 0f);
        var blockId = commandBlockIds.Next();
        var tail = new[] { hits ? (byte)1 : (byte)0 };
        var header = new SwingFrame(Opcode.S2C_OP_00E0_TBD,
            [.. SkillFrames.EffectHeader(caster.PlayerEntityId, target.EntityId, ObjectHits.FlashAttackingDo, -1, (ushort)flashSkill.Id, 0), .. tail],
            blockId, SwingAudience.Everyone);
        if (!await SendStrikeFramesAsync(new[] { header }, caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false)) return false;
        if (hits)
        {
            var power = ObjectHits.FlashPower(caster.PlayerAp, caster.PlayerAc, flashSkill.ActionId == ObjectHits.PowerFlashAttack);
            SkillHit hit;
            lock (npcLoopRng)
            {
                hit = SkillDamage.Physical(
                    ObjectAttackerOf(caster, flashStats), power, MonsterTarget(target, targetDefinition, SkillActions.NoElement),
                    flashRow.Value(ObjectHits.RatioColumn) ?? 0f, SkillActions.NoElement, npcLoopRng);
            }
            if (!await AreaStrikeAsync(caster, target, hit, blockId, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
        }
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} flash attack arrives at 0x{target.EntityId:X4}: hits {hits}");
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
            caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<bool> ObjectSelfAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if (caster.Stats is not { } selfStats) return true;
        if (ObjectEffects.IsSelf(castSkill.ActionId))
        {
            return await EffectSelfAsync(session, sessions, castSkill, action, mapId, now).ConfigureAwait(false);
        }
        if (castSkill.ActionId == UtilitySkills.BasicDetector)
        {
            return await DetectorAsync(session, castSkill, action).ConfigureAwait(false);
        }
        if (KnockSkills.IsSelf(castSkill.ActionId))
        {
            return await KnockAsync(session, sessions, castSkill, action, mapId, now).ConfigureAwait(false);
        }
        if (castSkill.ActionId == ScrollSkills.WideHealing)
        {
            return await WideHealingAsync(session, sessions, castSkill, action).ConfigureAwait(false);
        }
        var at = (caster.PlayerX, caster.PlayerY);
        var radius = action.Amount(ObjectHits.RadiusColumn) ?? 0;
        var tail = new tPoseidon.Core.Net.PacketWriter(32);
        var blockId = commandBlockIds.Next();
        switch (castSkill.ActionId)
        {
            case ObjectHits.WindSlash:
            {
                var mode = ObjectModeAgainst(castSkill, action, null);
                var victims = ObjectHits.Around(
                    caster.PlayerEntityId, at, radius, ObjectHits.LimitOf(action, ObjectHits.WindSlashVictims), skipsFlyers: true,
                    MonstersAround(caster, sessions, mapId, at, radius, skipsFlyers: true, now).Select(monster => ObjectCandidateOf(monster, now)));
                tail.WriteByte((byte)mode);
                WriteVictimIds(tail, victims);
                if (await StartHitObjectAsync(caster, sessions, castSkill, action, ObjectHits.WindSlashLifeMs, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied)
                        .ConfigureAwait(false) is not { } slash)
                {
                    return true;
                }
                if (!await SendStrikeFramesAsync(
                    new[] { ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, slash.ObjectId, tail.ToArray(), blockId) },
                    caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
                {
                    return false;
                }
                var attacker = ObjectAttackerOf(caster, selfStats);
                var power = ObjectHits.ElementPower(ElementTotalOf(caster, ElementSlots.Wind), action.Amount(ObjectHits.PowerColumn) ?? 0);
                var coefficient = action.Value(ObjectHits.ApplyRatioColumn) ?? 0f;
                if (!await ObjectVictimsAsync(
                    caster, victims,
                    (victim, definition) => SkillDamage.Magic(
                        attacker, power, MonsterTarget(victim, definition, SkillActions.WindElement), coefficient,
                        ObjectHits.MagicLoss, SkillActions.WindElement, npcLoopRng),
                    blockId, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false))
                {
                    return false;
                }
                Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} wind slash mode {mode}: {victims.Count} monster(s)");
                return await SendStrikeFramesAsync(
                    SkillFrames.EncodeAreaEnd(blockId, ObjectTmOf(caster, castSkill, selfStats.Level, null)),
                    caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
            }
            case ObjectHits.DarkWhisper:
            {
                var mode = ObjectModeAgainst(castSkill, action, null);
                var lifeMs = (action.Amount(ObjectHits.SecondsColumn) ?? 0) * tPoseidon.Core.Game.Skills.Effects.SkillEffect.MillisecondsPerSecond;
                if (await StartHitObjectAsync(caster, sessions, castSkill, action, lifeMs, mode,
                        tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.DarkWhisperRound, ObjectHits.DarkWhisperFirstMs, at)
                        .ConfigureAwait(false) is not { } whisper)
                {
                    return true;
                }
                tail.WriteByte((byte)mode);
                tail.WriteUInt16(at.PlayerX);
                tail.WriteUInt16(at.PlayerY);
                tail.WriteUInt16((ushort)radius);
                return await SendObjectStartAsync(caster, sessions, castSkill, whisper, tail.ToArray(), blockId, mode, selfStats.Level).ConfigureAwait(false);
            }
            case ObjectHits.Kamikaze:
            {
                var boomMs = (action.Amount(ObjectHits.AfterBoomColumn) ?? 0) * tPoseidon.Core.Game.Skills.Effects.SkillEffect.MillisecondsPerSecond;
                if (await StartHitObjectAsync(caster, sessions, castSkill, action, boomMs, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied,
                        tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.KamikazeBlast, boomMs).ConfigureAwait(false) is not { } fuse)
                {
                    return true;
                }
                tail.WriteInt32(boomMs);
                return await SendObjectStartAsync(
                    caster, sessions, castSkill, fuse, tail.ToArray(), blockId, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied, selfStats.Level).ConfigureAwait(false);
            }
        }
        return true;
    }

    async Task<bool> SendObjectStartAsync(
        InGameSessionState caster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect hitObject,
        byte[] tail,
        ushort blockId,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectMode mode,
        int casterLevel)
    {
        var tm = mode == tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied
            ? ObjectTmOf(caster, castSkill, casterLevel, null)
            : Array.Empty<(Opcode Opcode, byte[] Payload)>();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} object 0x{hitObject.ObjectId:X4} mode {mode}, {hitObject.DurationMs} ms");
        return await SendStrikeFramesAsync(
            [ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, hitObject.ObjectId, tail, blockId), .. SkillFrames.EncodeAreaEnd(blockId, tm)],
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> ObjectGroundAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (caster.Stats is not { } groundStats) return true;
        if (castSkill.ActionId == ObjectEffects.DeadlyFunk)
        {
            return await DeadlyFunkAsync(session, sessions, action, point, mapId, now).ConfigureAwait(false);
        }
        if (UtilitySkills.IsGround(castSkill.ActionId))
        {
            return await UtilityGroundAsync(session, sessions, castSkill, action, point, groundStats).ConfigureAwait(false);
        }
        if (castSkill.ActionId == ScrollSkills.PartyDetector)
        {
            return await PartyDetectorAsync(session, sessions, castSkill, action, point).ConfigureAwait(false);
        }
        if (castSkill.ActionId == FormSkills.UpThrow)
        {
            return await UpThrowAsync(session, sessions, castSkill, action, point, groundStats, mapId, now).ConfigureAwait(false);
        }
        var radius = action.Amount(ObjectHits.RadiusColumn) ?? 0;
        var blockId = commandBlockIds.Next();
        var tail = new tPoseidon.Core.Net.PacketWriter(16);
        if (castSkill.ActionId == ObjectHits.GaleBreak)
        {
            var mode = ObjectModeAgainst(castSkill, action, null);
            var delayMs = (action.Amount(ObjectHits.SecondsColumn) ?? 0) * tPoseidon.Core.Game.Skills.Effects.SkillEffect.MillisecondsPerSecond;
            var power = (short)ObjectHits.ElementPower(ElementTotalOf(caster, ElementSlots.Wind), action.Amount(ObjectHits.MagicPowerColumn) ?? 0);
            if (await StartHitObjectAsync(caster, sessions, castSkill, action, delayMs + ObjectHits.GaleBreakLingerMs, mode,
                    tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.GaleBurst, delayMs, point, power).ConfigureAwait(false) is not { } gale)
            {
                return true;
            }
            tail.WriteByte((byte)mode);
            tail.WriteUInt16(point.X);
            tail.WriteUInt16(point.Y);
            tail.WriteUInt16((ushort)radius);
            tail.WriteInt32(delayMs);
            return await SendObjectStartAsync(caster, sessions, castSkill, gale, tail.ToArray(), blockId, mode, groundStats.Level).ConfigureAwait(false);
        }

        var victims = MonstersAround(caster, sessions, mapId, (point.X, point.Y), radius, skipsFlyers: false, now)
            .Take(ObjectHits.CelestialVictims)
            .Select(monster => monster.EntityId)
            .ToList();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} celestial strike at ({point.X},{point.Y}): {victims.Count} monster(s)");
        if (victims.Count == 0) return true;
        tail.WriteByte((byte)victims.Count);
        foreach (var victim in victims) tail.WriteUInt16(victim);
        if (!await SendStrikeFramesAsync(
            new[] { ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, SkillFrames.NoEffectObject, tail.ToArray(), blockId) },
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }
        var attacker = ObjectAttackerOf(caster, groundStats);
        var celestialPower = ObjectHits.CelestialPower(action.Amount(ObjectHits.PowerColumn) ?? 0, ObjectHits.NoCelestialScan);
        var coefficient = action.Value(ObjectHits.ApplyRatioColumn) ?? 0f;
        var zoneGeometry = zoneGeometryByMap.GetValueOrDefault(mapId);
        if (!await ObjectVictimsAsync(
            caster, victims,
            (victim, definition) => SkillDamage.Magic(
                attacker, celestialPower, MonsterTarget(victim, definition, SkillActions.LightElement), coefficient,
                ObjectHits.LightMagicLoss, SkillActions.LightElement, npcLoopRng),
            blockId, sessions, mapId, zoneGeometry, now, new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>()).ConfigureAwait(false))
        {
            return false;
        }
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, ObjectTmOf(caster, castSkill, groundStats.Level, null)),
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> HitObjectTickAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect hitObject,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        var owner = session.Value;
        var zoneGeometry = zoneGeometryByMap.GetValueOrDefault(mapId);
        var battleEvents = new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>();
        switch (hitObject.Kind.Periodic)
        {
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.WatchTarget:
                if (monsterManager.Get(hitObject.Tracked.FirstOrDefault()) is not { IsDead: false }) hitObject.EndNow();
                return true;
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.FlashArrival:
                return await FlashArriveAsync(session, hitObject, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.ElectricFieldRound:
                return await ElectricFieldRoundAsync(owner, hitObject, sessions, mapId, now).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.FullHouseBurst:
                return await FullHouseBurstAsync(session, hitObject, sessions, mapId, now).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.RevealScan:
                return await RevealScanAsync(session, hitObject, sessions, mapId, now).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.MineWatch:
                return await MineWatchAsync(owner, hitObject, sessions, mapId, now).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.ThunderRider:
            {
                if (hitObject.PeriodCount++ > 0
                    || !owner.Skills.TryGetValue(ObjectHits.ElectroShockSkill, out var riderLevel)
                    || skillBook.Find(ObjectHits.ElectroShockSkill) is not { } shockSkill
                    || (shockSkill.ActionAt(riderLevel) ?? shockSkill.ActionAt(SkillDefinition.LowestLevel)) is not { } shockRow
                    || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(ObjectHits.ElectroShock) is not { } shockKind)
                {
                    return true;
                }
                foreach (var shockedId in hitObject.Tracked.ToList())
                {
                    if (monsterManager.Get(shockedId) is not { IsDead: false } shocked) continue;
                    if (!await StartSkillEffectAsync(session, sessions, shockSkill, shockKind, shockRow, shocked, now).ConfigureAwait(false)) return false;
                }
                return true;
            }
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.MorphDrain:
                return await MorphDrainAsync(session, hitObject).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.SoldierStrike:
                return await SoldierStrikeAsync(owner, hitObject, sessions, mapId, now).ConfigureAwait(false);
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.DashArrival:
            {
                if (hitObject.Point is { } dashTo)
                {
                    var (dashX, dashY) = UtilitySkills.DashEnd((owner.PlayerX, owner.PlayerY), (dashTo.X, dashTo.Y), hitObject.TickMs);
                    owner.PlayerX = (ushort)dashX;
                    owner.PlayerY = (ushort)dashY;
                }
                hitObject.EndNow();
                return true;
            }
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.DarkWhisperRound:
            {
                hitObject.PeriodCount++;
                if (hitObject.Point is not { } whisperAt || owner.PlayerX != whisperAt.X || owner.PlayerY != whisperAt.Y)
                {
                    hitObject.EndNow();
                    return true;
                }
                if (!ObjectHits.DarkWhisperRoundDue(hitObject.PeriodCount) || hitObject.Action is not { } whisperRow || owner.Stats is not { } whisperStats) return true;
                var victims = MonstersAround(
                        owner, sessions, mapId, (whisperAt.X, whisperAt.Y), whisperRow.Amount(ObjectHits.RadiusColumn) ?? 0, skipsFlyers: false, now)
                    .Take(ObjectHits.LimitOf(whisperRow, ObjectHits.DarkWhisperVictims))
                    .ToList();
                if (victims.Count == 0) return true;
                if (!await SendFromLoopAsync(
                    sessions, Opcode.S2C_OBJECT_VICTIMS, SkillFrames.ObjectVictims(hitObject.ObjectId, victims.Select(victim => victim.EntityId).ToList()),
                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                {
                    return false;
                }
                var attacker = ObjectAttackerOf(owner, whisperStats);
                var power = whisperRow.Amount(ObjectHits.DarkPowerColumn) ?? 0;
                var coefficient = whisperRow.Value(ObjectHits.ApplyRatioColumn) ?? 0f;
                foreach (var victim in victims)
                {
                    if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } whisperedDefinition) continue;
                    SkillHit hit;
                    lock (npcLoopRng)
                    {
                        hit = SkillDamage.Magic(
                            attacker, power, MonsterTarget(victim, whisperedDefinition, SkillActions.DarkElement), coefficient,
                            ObjectHits.MagicLoss, SkillActions.DarkElement, npcLoopRng);
                    }
                    if (!await PeriodicStrikeAsync(owner, victim, hit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
                }
                return true;
            }
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.GaleBurst:
            {
                if (hitObject.PeriodCount++ > 0 || hitObject.Point is not { } galeAt || hitObject.Action is not { } galeRow || owner.Stats is not { } galeStats) return true;
                var victims = MonstersAround(owner, sessions, mapId, (galeAt.X, galeAt.Y), galeRow.Amount(ObjectHits.RadiusColumn) ?? 0, skipsFlyers: false, now)
                    .Take(ObjectHits.GaleBreakVictims)
                    .Take(Math.Max(0, galeRow.Amount(ObjectHits.KillNumColumn) ?? 0))
                    .ToList();
                var attacker = ObjectAttackerOf(owner, galeStats);
                var coefficient = galeRow.Value(ObjectHits.ApplyRatioColumn) ?? 0f;
                Console.WriteLine($"{LogTag} [skill] 0x{owner.PlayerEntityId:X4} gale break bursts at ({galeAt.X},{galeAt.Y}): {victims.Count} monster(s)");
                foreach (var victim in victims)
                {
                    if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } galeDefinition) continue;
                    SkillHit hit;
                    lock (npcLoopRng)
                    {
                        hit = SkillDamage.Magic(
                            attacker, hitObject.Magnitude, MonsterTarget(victim, galeDefinition, SkillActions.WindElement), coefficient,
                            ObjectHits.MagicLoss, SkillActions.WindElement, npcLoopRng);
                    }
                    if (!await PeriodicStrikeAsync(owner, victim, hit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
                }
                return true;
            }
            case tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.KamikazeBlast:
            {
                hitObject.EndNow();
                if (hitObject.PeriodCount++ > 0 || hitObject.Action is not { } boomRow || owner.Stats is not { } boomStats) return true;
                var (lost, victimDamage) = ObjectHits.KamikazeBlast(
                    boomStats.CurrentHp,
                    boomRow.Value(ObjectHits.HpRemainColumn) ?? 0f, boomRow.Value(ObjectHits.RatioColumn) ?? 0f);
                boomStats.SetVitals(Math.Max(0L, boomStats.CurrentHp - lost), boomStats.CurrentMp);
                var victims = MonstersAround(owner, sessions, mapId, (owner.PlayerX, owner.PlayerY), boomRow.Amount(ObjectHits.RadiusColumn) ?? 0, skipsFlyers: false, now)
                    .Take(ObjectHits.LimitOf(boomRow, ObjectHits.KamikazeVictims))
                    .ToList();
                var blockId = commandBlockIds.Next();
                Console.WriteLine($"{LogTag} [skill] 0x{owner.PlayerEntityId:X4} kamikaze: loses {lost} HP, {victims.Count} monster(s) take {victimDamage}");
                if (!await SendStrikeFramesAsync(
                    new[]
                    {
                        new SwingFrame(Opcode.S2C_OP_002A_TBD,
                            SkillFrames.HitResult(owner.PlayerEntityId, new SkillHit(lost, SkillDamage.PhysicalType, SkillDamage.KindHit, SkillActions.NoElement, 0)),
                            blockId, SwingAudience.Everyone),
                    },
                    owner.PlayerEntityId, owner.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false)
                    || !await SendFromLoopAsync(
                        new[] { session }, Opcode.S2C_HEALTH_STATUS,
                        tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(boomStats.CurrentHp).EncodePayload(),
                        MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                {
                    return false;
                }
                foreach (var victim in victims)
                {
                    if (victim.IsDead) continue;
                    var blast = new SkillHit(victimDamage, SkillDamage.PhysicalType, SkillDamage.KindHit, SkillActions.NoElement, 0, ObjectHits.KamikazeFlags);
                    if (!await AreaStrikeAsync(owner, victim, blast, blockId, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
                }
                return await SendStrikeFramesAsync(
                    SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                    owner.PlayerEntityId, owner.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
            }
        }
        return true;
    }

    async Task<(bool Sent, tPoseidon.Core.Game.Skills.Effects.SkillEffect? Effect)> StartMonsterObjectAsync(
        InGameSessionState caster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKind kind,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity victim,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectMode mode,
        long magnitude,
        ushort blockId,
        (ushort X, ushort Y)? point = null,
        bool checksDebuff = true)
    {
        if (checksDebuff)
        {
            var (resistSent, resisted) = await DebuffResistedAsync(castSkill, victim, sessions).ConfigureAwait(false);
            if (!resistSent || resisted) return (resistSent, null);
        }
        var book = MonsterEffectsOf(victim.EntityId);
        var effect = new tPoseidon.Core.Game.Skills.Effects.SkillEffect
        {
            ObjectId = idAllocator.NewId(), Kind = kind, SkillId = castSkill.Id, EsClass = kind.HeaderClass ?? (sbyte)castSkill.Row.EsClass,
            CasterEntityId = caster.PlayerEntityId, OwnerEntityId = victim.EntityId, Amount = action.Value(kind.AmountColumn) ?? 0f,
            DurationMs = kind.FixedDurationMs ?? (int)Math.Clamp((long)(action.Amount(kind.DurationColumn) ?? 0) * kind.MillisecondsPerUnit, 0, int.MaxValue),
            TickMs = kind.TickColumn is { } tickColumn ? (action.Amount(tickColumn) ?? 0) * kind.TickScale : kind.PeriodMs,
            Mode = mode, Action = action, Magnitude = magnitude, Point = point,
        };
        if (book.Register(effect) is not { } registration)
        {
            idAllocator.Release(effect.ObjectId);
            Console.WriteLine($"{LogTag} [skill] 0x{victim.EntityId:X4} has no room for skill {castSkill.Id} (class {effect.EsClass})");
            return (true, null);
        }
        foreach (var (removed, _) in registration.Removed)
        {
            if (!await EndSkillEffectAsync(removed, sessions).ConfigureAwait(false)) return (false, null);
            UndoMonsterEffect(victim, removed);
        }
        var sent = await SendFromLoopAsync(
            sessions, Opcode.S2C_OP_00E0_TBD, SkillEffectFrames.Started(effect, victim.EntityId), blockId, npcLoopRng, npcLoopCt).ConfigureAwait(false);
        return (sent, effect);
    }

    async Task<bool> HoldMonsterAsync(MonsterEntity monster, int holdMs, List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions, DateTimeOffset now)
    {
        var (heldX, heldY) = monster.HoldUntil(now.AddMilliseconds(holdMs), now);
        return await SendFromLoopAsync(
            sessions, Opcode.S2C_OP_0019_TBD,
            new Packet0019Tbd { EntityId = monster.EntityId, WorldX = heldX, WorldY = heldY, StateFlag = monster.MoveType }.EncodePayload(),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
    }

    async Task<bool> EffectTargetAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity target,
        tPoseidon.Core.Game.Stats.StatManager stats,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var caster = session.Value;
        if (monsterCatalog.GetById(target.MonsterParamId) is not { } targetDefinition) return true;
        var targetAt = target.GetInterpolatedPosition(now);
        var attacker = ObjectAttackerOf(caster, stats);
        var ratio = action.Value(ObjectEffects.RatioColumn) ?? 0f;
        var coefficient = action.Value(ObjectEffects.ApplyRatioColumn) ?? 0f;
        var blockId = commandBlockIds.Next();
        switch (castSkill.ActionId)
        {
            case ObjectEffects.ElectricField:
            {
                var mode = ObjectModeAgainst(castSkill, action, target);
                var lifeMs = (action.Amount(ObjectEffects.SecondsColumn) ?? 0) * tPoseidon.Core.Game.Skills.Effects.SkillEffect.MillisecondsPerSecond;
                var radius = action.Amount(ObjectEffects.RadiusColumn) ?? 0;
                if (await StartHitObjectAsync(caster, sessions, castSkill, action, lifeMs, mode,
                        tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.ElectricFieldRound, ObjectEffects.ElectricFieldPeriodMs,
                        (targetAt.x, targetAt.y), radius, limiterClass: -1).ConfigureAwait(false) is not { } field)
                {
                    return true;
                }
                var tail = new tPoseidon.Core.Net.PacketWriter(7);
                tail.WriteByte((byte)mode);
                tail.WriteUInt16((ushort)radius);
                tail.WriteUInt16(targetAt.x);
                tail.WriteUInt16(targetAt.y);
                Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} electric field at ({targetAt.x},{targetAt.y}) mode {mode}, {lifeMs} ms");
                return await SendStrikeFramesAsync(
                    [ObjectHeaderFrame(caster, target.EntityId, castSkill, castSkill.ActionId, field.ObjectId, tail.ToArray(), blockId),
                     .. SkillFrames.EncodeAreaEnd(blockId, mode == tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied
                         ? ObjectTmOf(caster, castSkill, stats.Level, null)
                         : Array.Empty<(Opcode Opcode, byte[] Payload)>())],
                    caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false);
            }
            case ObjectEffects.SharpClaw:
            {
                if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(ObjectEffects.SharpClaw) is not { } clawKind) return true;
                var rolled = ObjectModeAgainst(castSkill, action, target);
                SkillHit hit;
                lock (npcLoopRng)
                {
                    hit = SkillDamage.Physical(
                        attacker, ObjectHits.RatioPower(caster.PlayerAp, ratio), MonsterTarget(target, targetDefinition, SkillActions.NoElement),
                        coefficient, SkillActions.NoElement, npcLoopRng);
                }
                var mode = ObjectEffects.SharpClawMode(hit.Kind, rolled);
                var (sent, claw) = await StartMonsterObjectAsync(caster, sessions, castSkill, clawKind, action, target, mode, hit.Damage, blockId).ConfigureAwait(false);
                if (!sent) return false;
                if (claw is null) return true;
                if (!await AreaStrikeAsync(caster, target, hit, blockId, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
                Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} sharp claw on 0x{target.EntityId:X4}: damage {hit.Damage} kind {hit.Kind}, mode {mode}");
                return await SendStrikeFramesAsync(
                    SkillFrames.EncodeAreaEnd(blockId, mode == tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied
                        ? ObjectTmOf(caster, castSkill, stats.Level, target.Level)
                        : Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                    caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false);
            }
            case ObjectEffects.PoisonBaggage or ObjectEffects.PoisonAssault:
            {
                var assault = castSkill.ActionId == ObjectEffects.PoisonAssault;
                var stack = ThrowStackOf(caster);
                if (assault && stack is null) return true;
                var weaponPower = stack is { } weapon ? ObjectEffects.ThrownWeaponPower(weapon.Definition, PowerThrowMultiplierOf(caster)) : 0;
                var power = assault
                    ? ObjectEffects.PoisonAssaultPower(weaponPower, caster.PlayerDa, ratio)
                    : ObjectEffects.WeightPower(
                        CarriedWeightOf(caster),
                        caster.Skills.ContainsKey(ObjectEffects.PoisonMasterySkill)
                            ? skillBook.Find(ObjectEffects.PoisonMasterySkill)?.ActionAt(ObjectEffects.PoisonMasteryRowLevel)?.Value(ObjectEffects.RatioColumn) ?? 0f
                            : 0f);
                SkillHit hit;
                lock (npcLoopRng)
                {
                    hit = SkillDamage.Physical(
                        attacker, power, MonsterTarget(target, targetDefinition, SkillActions.NoElement), coefficient, SkillActions.NoElement, npcLoopRng);
                }
                var tail = new tPoseidon.Core.Net.PacketWriter(20);
                tail.WriteBytes(TargetPositions(caster, target, targetAt));
                if (assault && stack is { } thrownStack) tail.WriteBytes(StackTail(thrownStack.Item));
                if (!await SendStrikeFramesAsync(
                    new[] { ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, SkillFrames.NoEffectObject, tail.ToArray(), blockId) },
                    caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false)
                    || !await AreaStrikeAsync(caster, target, hit, blockId, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)
                    || !await SendStrikeFramesAsync(
                        SkillFrames.EncodeAreaEnd(blockId, ObjectTmOf(caster, castSkill, stats.Level, null)),
                        caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false))
                {
                    return false;
                }
                Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} poisons 0x{target.EntityId:X4}: damage {hit.Damage} kind {hit.Kind}");
                if (ObjectEffects.StopsPoison(hit.Kind, hit.Damage) || target.IsDead)
                {
                    if (stack is { } spent) SpendStack(spent.Item, 1);
                    return true;
                }
                var doAction = assault ? ObjectEffects.PoisonAssaultDo : ObjectEffects.PoisonBaggageDo;
                if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(doAction) is not { } poisonKind) return true;
                var magnitude = assault
                    ? ObjectEffects.PoisonAssaultTick(weaponPower, caster.PlayerDa, ratio, action.Value(ObjectEffects.PoisonRatioColumn) ?? 0f)
                    : power;
                if (assault && stack is { } loaded) SpendStack(loaded.Item, 1);
                var doBlock = commandBlockIds.Next();
                var (doSent, _) = await StartMonsterObjectAsync(
                    caster, sessions, castSkill, poisonKind, action, target, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied, magnitude, doBlock).ConfigureAwait(false);
                return doSent && await SendStrikeFramesAsync(
                    SkillFrames.EncodeAreaEnd(doBlock, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                    caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false);
            }
        }
        return true;
    }

    async Task<bool> PoisonTickAsync(
        tPoseidon.Core.Game.Skills.Effects.SkillEffect poison,
        MonsterEntity monster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        if (poison.ElapsedMs >= poison.DurationMs
            || sessions.FirstOrDefault(entry => entry.Value.PlayerEntityId == poison.CasterEntityId).Value is not { } poisoner
            || poisoner.Stats is not { } poisonerStats
            || monsterCatalog.GetById(monster.MonsterParamId) is not { } poisonedDefinition)
        {
            return true;
        }
        if (poison.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.FixedPoison)
        {
            var whole = poisonedDefinition.Type == SkillRiders.WholeBurnMonsterType;
            if (whole) poison.EndNow();
            return await OpeningStrikeAsync(
                poisoner, monster,
                new SkillHit(whole ? ObjectEffects.WholePoisonDamage : poison.Magnitude, SkillDamage.PhysicalType, SkillDamage.KindHit, SkillActions.NoElement, 0, ObjectEffects.PoisonFlags),
                sessions, now).ConfigureAwait(false);
        }
        SkillHit hit;
        lock (npcLoopRng)
        {
            hit = SkillDamage.Physical(
                ObjectAttackerOf(poisoner, poisonerStats), poison.Magnitude, MonsterTarget(monster, poisonedDefinition, SkillActions.NoElement),
                1f, SkillActions.NoElement, npcLoopRng);
        }
        hit = hit with { Damage = ObjectEffects.PoisonTickDamage(hit.Damage, poison.Amount) };
        return await PeriodicStrikeAsync(poisoner, monster, hit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
    }

    async Task<bool> EffectSelfAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (caster.Stats is not { } selfStats) return true;
        var blockId = commandBlockIds.Next();
        if (castSkill.ActionId == ObjectEffects.Fluorescene)
        {
            if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(ObjectEffects.FluoresceneDo) is not { } revealKind) return true;
            var victims = MonstersAround(caster, sessions, mapId, (caster.PlayerX, caster.PlayerY), action.Amount(ObjectEffects.RadiusColumn) ?? 0, skipsFlyers: false, now)
                .Take(ObjectEffects.FluoresceneVictims)
                .ToList();
            foreach (var victim in victims)
            {
                if (!await StartSkillEffectAsync(session, sessions, castSkill, revealKind, action, victim, now).ConfigureAwait(false)) return false;
            }
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} fluorescene reaches {victims.Count} monster(s)");
            return await SendStrikeFramesAsync(
                [ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, SkillFrames.NoEffectObject, SkillFrames.FlagTail(victims.Count > 0), blockId),
                 .. SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
                caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
        }

        if (ThrowStackOf(caster) is not { } cards) return true;
        SpendStack(cards.Item, ObjectEffects.FullHouseCards);
        if (await StartHitObjectAsync(caster, sessions, castSkill, action, ObjectEffects.FullHouseDelayMs, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied,
                tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.FullHouseBurst, ObjectEffects.FullHouseDelayMs, (caster.PlayerX, caster.PlayerY)).ConfigureAwait(false) is not { } house)
        {
            return true;
        }
        var tail = new tPoseidon.Core.Net.PacketWriter(11);
        tail.WriteByte((byte)tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied);
        tail.WriteBytes(StackTail(cards.Item));
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} full house at ({caster.PlayerX},{caster.PlayerY})");
        return await SendStrikeFramesAsync(
            [ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, house.ObjectId, tail.ToArray(), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, ObjectTmOf(caster, castSkill, selfStats.Level, selfStats.Level))],
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> DeadlyFunkAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point,
        ushort mapId,
        DateTimeOffset now)
    {
        if (skillBook.Find(ObjectEffects.SkunkBagSkill) is not { } skunkSkill
            || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(skunkSkill.ActionId) is not { } skunkKind)
        {
            return true;
        }
        var victims = MonstersAround(session.Value, sessions, mapId, (point.X, point.Y), action.Amount(ObjectEffects.RadiusColumn) ?? 0, skipsFlyers: true, now)
            .Take(ObjectEffects.DeadlyFunkVictims)
            .ToList();
        Console.WriteLine($"{LogTag} [skill] 0x{session.Value.PlayerEntityId:X4} deadly funk at ({point.X},{point.Y}): {victims.Count} monster(s)");
        foreach (var victim in victims)
        {
            if (!await StartSkillEffectAsync(session, sessions, skunkSkill, skunkKind, action, victim, now).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> FieldOpeningAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKind fieldKind,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (caster.Stats is not { } fieldStats) return true;
        var limit = Math.Min(fieldKind.FieldLimitCap, fieldKind.FieldLimitColumn is { } limitColumn ? action.Amount(limitColumn) ?? 0 : int.MaxValue);
        var victims = MonstersAround(caster, sessions, mapId, (point.X, point.Y), action.Amount(ObjectEffects.RadiusColumn) ?? 0, skipsFlyers: false, now)
            .Take(Math.Max(0, limit))
            .ToList();
        var attacker = ObjectAttackerOf(caster, fieldStats);
        var power = ObjectHits.ElementPower(
            ElementTotalOf(caster, ElementSlots.AttackSlotOf(fieldKind.StrikeElement)), action.Amount(fieldKind.StrikePowerColumn ?? fieldKind.AmountColumn) ?? 0);
        var loss = fieldKind.StrikeLossLessLuck ? fieldKind.StrikeMagicLoss - caster.PlayerLk : fieldKind.StrikeMagicLoss;
        var coefficient = action.Value(fieldKind.ChanceColumn) ?? 0f;
        var zoneGeometry = zoneGeometryByMap.GetValueOrDefault(mapId);
        var battleEvents = new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>();
        foreach (var victim in victims)
        {
            if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } fieldDefinition) continue;
            SkillHit hit;
            lock (npcLoopRng)
            {
                hit = SkillDamage.Magic(
                    attacker, power, MonsterTarget(victim, fieldDefinition, fieldKind.StrikeElement), coefficient, loss, fieldKind.StrikeElement, npcLoopRng);
            }
            if (!await PeriodicStrikeAsync(caster, victim, hit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> ElectricFieldRoundAsync(
        InGameSessionState owner,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect field,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        if (field.ElapsedMs >= field.DurationMs || field.Point is not { } fieldAt || field.Action is not { } fieldRow
            || owner.Stats is not { } fieldStats || skillBook.Find(field.SkillId) is not { } fieldSkill)
        {
            return true;
        }
        var victims = MonstersAround(owner, sessions, mapId, (fieldAt.X, fieldAt.Y), WireLimits.SaturateInt32(field.Magnitude), skipsFlyers: true, now)
            .Take(ObjectEffects.ElectricFieldVictims)
            .Select(monster => monster.EntityId)
            .ToList();
        if (victims.Count == 0) return true;
        var blockId = commandBlockIds.Next();
        var tail = new tPoseidon.Core.Net.PacketWriter(2 + 2 * victims.Count);
        WriteVictimIds(tail, victims);
        var header = new SwingFrame(Opcode.S2C_OP_00E0_TBD,
            [.. SkillFrames.EffectHeader(owner.PlayerEntityId, owner.PlayerEntityId, ObjectEffects.ElectricFieldHit, -1, (ushort)fieldSkill.Id, 0), .. tail.ToArray()],
            blockId, SwingAudience.Everyone);
        if (!await SendStrikeFramesAsync(new[] { header }, owner.PlayerEntityId, owner.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false)) return false;
        var attacker = ObjectAttackerOf(owner, fieldStats);
        var power = ObjectHits.ElementPower(ElementTotalOf(owner, ElementSlots.Electric), fieldRow.Amount(ObjectEffects.PowerColumn) ?? 0);
        var loss = ObjectEffects.ElectricFieldLoss((int)Math.Min(owner.PlayerMaxMp, int.MaxValue));
        var coefficient = fieldRow.Value(ObjectEffects.ApplyRatioColumn) ?? 0f;
        if (!await ObjectVictimsAsync(
            owner, victims,
            (victim, definition) => SkillDamage.Magic(
                attacker, power, MonsterTarget(victim, definition, SkillActions.ElectricElement), coefficient, loss, SkillActions.ElectricElement, npcLoopRng),
            blockId, sessions, mapId, zoneGeometryByMap.GetValueOrDefault(mapId), now, new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>()).ConfigureAwait(false))
        {
            return false;
        }
        Console.WriteLine($"{LogTag} [skill] 0x{owner.PlayerEntityId:X4} electric field round: {victims.Count} monster(s)");
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, ObjectTmOf(owner, fieldSkill, fieldStats.Level, null)),
            owner.PlayerEntityId, owner.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> FullHouseBurstAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect house,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        var owner = session.Value;
        house.EndNow();
        if (house.PeriodCount++ > 0 || house.Point is not { } houseAt || house.Action is not { } houseRow || owner.Stats is not { } houseStats
            || skillBook.Find(house.SkillId) is not { } houseSkill
            || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(ObjectEffects.FullHouseHit) is not { } hitKind)
        {
            return true;
        }
        var victims = MonstersAround(owner, sessions, mapId, (houseAt.X, houseAt.Y), houseRow.Amount(ObjectEffects.RadiusColumn) ?? 0, skipsFlyers: false, now)
            .Take(ObjectEffects.FullHouseVictims)
            .ToList();
        var attacker = ObjectAttackerOf(owner, houseStats);
        var power = ObjectEffects.FullHousePower(owner.PlayerAp, owner.PlayerHv, owner.PlayerLk, houseRow.Value(ObjectEffects.RatioColumn) ?? 0f);
        var coefficient = houseRow.Value(ObjectEffects.ApplyRatioColumn) ?? 0f;
        var stunMs = (houseRow.Amount(ObjectEffects.SecondColumn) ?? 0) * tPoseidon.Core.Game.Skills.Effects.SkillEffect.MillisecondsPerSecond;
        var zoneGeometry = zoneGeometryByMap.GetValueOrDefault(mapId);
        Console.WriteLine($"{LogTag} [skill] 0x{owner.PlayerEntityId:X4} full house bursts at ({houseAt.X},{houseAt.Y}): {victims.Count} monster(s)");
        foreach (var victim in victims)
        {
            if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition) continue;
            var mode = ObjectModeAgainst(houseSkill, houseRow, victim);
            SkillHit hit;
            lock (npcLoopRng)
            {
                hit = SkillDamage.Physical(
                    attacker, power, MonsterTarget(victim, victimDefinition, SkillActions.NoElement), coefficient, SkillActions.NoElement, npcLoopRng);
            }
            var blockId = commandBlockIds.Next();
            var (sent, struck) = await StartMonsterObjectAsync(owner, sessions, houseSkill, hitKind, houseRow, victim, mode, 0, blockId, checksDebuff: false).ConfigureAwait(false);
            if (!sent) return false;
            var applied = struck is not null && mode == tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied;
            if (applied && !await AreaStrikeAsync(owner, victim, hit, blockId, sessions, mapId, zoneGeometry, now, new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>()).ConfigureAwait(false))
            {
                return false;
            }
            if (!await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaEnd(blockId, applied ? ObjectTmOf(owner, houseSkill, houseStats.Level, victim.Level) : Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                owner.PlayerEntityId, victim.EntityId, victim.OwnerEntityId, sessions).ConfigureAwait(false))
            {
                return false;
            }
            if (applied && !victim.IsDead && ObjectEffects.StunsAfterFullHouse(hit.Kind)
                && !await HoldMonsterAsync(victim, stunMs, sessions, now).ConfigureAwait(false))
            {
                return false;
            }
        }
        return true;
    }

    async Task<bool> SendVisibilityAsync(
        InGameSessionState player, uint mask, List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions) =>
        await SendFromLoopAsync(
            sessions, Opcode.S2C_VISIBILITY_MASK, SkillFrames.VisibilityMask(player.PlayerEntityId, mask),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);

    async Task<bool> SendRevealedAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> owner, bool added, IReadOnlyList<ushort> entityIds)
    {
        if (entityIds.Count == 0) return true;
        return await SendFromLoopAsync(
            new[] { owner }, Opcode.S2C_REVEALED, SkillFrames.Revealed(added, entityIds), MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
    }

    async Task<bool> RevealScanAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect eyes,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        var owner = session.Value;
        if (eyes.Action is not { } eyesRow) return true;
        var seen = MonstersAround(owner, sessions, mapId, (owner.PlayerX, owner.PlayerY), eyesRow.Amount(UtilitySkills.RadiusColumn) ?? 0, skipsFlyers: false, now)
            .Select(monster => monster.EntityId)
            .ToList();
        var (added, removed) = UtilitySkills.RevealDiff(eyes.Tracked, seen);
        foreach (var gone in removed) eyes.Tracked.Remove(gone);
        foreach (var found in added) eyes.Tracked.Add(found);
        return await SendRevealedAsync(session, false, removed.Where(gone => monsterManager.Get(gone) is not null).ToList()).ConfigureAwait(false)
            && await SendRevealedAsync(session, true, added).ConfigureAwait(false);
    }

    async Task<bool> DetectorAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action)
    {
        var caster = session.Value;
        if (caster.Stats is not { } detectorStats) return true;
        var found = RollsApplyRatio(action.Value(UtilitySkills.ApplyRatioColumn) ?? 0f);
        var blockId = commandBlockIds.Next();
        var casterOnly = new List<KeyValuePair<S2SessionContext, InGameSessionState>> { session };
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} detector {castSkill.Id}: roll {found}");
        return await SendStrikeFramesAsync(
            [ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, SkillFrames.NoEffectObject, SkillFrames.DetectorTail(found), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, castSkill.ActionId == UtilitySkills.BasicDetector
                 ? ObjectTmOf(caster, castSkill, detectorStats.Level, null)
                 : Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, casterOnly).ConfigureAwait(false);
    }

    async Task<bool> UtilityGroundAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point,
        tPoseidon.Core.Game.Stats.StatManager stats)
    {
        var caster = session.Value;
        if (castSkill.ActionId == UtilitySkills.ItemDetector) return await DetectorAsync(session, castSkill, action).ConfigureAwait(false);
        var blockId = commandBlockIds.Next();
        if (castSkill.ActionId == UtilitySkills.Dash)
        {
            if ((PlayerConditionOf(caster) & MonsterSkills.StunnedCondition) != 0) return true;
            var dashMs = UtilitySkills.DashMs(SkillAreaRules.Distance(caster.PlayerX, caster.PlayerY, point.X, point.Y));
            if (await StartHitObjectAsync(caster, sessions, castSkill, action, dashMs, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied,
                    tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.DashArrival, dashMs, point).ConfigureAwait(false) is not { } dash)
            {
                return true;
            }
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} dashes to ({point.X},{point.Y}) in {dashMs} ms");
            return await SendStrikeFramesAsync(
                [ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, dash.ObjectId, SkillFrames.PointTail(point), blockId),
                 .. SkillFrames.EncodeAreaEnd(blockId, ObjectTmOf(caster, castSkill, stats.Level, null))],
                caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
        }

        var radius = action.Amount(UtilitySkills.RadiusColumn) ?? 0;
        var lifeMs = (action.Amount(UtilitySkills.SecondsColumn) ?? 0) * tPoseidon.Core.Game.Skills.Effects.SkillEffect.MillisecondsPerSecond;
        if (await StartHitObjectAsync(caster, sessions, castSkill, action, lifeMs, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied,
                tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.MineWatch, ObjectHits.WatchTickMs, point, radius).ConfigureAwait(false) is not { } mine)
        {
            return true;
        }
        var tail = new tPoseidon.Core.Net.PacketWriter(6);
        tail.WriteUInt16(point.X);
        tail.WriteUInt16(point.Y);
        tail.WriteUInt16((ushort)radius);
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} lays mine {castSkill.Id} at ({point.X},{point.Y}) radius {radius}, {lifeMs} ms");
        return await SendStrikeFramesAsync(
            [ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, mine.ObjectId, tail.ToArray(), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, ObjectTmOf(caster, castSkill, stats.Level, stats.Level))],
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> MineWatchAsync(
        InGameSessionState owner,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect mine,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        if (mine.ElapsedMs <= UtilitySkills.MineArmingMs || mine.Point is not { } mineAt || mine.Action is not { } mineRow
            || owner.Stats is not { } mineStats || skillBook.Find(UtilitySkills.LandmineSkill) is not { } landmine)
        {
            return true;
        }
        if (MonstersAround(owner, sessions, mapId, (mineAt.X, mineAt.Y), WireLimits.SaturateInt32(mine.Magnitude), skipsFlyers: true, now).FirstOrDefault() is not { } victim
            || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition)
        {
            return true;
        }
        mine.EndNow();
        var blastAction = UtilitySkills.BlastOf(mine.Kind.ActionId);
        var tail = new tPoseidon.Core.Net.PacketWriter(6);
        tail.WriteUInt16(mineAt.X);
        tail.WriteUInt16(mineAt.Y);
        tail.WriteUInt16(victim.EntityId);
        var blockId = commandBlockIds.Next();
        var header = new SwingFrame(Opcode.S2C_OP_00E0_TBD,
            [.. SkillFrames.EffectHeader(owner.PlayerEntityId, owner.PlayerEntityId, (ushort)blastAction, (sbyte)landmine.Row.EsClass, (ushort)landmine.Id, (ushort)landmine.Row.EState),
             .. tail.ToArray()],
            blockId, SwingAudience.Everyone);
        if (!await SendStrikeFramesAsync(new[] { header }, owner.PlayerEntityId, victim.EntityId, victim.OwnerEntityId, sessions).ConfigureAwait(false)) return false;
        var power = UtilitySkills.MinePower(mine.Kind.ActionId, owner.PlayerDa, owner.PlayerLk, mineRow.Value(UtilitySkills.TrapPowerColumn) ?? 0f);
        SkillHit hit;
        lock (npcLoopRng)
        {
            hit = SkillDamage.Physical(
                ObjectAttackerOf(owner, mineStats), power, MonsterTarget(victim, victimDefinition, SkillActions.NoElement),
                mineRow.Value(UtilitySkills.ApplyRatioColumn) ?? 0f, SkillActions.NoElement, npcLoopRng);
        }
        Console.WriteLine($"{LogTag} [skill] 0x{owner.PlayerEntityId:X4} mine blast on 0x{victim.EntityId:X4}: damage {hit.Damage} kind {hit.Kind}");
        if (!await AreaStrikeAsync(owner, victim, hit, blockId, sessions, mapId, zoneGeometryByMap.GetValueOrDefault(mapId), now,
                new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>()).ConfigureAwait(false))
        {
            return false;
        }
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, ObjectTmOf(owner, landmine, mineStats.Level, victim.Level)),
            owner.PlayerEntityId, victim.EntityId, victim.OwnerEntityId, sessions).ConfigureAwait(false);
    }

    int MorphStatValueOf(InGameSessionState player, int stat) => stat switch
    {
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatAp => WireLimits.SaturateInt32(player.PlayerAp),
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatDx => player.PlayerDx,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatLk => player.PlayerLk,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatDp => player.PlayerDp,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatHv => player.PlayerHv,
        _ => 0,
    };

    async Task<bool> SendMorphStatsAsync(KeyValuePair<S2SessionContext, InGameSessionState> owner, IReadOnlyList<(int Stat, int Delta)> order) =>
        await SendFromLoopAsync(
            new[] { owner }, Opcode.S2C_MORPH_STATS,
            SkillEffectFrames.MorphStats(order.Select(entry => (entry.Stat, MorphStatValueOf(owner.Value, entry.Stat))).ToList()),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);

    async Task<bool> StartMorphAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> owner,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect morph,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions)
    {
        var player = owner.Value;
        if (morph.Kind.StripsActions.Count > 0 && !(await RemoveEffectsAsync(owner, null, morph.Kind.StripsActions, sessions).ConfigureAwait(false)).Sent) return false;
        if (morph.Action is not { } morphRow
            || player.Effects.Snapshot().Any(held => held != morph && held.IsApplied && held.Kind.Morph != MorphRule.None))
        {
            Console.WriteLine($"{LogTag} [skill] 0x{player.PlayerEntityId:X4} is already transformed: skill {morph.SkillId} ends");
            return !player.Effects.Remove(morph) || await EndSkillEffectAsync(morph, sessions).ConfigureAwait(false);
        }
        morph.FixedDeltas = FormSkills.MorphDeltas(morph.Kind.Morph, morphRow, player.PlayerAp, player.PlayerLk, player.PlayerDp, player.PlayerHv);
        ApplyEffectDeltas(player);
        Console.WriteLine($"{LogTag} [skill] 0x{player.PlayerEntityId:X4} transforms ({morph.Kind.Morph}): {string.Join(" ", morph.FixedDeltas.Select(entry => $"{entry.Stat}:{entry.Delta:+#;-#;0}"))}");
        return await SendMorphStatsAsync(owner, morph.FixedDeltas).ConfigureAwait(false);
    }

    async Task<bool> EndMorphAsync(KeyValuePair<S2SessionContext, InGameSessionState> owner, tPoseidon.Core.Game.Skills.Effects.SkillEffect morph)
    {
        var player = owner.Value;
        var order = morph.FixedDeltas;
        if (order.Count == 0) return true;
        morph.FixedDeltas = Array.Empty<(int Stat, int Delta)>();
        ApplyEffectDeltas(player);
        if (!await SendMorphStatsAsync(owner, order).ConfigureAwait(false)) return false;
        if (player.Stats is not { } morphStats || morph.Action is not { } morphRow) return true;
        morphStats.SetVitals(FormSkills.HpAfterMorph(morphStats.CurrentHp, morphRow.Value(FormSkills.HpRemainColumn) ?? 0f), morphStats.CurrentMp);
        Console.WriteLine($"{LogTag} [skill] 0x{player.PlayerEntityId:X4} returns to normal form, HP {morphStats.CurrentHp}");
        return await SendFromLoopAsync(
            new[] { owner }, Opcode.S2C_HEALTH_STATUS,
            tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(morphStats.CurrentHp).EncodePayload(),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
    }

    async Task<bool> MorphDrainAsync(KeyValuePair<S2SessionContext, InGameSessionState> owner, tPoseidon.Core.Game.Skills.Effects.SkillEffect morph)
    {
        if (owner.Value.Stats is not { } drainStats || morph.Action is not { } drainRow) return true;
        var unit = drainRow.Amount(FormSkills.MpUnitColumn) ?? 0;
        if (unit >= drainStats.CurrentMp)
        {
            morph.EndNow();
            return true;
        }
        drainStats.SetVitals(drainStats.CurrentHp, drainStats.CurrentMp - (uint)Math.Max(0, unit));
        return await SendFromLoopAsync(
            new[] { owner }, Opcode.S2C_MANA_STATUS, tPoseidon.Core.Net.Packets.ManaStatusPacket.Of(drainStats.CurrentMp).EncodePayload(),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
    }

    async Task<bool> FormSelfAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill)
    {
        var caster = session.Value;
        if (castSkill.ActionId == FormSkills.EscapeBunker)
        {
            if (!caster.Effects.Snapshot().Any(held => held.IsApplied && held.Kind.ActionId == FormSkills.BunkerDo)) return true;
            if (!(await RemoveEffectsAsync(session, null, new[] { FormSkills.BunkerDo }, sessions).ConfigureAwait(false)).Sent) return false;
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} leaves the bunker");
            return await SendStrikeFramesAsync(
                FlatTm(caster, castSkill).Select(tmFrame => new SwingFrame(tmFrame.Opcode, tmFrame.Payload, MeleeSwingFrames.NoBlock, SwingAudience.Attacker)).ToList(),
                caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
        }
        var blockId = commandBlockIds.Next();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} card identification");
        return await SendStrikeFramesAsync(
            [ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, SkillFrames.NoEffectObject, Array.Empty<byte>(), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    float KnifeMultiplierOf(InGameSessionState thrower) =>
        FormSkills.KnifeMultiplier(
            thrower.Effects.Snapshot().Any(held => held.IsApplied && held.Kind.ActionId == tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.PowerThrow),
            thrower.Skills.TryGetValue(ThrowSkills.PowerThrowSkill, out var throwLevel) ? throwLevel : null,
            thrower.Skills.TryGetValue(ThrowSkills.PowerThrowSkill, out var rowLevel)
                ? skillBook.Find(ThrowSkills.PowerThrowSkill)?.ActionAt(rowLevel)?.Value(ThrowSkills.RatioColumn)
                : null);

    async Task<bool> KnivesAsync(
        InGameSessionState caster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        IReadOnlyList<(MonsterEntity Monster, IReadOnlyList<SkillHit> Hits)> struck,
        byte[] tail,
        int spent,
        Item knives,
        ushort mapId,
        DateTimeOffset now)
    {
        var blockId = commandBlockIds.Next();
        if (!await SendStrikeFramesAsync(
            new[] { ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, SkillFrames.NoEffectObject, tail, blockId) },
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }
        var zoneGeometry = zoneGeometryByMap.GetValueOrDefault(mapId);
        foreach (var (victim, hits) in struck)
        {
            var chain = new ChainVictim(victim.EntityId, hits);
            var hpBefore = victim.CurrentHp;
            victim.AddAttacker(caster.PlayerEntityId);
            victim.ApplyDamage(chain.Sum);
            var (victimX, victimY) = victim.GetInterpolatedPosition(now);
            var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
                AttackerEntityId: caster.PlayerEntityId, TargetEntityId: victim.EntityId,
                AttackerX: caster.PlayerX, AttackerY: caster.PlayerY, TargetX: victimX, TargetY: victimY,
                Damage: chain.Sum, MobHpAfter: victim.CurrentHp, HitFlags: 0, HitKind: SkillDamage.KindHit, Killed: victim.IsDead,
                NextSwingAtUtc: now, MonsterParamId: victim.MonsterParamId, MobMaxHp: victim.MaxHp, MobHpBefore: hpBefore);
            if (strike.Killed && caster.CurrentTargetEntityId == victim.EntityId) caster.CurrentTargetEntityId = 0;
            var (additions, ownerEntityId) = await ResolveStrikeAsync(
                strike, caster, mapId, sessions, zoneGeometry, now, new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>(),
                SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} knives -> 0x{victim.EntityId:X4}: {string.Join("/", hits.Select(hit => $"{hit.Damage}k{hit.Kind}"))} = {chain.Sum}");
            if (!await SendStrikeFramesAsync(
                SkillFrames.EncodeChainVictim(strike, chain, blockId, additions),
                strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false))
            {
                return false;
            }
        }
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, FlatTm(caster, castSkill)), caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }
        SpendStack(knives, spent);
        return true;
    }

    IReadOnlyList<SkillHit> KnifeHits(InGameSessionState caster, tPoseidon.Core.Game.Stats.StatManager stats, MonsterEntity victim, long power, float coefficient, int count, int split)
    {
        if (monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition) return Array.Empty<SkillHit>();
        var attacker = ObjectAttackerOf(caster, stats);
        var hits = new List<SkillHit>(count);
        lock (npcLoopRng)
        {
            for (var knife = 0; knife < count; knife++)
            {
                var hit = SkillDamage.Physical(attacker, power, MonsterTarget(victim, victimDefinition, SkillActions.NoElement), coefficient, SkillActions.NoElement, npcLoopRng);
                hits.Add(split > 0 ? FormSkills.SplitKnife(hit, split) : hit);
            }
        }
        return hits;
    }

    async Task<bool> FormTargetAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity target,
        tPoseidon.Core.Game.Stats.StatManager stats,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        var targetAt = target.GetInterpolatedPosition(now);
        if (castSkill.ActionId == FormSkills.ChainOfKnives)
        {
            if (ThrowStackOf(caster) is not { } knives) return true;
            var count = action.Amount(FormSkills.CountColumn) ?? 0;
            var thrown = Math.Min(count, (int)knives.Item.Stack);
            var power = FormSkills.KnifePower(
                knives.Definition.Ap, KnifeMultiplierOf(caster), caster.PlayerDa, action.Value(FormSkills.RatioColumn) ?? 0f, thrown, count);
            var hits = KnifeHits(caster, stats, target, power, action.Value(FormSkills.ApplyRatioColumn) ?? 0f, thrown, thrown);
            var tail = new tPoseidon.Core.Net.PacketWriter(24 + 10 * hits.Count);
            tail.WriteUInt16(caster.PlayerX);
            tail.WriteUInt16(caster.PlayerY);
            tail.WriteInt32(knives.Item.ItemIndex);
            tail.WriteUInt64(knives.Item.ItemUid);
            tail.WriteByte((byte)thrown);
            tail.WriteUInt16(target.EntityId);
            tail.WriteUInt16(targetAt.x);
            tail.WriteUInt16(targetAt.y);
            tail.WriteByte((byte)hits.Count);
            tail.WriteBytes(SkillFrames.ChainSlots(hits));
            return await KnivesAsync(caster, sessions, castSkill, new[] { (target, hits) }, tail.ToArray(), thrown, knives.Item, mapId, now).ConfigureAwait(false);
        }

        var mode = ObjectModeAgainst(castSkill, action, target);
        var blockId = commandBlockIds.Next();
        var soldierTail = new tPoseidon.Core.Net.PacketWriter(19);
        soldierTail.WriteByte((byte)mode);
        if (ThrowStackOf(caster) is not { } cards)
        {
            soldierTail.WriteUInt16(FormSkills.SoldierWithoutCards);
            var lostObject = idAllocator.NewId();
            idAllocator.Release(lostObject);
            return await SendStrikeFramesAsync(
                [ObjectHeaderFrame(caster, target.EntityId, castSkill, castSkill.ActionId, lostObject, soldierTail.ToArray(), blockId),
                 .. SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
                caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false);
        }
        if (cards.Definition.SubType != ThrowSkills.CardSubType) return true;
        SpendStack(cards.Item, 1);
        int random;
        lock (npcLoopRng)
        {
            random = npcLoopRng.Next(FormSkills.RandomMax + 1);
        }
        var drawn = FormSkills.SoldierCards(action.Amount(FormSkills.MinCardColumn) ?? 0, action.Amount(FormSkills.MaxCardColumn) ?? 0, random);
        var soldierPower = FormSkills.SoldierPower(caster.PlayerAp, caster.PlayerHv, action.Value(FormSkills.DamageRatioColumn) ?? 0f, drawn);
        if (await StartHitObjectAsync(caster, sessions, castSkill, action, FormSkills.SoldierLifeMs, mode,
                tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.SoldierStrike, FormSkills.SoldierStrikeMs,
                magnitude: soldierPower, trackedEntityId: target.EntityId, limiterClass: -1).ConfigureAwait(false) is not { } soldiers)
        {
            return true;
        }
        soldierTail.WriteUInt16(0);
        soldierTail.WriteUInt16((ushort)drawn);
        soldierTail.WriteUInt16(target.EntityId);
        soldierTail.WriteInt32(cards.Item.ItemIndex);
        soldierTail.WriteUInt64(cards.Item.ItemUid);
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} summons {drawn} card soldier(s) at 0x{target.EntityId:X4}, power {soldierPower}, mode {mode}");
        return await SendStrikeFramesAsync(
            [ObjectHeaderFrame(caster, target.EntityId, castSkill, castSkill.ActionId, soldiers.ObjectId, soldierTail.ToArray(), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, mode == tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied
                 ? ObjectTmOf(caster, castSkill, stats.Level, null)
                 : Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<bool> SoldierStrikeAsync(
        InGameSessionState owner,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect soldiers,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        if (soldiers.PeriodCount++ > 0 || soldiers.Action is not { } soldierRow || owner.Stats is not { } soldierStats
            || monsterManager.Get(soldiers.Tracked.FirstOrDefault()) is not { IsDead: false } target
            || monsterCatalog.GetById(target.MonsterParamId) is not { } targetDefinition)
        {
            return true;
        }
        SkillHit hit;
        lock (npcLoopRng)
        {
            hit = SkillDamage.Physical(
                ObjectAttackerOf(owner, soldierStats), soldiers.Magnitude, MonsterTarget(target, targetDefinition, SkillActions.NoElement),
                soldierRow.Value(FormSkills.ApplyRatioColumn) ?? 0f, SkillActions.NoElement, npcLoopRng);
        }
        var blockId = commandBlockIds.Next();
        Console.WriteLine($"{LogTag} [skill] 0x{owner.PlayerEntityId:X4} card soldiers strike 0x{target.EntityId:X4}: damage {hit.Damage} kind {hit.Kind}");
        return await AreaStrikeAsync(owner, target, hit, blockId, sessions, mapId, zoneGeometryByMap.GetValueOrDefault(mapId), now,
                new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>()).ConfigureAwait(false)
            && await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                owner.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<bool> UpThrowAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point,
        tPoseidon.Core.Game.Stats.StatManager stats,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (ThrowStackOf(caster) is not { } knives) return true;
        var victims = MonstersAround(caster, sessions, mapId, (point.X, point.Y), action.Amount(FormSkills.RadiusColumn) ?? 0, skipsFlyers: false, now)
            .Take(Math.Clamp(action.Amount(FormSkills.KillNumColumn) ?? 0, 0, FormSkills.UpThrowVictims))
            .ToList();
        var count = Math.Clamp(action.Amount(FormSkills.CountColumn) ?? 0, 0, FormSkills.UpThrowSlots);
        var knifeCount = action.Amount(FormSkills.KnifeCountColumn) ?? 0;
        var power = FormSkills.UpThrowPower(knives.Definition.Ap, KnifeMultiplierOf(caster), caster.PlayerDa, action.Value(FormSkills.RatioColumn) ?? 0f);
        var coefficient = action.Value(FormSkills.ApplyRatioColumn) ?? 0f;
        var struck = victims.Select(victim => (victim, KnifeHits(caster, stats, victim, power, coefficient, count, split: 0))).ToList();
        var tail = new tPoseidon.Core.Net.PacketWriter(18 + struck.Sum(entry => 3 + 10 * entry.Item2.Count));
        tail.WriteUInt16(point.X);
        tail.WriteUInt16(point.Y);
        tail.WriteInt32(knives.Item.ItemIndex);
        tail.WriteUInt64(knives.Item.ItemUid);
        tail.WriteByte((byte)knifeCount);
        tail.WriteByte((byte)struck.Count);
        foreach (var (victim, hits) in struck)
        {
            tail.WriteUInt16(victim.EntityId);
            tail.WriteByte((byte)hits.Count);
            tail.WriteBytes(SkillFrames.ChainSlots(hits));
        }
        return await KnivesAsync(
            caster, sessions, castSkill, struck.Select(entry => (entry.victim, entry.Item2)).ToList(), tail.ToArray(), knifeCount, knives.Item, mapId, now).ConfigureAwait(false);
    }

    bool KnockBlocked(ZoneGeometry? geometry, int x, int y)
    {
        if (geometry is null) return false;
        var (cellX, cellY) = (ZoneGeometry.CellOf(x), ZoneGeometry.CellOf(y));
        return !geometry.ContainsCell(cellX, cellY) || geometry.AttributeAt(cellX, cellY) == ZoneGeometry.CellBlocked;
    }

    (ushort X, ushort Y) KnockPoint(InGameSessionState caster, MonsterEntity victim, int radius, ushort mapId, DateTimeOffset now)
    {
        var victimAt = victim.GetInterpolatedPosition(now);
        if (monsterCatalog.GetById(victim.MonsterParamId) is not { } knockedDefinition
            || (knockedDefinition.WalkSpeed == 0f && knockedDefinition.RunSpeed == 0f))
        {
            return victimAt;
        }
        var geometry = zoneGeometryByMap.GetValueOrDefault(mapId);
        var destination = KnockSkills.Destination((caster.PlayerX, caster.PlayerY), (victimAt.x, victimAt.y), radius);
        var (walkedX, walkedY) = KnockSkills.Walk((victimAt.x, victimAt.y), destination, (x, y) => KnockBlocked(geometry, x, y));
        return ((ushort)Math.Clamp(walkedX, 0, ushort.MaxValue), (ushort)Math.Clamp(walkedY, 0, ushort.MaxValue));
    }

    async Task<bool> KnockAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (caster.Stats is not { } knockStats
            || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(KnockSkills.ObjectOf(castSkill.ActionId)) is not { } knockKind)
        {
            return true;
        }
        var victims = MonstersAround(caster, sessions, mapId, (caster.PlayerX, caster.PlayerY), action.Amount(KnockSkills.RadiusColumn) ?? 0, skipsFlyers: true, now)
            .Take(KnockSkills.Victims)
            .ToList();
        var announce = new Func<Task<bool>>(async () =>
        {
            var blockId = commandBlockIds.Next();
            return await SendStrikeFramesAsync(
                [ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, SkillFrames.NoEffectObject, Array.Empty<byte>(), blockId),
                 .. SkillFrames.EncodeAreaEnd(blockId, ObjectTmOf(caster, castSkill, knockStats.Level, null))],
                caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
        });
        var spawnsFirst = castSkill.ActionId == KnockSkills.PowerCharging;
        if (!spawnsFirst && !await announce().ConfigureAwait(false)) return false;
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} knocks {victims.Count} monster(s)");
        foreach (var victim in victims)
        {
            if (!await KnockVictimAsync(caster, knockStats, sessions, castSkill, action, knockKind, victim, mapId, now).ConfigureAwait(false)) return false;
        }
        return !spawnsFirst || await announce().ConfigureAwait(false);
    }

    async Task<bool> KnockVictimAsync(
        InGameSessionState caster,
        tPoseidon.Core.Game.Stats.StatManager stats,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKind knockKind,
        MonsterEntity victim,
        ushort mapId,
        DateTimeOffset now)
    {
        if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition) return true;
        var mode = ObjectModeAgainst(castSkill, action, victim);
        var victimAt = victim.GetInterpolatedPosition(now);
        var applied = mode == tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied;
        (ushort X, ushort Y) pushedTo = applied ? KnockPoint(caster, victim, action.Amount(KnockSkills.RadiusColumn) ?? 0, mapId, now) : victimAt;
        var hit = new SkillHit(0, SkillDamage.PhysicalType, SkillDamage.KindMiss, SkillActions.NoElement, 0);
        if (applied)
        {
            var power = KnockSkills.Power(
                KnockSkills.PushedDistance((victimAt.x, victimAt.y), (pushedTo.X, pushedTo.Y)),
                stats.CurrentHp, action.Value(KnockSkills.RatioColumn) ?? 0f);
            lock (npcLoopRng)
            {
                hit = SkillDamage.Physical(
                    ObjectAttackerOf(caster, stats), power, MonsterTarget(victim, victimDefinition, SkillActions.NoElement),
                    action.Value(KnockSkills.ApplyRatioColumn) ?? 0f, SkillActions.NoElement, npcLoopRng);
            }
            if (hit.Damage == 0) pushedTo = victimAt;
        }
        var blockId = commandBlockIds.Next();
        var (sent, knock) = await StartMonsterObjectAsync(caster, sessions, castSkill, knockKind, action, victim, mode, 0, blockId, pushedTo).ConfigureAwait(false);
        if (!sent) return false;
        if (knock is null) return true;
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} knocks 0x{victim.EntityId:X4} to ({pushedTo.X},{pushedTo.Y}): damage {hit.Damage} kind {hit.Kind}, mode {mode}");
        if (applied)
        {
            if (!await AreaStrikeAsync(caster, victim, hit, blockId, sessions, mapId, zoneGeometryByMap.GetValueOrDefault(mapId), now,
                    new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>()).ConfigureAwait(false))
            {
                return false;
            }
        }
        else if (!await SendStrikeFramesAsync(
            new[] { new SwingFrame(Opcode.S2C_OP_002A_TBD, SkillFrames.HitResult(victim.EntityId, hit), blockId, SwingAudience.Everyone) },
            caster.PlayerEntityId, victim.EntityId, victim.OwnerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (!await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
            caster.PlayerEntityId, victim.EntityId, victim.OwnerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }
        return !applied || hit.Damage == 0 || victim.IsDead || knockKind.ActionId != KnockSkills.ChargingHit
            || await HoldMonsterAsync(victim, KnockSkills.ChargingMoveMs, sessions, now).ConfigureAwait(false);
    }

    async Task<bool> MassiveAttackAsync(
        InGameSessionState caster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        MonsterEntity target,
        ushort mapId,
        DateTimeOffset now)
    {
        if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(KnockSkills.MassiveAttack) is not { } massiveKind) return true;
        var mode = ObjectModeAgainst(castSkill, action, target);
        (ushort X, ushort Y) pushedTo = mode == tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied
            ? KnockPoint(caster, target, action.Amount(KnockSkills.RadiusColumn) ?? 0, mapId, now)
            : target.GetInterpolatedPosition(now);
        var blockId = commandBlockIds.Next();
        var (sent, _) = await StartMonsterObjectAsync(caster, sessions, castSkill, massiveKind, action, target, mode, 0, blockId, pushedTo).ConfigureAwait(false);
        return sent && await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
            caster.PlayerEntityId, target.EntityId, target.OwnerEntityId, sessions).ConfigureAwait(false);
    }

    IReadOnlyList<byte[]> ScrollTailStrings(tPoseidon.Core.LibConfig.SkillTableRow action) =>
        ScrollSkills.StringColumns
            .Select(column => System.Text.Encoding.GetEncoding(ScrollSkills.TextCodePage).GetBytes(action.Columns.GetValueOrDefault(column) ?? string.Empty))
            .ToList();

    async Task<bool> WideHealingAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action)
    {
        var caster = session.Value;
        if (caster.Stats is not { } healerStats) return true;
        var ratio = action.Value(ScrollSkills.RatioColumn) ?? 0f;
        var radius = action.Amount(ScrollSkills.RadiusColumn) ?? 0;
        async Task<bool> HealAsync(KeyValuePair<S2SessionContext, InGameSessionState> healed)
        {
            if (healed.Value.Stats is not { } healedStats) return true;
            healedStats.SetVitals(ScrollSkills.Healed(healedStats.CurrentHp, healed.Value.PlayerMaxHp, ratio), healedStats.CurrentMp);
            return await SendFromLoopAsync(
                new[] { healed }, Opcode.S2C_HEALTH_STATUS,
                tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(healedStats.CurrentHp).EncodePayload(),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
        }
        if (!await HealAsync(session).ConfigureAwait(false)) return false;
        var healedIds = new List<ushort>();
        foreach (var other in sessions)
        {
            if (other.Value == caster || other.Value.IsDead || other.Value.Stats is not { CurrentHp: > 0 }
                || SkillAreaRules.Distance(caster.PlayerX, caster.PlayerY, other.Value.PlayerX, other.Value.PlayerY) >= radius)
            {
                continue;
            }
            if (!await HealAsync(other).ConfigureAwait(false)) return false;
            healedIds.Add(other.Value.PlayerEntityId);
        }
        var mode = ObjectModeAgainst(castSkill, action, null);
        var tail = new tPoseidon.Core.Net.PacketWriter(5 + 2 * healedIds.Count);
        tail.WriteByte((byte)mode);
        tail.WriteInt32(healedIds.Count);
        foreach (var healedId in healedIds) tail.WriteUInt16(healedId);
        var healObject = idAllocator.NewId();
        idAllocator.Release(healObject);
        var blockId = commandBlockIds.Next();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} wide healing reaches {healedIds.Count} other player(s)");
        return await SendStrikeFramesAsync(
            [ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, healObject, tail.ToArray(), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, mode == tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied
                 ? ObjectTmOf(caster, castSkill, healerStats.Level, null)
                 : Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> PartyDetectorAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point)
    {
        var caster = session.Value;
        var partyIds = caster.Character is { } detecting && systemLink.MirrorOf(detecting.CharacterId) is { } detectingParty
            ? detectingParty.Others.Select(member => member.CharacterId).ToHashSet()
            : new HashSet<ulong>();
        var members = sessions
            .Where(entry => entry.Value == caster || (entry.Value.Character is { } member && partyIds.Contains(member.CharacterId)))
            .Select(entry => entry.Value.PlayerEntityId)
            .ToList();
        var lifeMs = (action.Amount(ScrollSkills.SecondsColumn) ?? 0) * tPoseidon.Core.Game.Skills.Effects.SkillEffect.MillisecondsPerSecond;
        if (await StartHitObjectAsync(caster, sessions, castSkill, action, lifeMs, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied)
                .ConfigureAwait(false) is not { } detector)
        {
            return true;
        }
        var tail = new tPoseidon.Core.Net.PacketWriter(10 + 2 * members.Count);
        tail.WriteUInt16(point.X);
        tail.WriteUInt16(point.Y);
        tail.WriteUInt16(0);
        tail.WriteInt32(members.Count);
        foreach (var memberId in members) tail.WriteUInt16(memberId);
        var blockId = commandBlockIds.Next();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} party detection at ({point.X},{point.Y}) for {members.Count} player(s)");
        return await SendStrikeFramesAsync(
            [ObjectHeaderFrame(caster, caster.PlayerEntityId, castSkill, castSkill.ActionId, detector.ObjectId, tail.ToArray(), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    SkillAttacker MonsterAttackerOf(MonsterEntity monster, MonsterParamEntry definition) => new(
        definition.ApLv + monster.StatDelta(MonsterStat.Ap), definition.AcLv + monster.StatDelta(MonsterStat.Ac),
        definition.MaLv + monster.StatDelta(MonsterStat.Ma), definition.LkLv + monster.StatDelta(MonsterStat.Lk), definition.Level,
        definition.DaLv + monster.StatDelta(MonsterStat.Da), monster.CurrentHp, monster.MaxHp,
        definition.HvLv + monster.StatDelta(MonsterStat.Hv), definition.DxLv + monster.StatDelta(MonsterStat.Dx),
        definition.DpLv + monster.StatDelta(MonsterStat.Dp));

    SkillTarget PlayerTargetOf(InGameSessionState player, int element) => new(
        player.PlayerDp, player.PlayerHv, player.PlayerMd, player.PlayerLk,
        player.Stats?.CurrentHp ?? 0, player.Stats?.Level ?? 0, ElementTotalOf(player, ElementSlots.ResistSlotOf(element)),
        player.PlayerMaxHp, player.CombatBase.Dp);

    List<KeyValuePair<S2SessionContext, InGameSessionState>> PlayersAround(
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions, (int X, int Y) centre, float radius) =>
        sessions
            .Where(entry => !entry.Value.IsDead && entry.Value.Stats is { CurrentHp: > 0 }
                && SkillAreaRules.Distance(centre.X, centre.Y, entry.Value.PlayerX, entry.Value.PlayerY) <= radius)
            .ToList();

    SwingFrame MonsterHeaderFrame(ushort monsterId, ushort secondId, int actionId, int skillId, byte[] tail, ushort blockId) =>
        new(Opcode.S2C_OP_00E0_TBD, [.. SkillFrames.EffectHeader(monsterId, secondId, (ushort)actionId, -1, (ushort)skillId, 0), .. tail], blockId, SwingAudience.Everyone);

    async Task<bool> MonsterHitPlayerAsync(
        MonsterEntity monster,
        KeyValuePair<S2SessionContext, InGameSessionState> victimSession,
        SkillHit hit,
        ushort blockId,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId)
    {
        var victim = victimSession.Value;
        if (victim.IsDead || victim.Stats is not { } victimStats) return true;
        if (!await SendStrikeFramesAsync(
            new[] { new SwingFrame(Opcode.S2C_OP_002A_TBD, SkillFrames.HitResult(victim.PlayerEntityId, hit), blockId, SwingAudience.Everyone) },
            monster.EntityId, victim.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (hit.Damage <= 0 || victim.GameMasterImmortal) return true;
        var fatal = hit.Damage >= victimStats.CurrentHp;
        var hpAfter = victimStats.TakeHit(hit.Damage);
        victim.Recovery.PauseAfterDamage();
        if (victim.Dig is not null)
        {
            if (fatal) victim.Dig = null;
            else await drillController.InterruptAsync(victim).ConfigureAwait(false);
        }
        if (victim.Cast is { IsUnderWay: true } interrupted
            && (fatal || (interrupted.Skill.Has(SkillDefinition.CancelledByDamageOption)
                && !skillBook.GrantsAbility(victim.Skills, SkillBook.InvincibleCastingAbility))))
        {
            victim.Cast = null;
            if (!fatal && !await SendFromLoopAsync(
                new[] { victimSession }, Opcode.SKILL_CAST_CANCEL, Array.Empty<byte>(), MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
        }
        Console.WriteLine($"{LogTag} [skill] monster 0x{monster.EntityId:X4} skill hits 0x{victim.PlayerEntityId:X4}: damage {hit.Damage} kind {hit.Kind}, hp {hpAfter}");
        if (!fatal) return true;

        var wasAttacking = victim.CurrentTargetEntityId != 0;
        if (wasAttacking)
        {
            monsterManager.Get(victim.CurrentTargetEntityId)?.RemoveAttacker(victim.PlayerEntityId);
            victim.CurrentTargetEntityId = 0;
            victim.NextSwingAt = default;
            victim.SwingsOnTarget = 0;
        }
        victim.ChaseEndsAt = default;
        victim.IsDead = true;
        victim.Engagements.TakeAll();
        foreach (var orphaned in monsterManager.GetByMap(mapId)) orphaned.ReleaseOwner(victim.PlayerEntityId);
        var expAfter = victimStats.Die(tPoseidon.Core.Game.PvP.PkRules.LosesExpOnDeath(victim.PvpMode));
        var forVictim = PlayerDeathFrames.ForVictim(victim.PlayerEntityId, victim.PlayerX, victim.PlayerY, victim.PlayerMoveType, wasAttacking, expAfter)
            .Concat(victim.Quests?.SuspendHuntForDeath() ?? Array.Empty<(Opcode Opcode, byte[] Payload)>())
            .ToList();
        var forOthers = PlayerDeathFrames.ForOthers(victim.PlayerEntityId, victim.PlayerX, victim.PlayerY, victim.PlayerMoveType, wasAttacking);
        Console.WriteLine($"{LogTag} [combat] player 0x{victim.PlayerEntityId:X4} died to monster 0x{monster.EntityId:X4}'s skill: exp {expAfter}");
        if (victim.Character is { } deadCharacter) await systemLink.DiedAsync(deadCharacter.CharacterId, npcLoopCt).ConfigureAwait(false);
        foreach (var watcher in sessions)
        {
            foreach (var (opcode, payload) in watcher.Value == victim ? forVictim : forOthers)
            {
                if (!await SendFromLoopAsync(new[] { watcher }, opcode, payload, blockId, npcLoopRng, npcLoopCt).ConfigureAwait(false)) return false;
            }
        }
        return true;
    }

    tPoseidon.Core.Game.Combat.MonsterBattleTarget MonsterBattleTargetOf(MonsterEntity monster, MonsterParamEntry definition, DateTimeOffset now)
    {
        var (x, y) = monster.GetInterpolatedPosition(now);
        return new tPoseidon.Core.Game.Combat.MonsterBattleTarget(
            x, y, definition.DpLv + monster.StatDelta(MonsterStat.Dp), definition.HvLv + monster.StatDelta(MonsterStat.Hv),
            definition.LkLv + monster.StatDelta(MonsterStat.Lk), monster.Level, monster.CurrentHp, MaxHp: monster.MaxHp);
    }

    (int X, int Y)? HarconFighterAt(ushort entityId, ushort mapId, DateTimeOffset now)
    {
        if (monsterManager.Get(entityId) is not { IsDead: false } fighter || fighter.MapId != mapId) return null;
        var (x, y) = fighter.GetInterpolatedPosition(now);
        return (x, y);
    }

    int HarconReachOf(ushort entityId) =>
        monsterManager.Get(entityId) is { } reaching && combatInfoByType.TryGetValue(reaching.MonsterParamId, out var reach) ? reach.MaxAttackRange : 0;

    async Task<(SwingAdditions Additions, ushort OwnerEntityId, bool Sent)> MonsterStruckByMonsterAsync(
        tPoseidon.Core.Game.Combat.PlayerStrike strike,
        MonsterEntity victim,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        if (strike.Killed)
        {
            var (additions, ownerEntityId) = await ResolveStrikeAsync(
                strike, null, mapId, sessions, zoneGeometryByMap.GetValueOrDefault(mapId), now,
                new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>(), SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
            return (additions, ownerEntityId, true);
        }
        if (harcon.BattleOf(victim.EntityId) is { } battle && battle.TurnsOnAttacker(victim.EntityId, strike.AttackerEntityId)
            && !monsterBattleCycle.Retarget(victim, strike.AttackerEntityId)
            && monsterBattleCycle.Engage(victim, strike.AttackerEntityId, now) is { } halted)
        {
            foreach (var (opcode, payload, blockId) in MonsterBattleFrames.Encode(halted, MeleeSwingFrames.NoBlock))
            {
                if (!await SendFromLoopAsync(sessions, opcode, payload, blockId, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                {
                    return (SwingAdditions.None, victim.OwnerEntityId, false);
                }
            }
        }
        return (SwingAdditions.None, victim.OwnerEntityId, true);
    }

    async Task<bool> MonsterSwingsAtMonsterAsync(
        tPoseidon.Core.Game.Combat.MonsterBattleSwung blow,
        MonsterEntity victim,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        if (victim.IsDead) return true;
        var hpBefore = victim.CurrentHp;
        victim.ApplyDamage(blow.Damage);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: blow.EntityId, TargetEntityId: victim.EntityId,
            AttackerX: blow.X, AttackerY: blow.Y, TargetX: blow.TargetX, TargetY: blow.TargetY,
            Damage: blow.Damage, MobHpAfter: victim.CurrentHp, HitFlags: 0, HitKind: blow.Result, Killed: victim.IsDead,
            NextSwingAtUtc: now, MonsterParamId: victim.MonsterParamId, MobMaxHp: victim.MaxHp, MobHpBefore: hpBefore);
        Console.WriteLine($"{LogTag} [harcon] monster 0x{blow.EntityId:X4} -> monster 0x{victim.EntityId:X4}: damage {blow.Damage}, hp {victim.CurrentHp}/{victim.MaxHp}");
        var (additions, ownerEntityId, sent) = await MonsterStruckByMonsterAsync(strike, victim, sessions, mapId, now).ConfigureAwait(false);
        return sent && await SendStrikeFramesAsync(
            MeleeSwingFrames.Encode(strike, commandBlockIds.Next(), additions),
            strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<bool> MonsterHitMonsterAsync(
        MonsterEntity caster,
        MonsterEntity victim,
        SkillHit hit,
        ushort blockId,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        if (victim.IsDead) return true;
        var hpBefore = victim.CurrentHp;
        victim.ApplyDamage(hit.Damage);
        var (casterX, casterY) = caster.GetInterpolatedPosition(now);
        var (victimX, victimY) = victim.GetInterpolatedPosition(now);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: caster.EntityId, TargetEntityId: victim.EntityId,
            AttackerX: casterX, AttackerY: casterY, TargetX: victimX, TargetY: victimY,
            Damage: hit.Damage, MobHpAfter: victim.CurrentHp, HitFlags: 0, HitKind: (uint)hit.Kind, Killed: victim.IsDead,
            NextSwingAtUtc: now, MonsterParamId: victim.MonsterParamId, MobMaxHp: victim.MaxHp, MobHpBefore: hpBefore);
        Console.WriteLine($"{LogTag} [harcon] monster 0x{caster.EntityId:X4} skill hits monster 0x{victim.EntityId:X4}: damage {hit.Damage}, hp {victim.CurrentHp}/{victim.MaxHp}");
        var (additions, ownerEntityId, sent) = await MonsterStruckByMonsterAsync(strike, victim, sessions, mapId, now).ConfigureAwait(false);
        return sent && await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaVictim(strike, hit, blockId, additions),
            caster.EntityId, victim.EntityId, ownerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<bool> MonsterCastOnMonsterAsync(
        MonsterEntity caster,
        MonsterParamEntry definition,
        SkillDefinition skill,
        tPoseidon.Core.LibConfig.SkillTableRow row,
        MonsterEntity target,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        var attacker = MonsterAttackerOf(caster, definition);
        var coefficient = row.Value(MonsterSkills.ApplyRatioColumn) ?? 0f;
        var power = row.Amount(MonsterSkills.PowerColumn) ?? 0;
        var radius = (float)(row.Amount(MonsterSkills.RadiusColumn) ?? 0);
        var at = caster.GetInterpolatedPosition(now);
        var targetAt = target.GetInterpolatedPosition(now);
        async Task<bool> BlockAsync(SwingFrame header, IReadOnlyList<MonsterEntity> victims, int loss, int element, ushort blockId)
        {
            if (!await SendStrikeFramesAsync(new[] { header }, caster.EntityId, target.EntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false)) return false;
            foreach (var victim in victims)
            {
                if (victim.IsDead || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition) continue;
                SkillHit hit;
                lock (npcLoopRng)
                {
                    hit = SkillDamage.Magic(attacker, power, MonsterTarget(victim, victimDefinition, element), coefficient, loss, element, npcLoopRng);
                }
                if (!await MonsterHitMonsterAsync(caster, victim, hit, blockId, sessions, mapId, now).ConfigureAwait(false)) return false;
            }
            return await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                caster.EntityId, target.EntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
        }
        Console.WriteLine($"{LogTag} [harcon] monster 0x{caster.EntityId:X4} casts skill {skill.Id} (action {skill.ActionId}) at monster 0x{target.EntityId:X4}");
        switch (skill.ActionId)
        {
            case MonsterSkills.ChainArrow:
            {
                var imitation = MonsterSkills.ImitationOf(MonsterSkills.ChainArrow)!.Value;
                for (var arrow = 0; arrow < (row.Amount(MonsterSkills.CountColumn) ?? 0) && !target.IsDead; arrow++)
                {
                    var arrowBlock = commandBlockIds.Next();
                    var tail = new tPoseidon.Core.Net.PacketWriter(10);
                    tail.WriteUInt16(target.EntityId);
                    tail.WriteUInt16(at.x);
                    tail.WriteUInt16(at.y);
                    tail.WriteUInt16(targetAt.x);
                    tail.WriteUInt16(targetAt.y);
                    if (!await BlockAsync(
                        MonsterHeaderFrame(caster.EntityId, caster.EntityId, imitation.Action, imitation.Skill, tail.ToArray(), arrowBlock),
                        new[] { target }, MonsterSkills.MagicLoss, MonsterSkills.NoPropertyElement, arrowBlock).ConfigureAwait(false))
                    {
                        return false;
                    }
                }
                return true;
            }
            case MonsterSkills.Thunderbolt:
            {
                var victims = (harcon.BattleOf(caster.EntityId)?.EnemiesOf(caster.EntityId) ?? Enumerable.Empty<ushort>())
                    .Select(monsterManager.Get)
                    .OfType<MonsterEntity>()
                    .Where(victim => !victim.IsDead && victim.MapId == mapId)
                    .Select(victim => (Victim: victim, At: victim.GetInterpolatedPosition(now)))
                    .Select(entry => (entry.Victim, Distance: SkillAreaRules.Distance(targetAt.x, targetAt.y, entry.At.x, entry.At.y)))
                    .Where(entry => entry.Victim.EntityId == target.EntityId || entry.Distance <= radius)
                    .OrderBy(entry => entry.Victim.EntityId == target.EntityId ? 0 : 1)
                    .ThenBy(entry => entry.Distance)
                    .Select(entry => entry.Victim)
                    .Take(MonsterSkills.ThunderboltVictims)
                    .ToList();
                var tail = new tPoseidon.Core.Net.PacketWriter(2 + 2 * victims.Count);
                WriteVictimIds(tail, victims.Select(victim => victim.EntityId).ToList());
                var boltBlock = commandBlockIds.Next();
                return await BlockAsync(
                    MonsterHeaderFrame(caster.EntityId, target.EntityId, skill.ActionId, skill.Id, tail.ToArray(), boltBlock),
                    victims, MonsterSkills.ThunderLoss, SkillActions.ElectricElement, boltBlock).ConfigureAwait(false);
            }
            default:
            {
                if (MonsterSkills.SingleMagicOf(skill.ActionId) is not { } magic) return true;
                var hitBlock = commandBlockIds.Next();
                return await BlockAsync(
                    MonsterHeaderFrame(caster.EntityId, target.EntityId, skill.ActionId, skill.Id, Array.Empty<byte>(), hitBlock),
                    new[] { target }, magic.Loss, magic.Element, hitBlock).ConfigureAwait(false);
            }
        }
    }

    async Task<bool> PvpSwingAsync(
        InGameSessionState attacker,
        InGameSessionState victim,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        if (attacker.IsDead || attacker.NextSwingAt > now) return true;
        if (victim.IsDead || victim.Stats is not { } victimStats) return true;
        var hpBefore = victimStats.CurrentHp;
        tPoseidon.Core.Game.Combat.MeleeOutcome outcome;
        lock (npcLoopRng)
        {
            outcome = tPoseidon.Core.Game.PvP.PvpHitRules.Strike(
                attacker.PlayerAp, attacker.PlayerAc, attacker.PvpMode, victim.PlayerDp, victim.PlayerHv, hpBefore,
                victim.IsProtected(now), npcLoopRng);
        }
        var fatal = outcome.Damage > 0 && outcome.Damage >= hpBefore;
        attacker.NextSwingAt = fatal
            ? default
            : combatInfoByType.TryGetValue(attacker.PlayerCharacterType, out var attackerInfo) && attackerInfo.AttackMs > 0
                ? now.AddMilliseconds(attackerInfo.PlayerSwingDelayMs(attacker.PlayerDx))
                : now;
        if (fatal) attacker.CurrentTargetEntityId = 0;
        attacker.SwingsOnTarget = fatal ? 0 : attacker.SwingsOnTarget + 1;
        attacker.FollowSettled = !fatal;

        var blockId = commandBlockIds.Next();
        var beforeHit = new List<SwingFrame>();
        if (tPoseidon.Core.Game.PvP.PvpHitRules.BreaksPeace(victim.PvpMode, victim.PkRecordMode, attackerIsPlayer: true))
        {
            victim.PkRecordMode = tPoseidon.Core.Game.PvP.PkRules.RecordSkirmish;
            beforeHit.Add(new SwingFrame(Opcode.S2C_PK_PEACE_BROKEN, Array.Empty<byte>(), blockId, SwingAudience.Owner));
            Console.WriteLine($"{LogTag} [pk] 0x{victim.PlayerEntityId:X4} was hit in peace mode by 0x{attacker.PlayerEntityId:X4}: back to PK mode");
        }
        var hpAfter = victimStats.TakeHit(outcome.Damage);
        if (outcome.Damage > 0)
        {
            victim.Recovery.PauseAfterDamage();
            if (victim.Dig is not null)
            {
                if (fatal) victim.Dig = null;
                else await drillController.InterruptAsync(victim).ConfigureAwait(false);
            }
        }
        if (outcome.Damage > 0 && victim.Cast is { IsUnderWay: true } interrupted
            && (fatal || (interrupted.Skill.Has(SkillDefinition.CancelledByDamageOption)
                && !skillBook.GrantsAbility(victim.Skills, SkillBook.InvincibleCastingAbility))))
        {
            victim.Cast = null;
            if (!fatal) beforeHit.Add(new SwingFrame(Opcode.SKILL_CAST_CANCEL, Array.Empty<byte>(), MeleeSwingFrames.NoBlock, SwingAudience.Owner));
        }
        Console.WriteLine($"{LogTag} [pk] 0x{attacker.PlayerEntityId:X4} -> 0x{victim.PlayerEntityId:X4} result={outcome.Result} damage={outcome.Damage} playerHp={hpAfter} (PvP mode {attacker.PvpMode}, factor {tPoseidon.Core.Game.PvP.PvpHitRules.DamageFactor(attacker.PvpMode)})");

        var afterHit = new List<SwingFrame>();
        if (fatal)
        {
            var wasAttacking = victim.CurrentTargetEntityId != 0;
            if (wasAttacking)
            {
                monsterManager.Get(victim.CurrentTargetEntityId)?.RemoveAttacker(victim.PlayerEntityId);
                victim.CurrentTargetEntityId = 0;
                victim.NextSwingAt = default;
                victim.SwingsOnTarget = 0;
            }
            victim.ChaseEndsAt = default;
            victim.IsDead = true;
            victim.Engagements.TakeAll();
            foreach (var orphaned in monsterManager.GetByMap(mapId)) orphaned.ReleaseOwner(victim.PlayerEntityId);
            var expAfter = victimStats.Die(tPoseidon.Core.Game.PvP.PkRules.LosesExpOnDeath(victim.PvpMode));
            afterHit.Add(new SwingFrame(Opcode.S2C_EXP_VALUE, PlayerDeathFrames.ExpValue(expAfter), blockId, SwingAudience.Owner));
            foreach (var (deathOpcode, deathPayload) in PlayerDeathFrames.ForOthers(
                victim.PlayerEntityId, victim.PlayerX, victim.PlayerY, victim.PlayerMoveType, wasAttacking))
            {
                afterHit.Add(new SwingFrame(deathOpcode, deathPayload, blockId, SwingAudience.Everyone));
            }
            foreach (var (huntOpcode, huntPayload) in victim.Quests?.SuspendHuntForDeath() ?? Array.Empty<(Opcode Opcode, byte[] Payload)>())
            {
                afterHit.Add(new SwingFrame(huntOpcode, huntPayload, blockId, SwingAudience.Owner));
            }
            Console.WriteLine($"{LogTag} [pk] player 0x{victim.PlayerEntityId:X4} was killed by player 0x{attacker.PlayerEntityId:X4}: exp {expAfter}");
        }

        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: attacker.PlayerEntityId,
            TargetEntityId: victim.PlayerEntityId,
            AttackerX: attacker.PlayerX,
            AttackerY: attacker.PlayerY,
            TargetX: victim.PlayerX,
            TargetY: victim.PlayerY,
            Damage: outcome.Damage,
            MobHpAfter: (int)Math.Min(hpAfter, int.MaxValue),
            HitFlags: 0,
            HitKind: outcome.Result,
            Killed: fatal,
            NextSwingAtUtc: attacker.NextSwingAt,
            MonsterParamId: 0,
            MobMaxHp: victim.PlayerMaxHp,
            MobHpBefore: hpBefore);
        if (!await SendStrikeFramesAsync(
            MeleeSwingFrames.EncodeOnPlayer(strike, blockId, beforeHit, afterHit),
            attacker.PlayerEntityId, victim.PlayerEntityId, victim.PlayerEntityId, sessions).ConfigureAwait(false))
        {
            return false;
        }
        if (!fatal) return true;
        if (victim.Character is { } deadCharacter) await systemLink.DiedAsync(deadCharacter.CharacterId, npcLoopCt).ConfigureAwait(false);
        await pk.KillCreditAsync(victim, attacker).ConfigureAwait(false);
        await guildGames.PlayerKilledAsync(victim, attacker).ConfigureAwait(false);
        return true;
    }

    async Task<bool> MonsterCastAsync(
        tPoseidon.Core.Game.Combat.MonsterBattleCast cast,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        if (monsterManager.Get(cast.EntityId) is not { IsDead: false } monster
            || monsterCatalog.GetById(monster.MonsterParamId) is not { } definition
            || skillBook.Find(cast.SkillId) is not { } skill
            || skill.ActionAt(cast.Level) is not { } row)
        {
            return true;
        }
        if (!string.IsNullOrEmpty(cast.Speech))
        {
            var speech = new tPoseidon.Core.Net.PacketWriter(3 + cast.Speech.Length * 2);
            speech.WriteUInt16(monster.EntityId);
            speech.WriteBytes(System.Text.Encoding.GetEncoding(ScrollSkills.TextCodePage).GetBytes(cast.Speech));
            speech.WriteByte(0);
            if (!await SendFromLoopAsync(sessions, Opcode.S2C_MONSTER_SPEECH, speech.ToArray(), MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
        }
        if (cast.TargetEntityId != monster.EntityId && monsterManager.Get(cast.TargetEntityId) is { IsDead: false } struckMonster)
        {
            return await MonsterCastOnMonsterAsync(monster, definition, skill, row, struckMonster, sessions, mapId, now).ConfigureAwait(false);
        }
        var targetSession = sessions.FirstOrDefault(entry => entry.Value.PlayerEntityId == cast.TargetEntityId);
        var target = targetSession.Value is { IsDead: false } ? targetSession.Value : null;
        var at = monster.GetInterpolatedPosition(now);
        var attacker = MonsterAttackerOf(monster, definition);
        var coefficient = row.Value(MonsterSkills.ApplyRatioColumn) ?? 0f;
        var power = row.Amount(MonsterSkills.PowerColumn) ?? 0;
        var radius = (float)(row.Amount(MonsterSkills.RadiusColumn) ?? 0);
        Console.WriteLine($"{LogTag} [skill] monster 0x{monster.EntityId:X4} casts skill {skill.Id} level {cast.Level} (action {skill.ActionId}) at 0x{cast.TargetEntityId:X4}");
        SkillHit Magic(InGameSessionState victim, long magicPower, long loss, int element)
        {
            lock (npcLoopRng)
            {
                return SkillDamage.Magic(attacker, magicPower, PlayerTargetOf(victim, element), coefficient, loss, element, npcLoopRng);
            }
        }
        SkillHit Physical(InGameSessionState victim, long physicalPower)
        {
            lock (npcLoopRng)
            {
                return SkillDamage.Physical(attacker, physicalPower, PlayerTargetOf(victim, SkillActions.NoElement), coefficient, SkillActions.NoElement, npcLoopRng);
            }
        }
        async Task<bool> OneBlockAsync(SwingFrame header, IReadOnlyList<(KeyValuePair<S2SessionContext, InGameSessionState> Victim, SkillHit Hit)> hits, ushort blockId)
        {
            if (!await SendStrikeFramesAsync(new[] { header }, monster.EntityId, cast.TargetEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false)) return false;
            foreach (var (victim, hit) in hits)
            {
                if (!await MonsterHitPlayerAsync(monster, victim, hit, blockId, sessions, mapId).ConfigureAwait(false)) return false;
            }
            return await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                monster.EntityId, cast.TargetEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
        }

        if (HarconSkills.IsHarcon(skill.ActionId))
        {
            return await HarconCastAsync(monster, skill, row, sessions, now).ConfigureAwait(false);
        }
        if (MonsterSkills.IsObject(skill.ActionId))
        {
            return await MonsterObjectCastAsync(monster, definition, skill, row, targetSession, sessions, mapId, now).ConfigureAwait(false);
        }
        switch (skill.ActionId)
        {
            case MonsterSkills.Heal:
            {
                var (raised, healed) = MonsterSkills.Healing(monster.CurrentHp, monster.MaxHp, row.Value(MonsterSkills.RatioColumn) ?? 0f);
                monster.RaiseHp(raised);
                var imitation = MonsterSkills.ImitationOf(MonsterSkills.Heal)!.Value;
                if (target is not null && !await SendFromLoopAsync(
                    new[] { targetSession }, Opcode.S2C_OP_011E_TBD, SkillFrames.MonsterHpUpdate(monster.EntityId, monster.CurrentHp),
                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                {
                    return false;
                }
                var healBlock = commandBlockIds.Next();
                var tail = new tPoseidon.Core.Net.PacketWriter(11);
                tail.WriteUInt16(monster.EntityId);
                tail.WriteUInt16(at.x);
                tail.WriteUInt16(at.y);
                tail.WriteUInt16(at.x);
                tail.WriteUInt16(at.y);
                tail.WriteByte(healed ? (byte)1 : (byte)0);
                return await OneBlockAsync(
                    MonsterHeaderFrame(monster.EntityId, monster.EntityId, imitation.Action, imitation.Skill, tail.ToArray(), healBlock),
                    Array.Empty<(KeyValuePair<S2SessionContext, InGameSessionState>, SkillHit)>(), healBlock).ConfigureAwait(false);
            }
            case MonsterSkills.ChainArrow when target is not null:
            {
                var imitation = MonsterSkills.ImitationOf(MonsterSkills.ChainArrow)!.Value;
                for (var arrow = 0; arrow < (row.Amount(MonsterSkills.CountColumn) ?? 0) && !target.IsDead; arrow++)
                {
                    var arrowBlock = commandBlockIds.Next();
                    var tail = new tPoseidon.Core.Net.PacketWriter(10);
                    tail.WriteUInt16(target.PlayerEntityId);
                    tail.WriteUInt16(at.x);
                    tail.WriteUInt16(at.y);
                    tail.WriteUInt16(target.PlayerX);
                    tail.WriteUInt16(target.PlayerY);
                    if (!await OneBlockAsync(
                        MonsterHeaderFrame(monster.EntityId, monster.EntityId, imitation.Action, imitation.Skill, tail.ToArray(), arrowBlock),
                        new[] { (targetSession, Magic(target, power, MonsterSkills.MagicLoss, MonsterSkills.NoPropertyElement)) }, arrowBlock).ConfigureAwait(false))
                    {
                        return false;
                    }
                }
                return true;
            }
            case MonsterSkills.MagicRing when target is not null:
            {
                var imitation = MonsterSkills.ImitationOf(MonsterSkills.MagicRing)!.Value;
                var ringBlock = commandBlockIds.Next();
                var hits = PlayersAround(sessions, (target.PlayerX, target.PlayerY), radius)
                    .Select(victim =>
                    {
                        var hit = Magic(victim.Value, power, 0, MonsterSkills.NoPropertyElement);
                        var distance = SkillAreaRules.Distance(target.PlayerX, target.PlayerY, victim.Value.PlayerX, victim.Value.PlayerY);
                        return (victim, hit with { Damage = MonsterSkills.RingFalloff(hit.Damage, distance, radius) });
                    })
                    .ToList();
                return await OneBlockAsync(
                    MonsterHeaderFrame(monster.EntityId, monster.EntityId, imitation.Action, imitation.Skill, BitConverter.GetBytes(target.PlayerEntityId), ringBlock),
                    hits, ringBlock).ConfigureAwait(false);
            }
            case MonsterSkills.FireBreath or MonsterSkills.ThunderBreath when target is not null:
            {
                foreach (var victim in PlayersAround(sessions, (target.PlayerX, target.PlayerY), radius))
                {
                    var breathBlock = commandBlockIds.Next();
                    if (!await OneBlockAsync(
                        MonsterHeaderFrame(monster.EntityId, monster.EntityId, skill.ActionId, skill.Id, BitConverter.GetBytes(victim.Value.PlayerEntityId), breathBlock),
                        new[] { (victim, Magic(victim.Value, power, MonsterSkills.MagicLoss, MonsterSkills.NoPropertyElement)) }, breathBlock).ConfigureAwait(false))
                    {
                        return false;
                    }
                }
                return true;
            }
            case MonsterSkills.MagicStorm or MonsterSkills.EarthDivide when target is not null:
            {
                var storm = skill.ActionId == MonsterSkills.MagicStorm;
                if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(storm ? GroundSkills.ManaStormHit : GroundSkills.EarthDivideHit) is not { } groundHitKind) return true;
                var struck = PlayersAround(sessions, (target.PlayerX, target.PlayerY), radius);
                foreach (var victim in storm ? struck : struck.Take(MonsterSkills.EarthDivideVictims).ToList())
                {
                    var groundBlock = commandBlockIds.Next();
                    var (groundSent, _) = await MonsterEffectOnAsync(
                        monster, skill, row, groundHitKind, victim, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied, coefficient, power, sessions, groundBlock,
                        headerSkillId: storm ? MonsterSkills.MagicStormHeaderSkill : skill.Id, headerClass: -1).ConfigureAwait(false);
                    if (!groundSent
                        || !await SendStrikeFramesAsync(
                            SkillFrames.EncodeAreaEnd(groundBlock, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                            monster.EntityId, victim.Value.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
                    {
                        return false;
                    }
                }
                return true;
            }
            case MonsterSkills.Thunderbolt when target is not null:
            {
                var victims = PlayersAround(sessions, (target.PlayerX, target.PlayerY), radius).Take(MonsterSkills.ThunderboltVictims).ToList();
                var tail = new tPoseidon.Core.Net.PacketWriter(2 + 2 * victims.Count);
                WriteVictimIds(tail, victims.Select(victim => victim.Value.PlayerEntityId).ToList());
                var boltBlock = commandBlockIds.Next();
                return await OneBlockAsync(
                    MonsterHeaderFrame(monster.EntityId, target.PlayerEntityId, skill.ActionId, skill.Id, tail.ToArray(), boltBlock),
                    victims.Select(victim => (victim, Magic(victim.Value, power, MonsterSkills.ThunderLoss, SkillActions.ElectricElement))).ToList(),
                    boltBlock).ConfigureAwait(false);
            }
            case MonsterSkills.WindSlash:
            {
                var lands = RollsApplyRatio(coefficient);
                var victims = lands
                    ? PlayersAround(sessions, (at.x, at.y), radius).Take(MonsterSkills.WindSlashVictims).ToList()
                    : new List<KeyValuePair<S2SessionContext, InGameSessionState>>();
                var tail = new tPoseidon.Core.Net.PacketWriter(3 + 2 * victims.Count);
                tail.WriteByte(lands ? (byte)1 : (byte)0);
                WriteVictimIds(tail, victims.Select(victim => victim.Value.PlayerEntityId).ToList());
                var slashBlock = commandBlockIds.Next();
                return await OneBlockAsync(
                    MonsterHeaderFrame(monster.EntityId, monster.EntityId, skill.ActionId, skill.Id, tail.ToArray(), slashBlock),
                    victims.Select(victim => (victim, Magic(victim.Value, power, MonsterSkills.MagicLoss, SkillActions.WindElement))).ToList(),
                    slashBlock).ConfigureAwait(false);
            }
            case MonsterSkills.SharpBrandish or MonsterSkills.Siren when target is not null:
            {
                var physicalPower = skill.ActionId == MonsterSkills.Siren
                    ? MonsterSkills.SirenPower(monster.CurrentHp, attacker.Dp, row.Value(MonsterSkills.RatioColumn) ?? 0f)
                    : ObjectHits.RatioPower(attacker.Ap, row.Value(MonsterSkills.RatioColumn) ?? 0f);
                var hitBlock = commandBlockIds.Next();
                return await OneBlockAsync(
                    MonsterHeaderFrame(monster.EntityId, target.PlayerEntityId, skill.ActionId, skill.Id, Array.Empty<byte>(), hitBlock),
                    new[] { (targetSession, Physical(target, physicalPower)) }, hitBlock).ConfigureAwait(false);
            }
            default:
            {
                if (target is null || MonsterSkills.SingleMagicOf(skill.ActionId) is not { } magic) return true;
                var hitBlock = commandBlockIds.Next();
                return await OneBlockAsync(
                    MonsterHeaderFrame(monster.EntityId, target.PlayerEntityId, skill.ActionId, skill.Id, Array.Empty<byte>(), hitBlock),
                    new[] { (targetSession, Magic(target, power, magic.Loss, magic.Element)) }, hitBlock).ConfigureAwait(false);
            }
        }
    }

    async Task<(bool Sent, tPoseidon.Core.Game.Skills.Effects.SkillEffect? Effect)> MonsterEffectOnAsync(
        MonsterEntity caster,
        SkillDefinition skill,
        tPoseidon.Core.LibConfig.SkillTableRow row,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectKind kind,
        KeyValuePair<S2SessionContext, InGameSessionState>? playerOwner,
        tPoseidon.Core.Game.Skills.Effects.SkillEffectMode mode,
        float amount,
        long magnitude,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort blockId,
        (ushort X, ushort Y)? point = null,
        int? tickMs = null,
        int? durationMs = null,
        bool announce = true,
        int? headerSkillId = null,
        sbyte? headerClass = null)
    {
        var book = playerOwner is { } holder ? holder.Value.Effects : MonsterEffectsOf(caster.EntityId);
        var ownerId = playerOwner is { } named ? named.Value.PlayerEntityId : caster.EntityId;
        var effect = new tPoseidon.Core.Game.Skills.Effects.SkillEffect
        {
            ObjectId = idAllocator.NewId(), Kind = kind, SkillId = headerSkillId ?? skill.Id, EsClass = headerClass ?? kind.HeaderClass ?? (sbyte)skill.Row.EsClass,
            CasterEntityId = caster.EntityId, OwnerEntityId = ownerId, Amount = amount, Magnitude = magnitude, Mode = mode, Action = row,
            DurationMs = durationMs ?? kind.FixedDurationMs ?? (int)Math.Clamp((long)(row.Amount(kind.DurationColumn) ?? 0) * kind.MillisecondsPerUnit, 0, int.MaxValue),
            TickMs = tickMs ?? kind.PeriodMs, Point = point,
        };
        if (book.Register(effect) is not { } registration)
        {
            idAllocator.Release(effect.ObjectId);
            return (true, null);
        }
        foreach (var (removed, _) in registration.Removed)
        {
            if (!await EndSkillEffectAsync(removed, sessions).ConfigureAwait(false)) return (false, null);
            if (playerOwner is { } evicted)
            {
                if ((removed.IsApplied && !await SendEffectChangesAsync(evicted, null, removed.Kind.Stats, sessions).ConfigureAwait(false))
                    || !await AfterPlayerEffectEndsAsync(evicted, removed, sessions).ConfigureAwait(false))
                {
                    return (false, null);
                }
            }
            else
            {
                UndoMonsterEffect(caster, removed);
            }
        }
        var sent = !announce || await SendFromLoopAsync(
            sessions, Opcode.S2C_OP_00E0_TBD, SkillEffectFrames.Started(effect, ownerId), blockId, npcLoopRng, npcLoopCt).ConfigureAwait(false);
        if (sent && effect.IsApplied && kind.ChangesStat)
        {
            sent = playerOwner is { } changed
                ? await SendEffectChangesAsync(changed, null, kind.Stats, sessions).ConfigureAwait(false)
                : await SendEffectChangesAsync(null, caster, kind.Stats, sessions).ConfigureAwait(false);
        }
        return (sent, effect);
    }

    tPoseidon.Core.Game.Skills.Effects.SkillEffectMode MonsterModeRoll(float applyRatio)
    {
        lock (npcLoopRng)
        {
            return tPoseidon.Core.Game.Skills.Effects.SkillEffectBook.RollMode(applyRatio, 0, npcLoopRng);
        }
    }

    async Task<bool> MonsterObjectCastAsync(
        MonsterEntity monster,
        MonsterParamEntry definition,
        SkillDefinition skill,
        tPoseidon.Core.LibConfig.SkillTableRow row,
        KeyValuePair<S2SessionContext, InGameSessionState> targetSession,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        var kind = tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(skill.ActionId);
        if (kind is null && skill.ActionId is not (MonsterSkills.ElectricShock or MonsterSkills.Destruction or MonsterSkills.Poison)) return true;
        var target = targetSession.Value is { IsDead: false } ? targetSession.Value : null;
        var attacker = MonsterAttackerOf(monster, definition);
        var coefficient = row.Value(MonsterSkills.ApplyRatioColumn) ?? 0f;
        var power = row.Amount(MonsterSkills.PowerColumn) ?? 0;
        var ratio = row.Value(MonsterSkills.RatioColumn) ?? 0f;
        var blockId = commandBlockIds.Next();
        SkillHit Magic(InGameSessionState victim, int element)
        {
            lock (npcLoopRng)
            {
                return SkillDamage.Magic(attacker, power, PlayerTargetOf(victim, element), coefficient, MonsterSkills.MagicLoss, element, npcLoopRng);
            }
        }
        SkillHit Physical(InGameSessionState victim, long physicalPower)
        {
            lock (npcLoopRng)
            {
                return SkillDamage.Physical(attacker, physicalPower, PlayerTargetOf(victim, SkillActions.NoElement), coefficient, SkillActions.NoElement, npcLoopRng);
            }
        }
        async Task<bool> CloseAsync() => await SendStrikeFramesAsync(
            SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
            monster.EntityId, monster.EntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
        var applied = tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied;

        switch (skill.ActionId)
        {
            case MonsterSkills.Charging:
                return await MonsterChargingAsync(monster, definition, skill, row, sessions, mapId, now).ConfigureAwait(false);
            case MonsterSkills.Warp:
                return await MonsterWarpAsync(monster, skill, row, sessions, now).ConfigureAwait(false);
            case MonsterSkills.DefenseParalysis or MonsterSkills.PowerStun when target is not null:
            {
                var stun = skill.ActionId == MonsterSkills.PowerStun;
                if (stun && target.Effects.Snapshot().Any(held => held.IsApplied && held.Kind.ActionId == MonsterSkills.FreeMotionScroll)) return true;
                var mode = MonsterModeRoll(coefficient);
                var hit = Physical(target, ObjectHits.RatioPower(attacker.Ap, ratio));
                var (sent, _) = await MonsterEffectOnAsync(monster, skill, row, kind!, targetSession, mode, 0f, 0, sessions, blockId).ConfigureAwait(false);
                if (!sent) return false;
                if ((!stun || mode == applied) && !await MonsterHitPlayerAsync(monster, targetSession, hit, blockId, sessions, mapId).ConfigureAwait(false)) return false;
                return await CloseAsync().ConfigureAwait(false);
            }
            case MonsterSkills.WaterNet when target is not null:
            {
                var hit = Magic(target, SkillActions.WaterElement);
                var mode = MonsterModeRoll(coefficient);
                if (mode != applied) return true;
                var (sent, _) = await MonsterEffectOnAsync(
                    monster, skill, row, kind!, targetSession, mode, MonsterSkills.WaterNetDexterity(attacker.Ma, ratio), 0, sessions, blockId).ConfigureAwait(false);
                if (!sent || !await MonsterHitPlayerAsync(monster, targetSession, hit, blockId, sessions, mapId).ConfigureAwait(false)) return false;
                return await CloseAsync().ConfigureAwait(false);
            }
            case MonsterSkills.WhirlWind or MonsterSkills.CruelFlame when target is not null:
            {
                var mode = MonsterModeRoll(coefficient);
                if (mode != applied) return true;
                var first = Magic(target, MonsterSkills.DotElementOf(skill.ActionId));
                if (first.Damage < 1) return true;
                var (sent, _) = await MonsterEffectOnAsync(monster, skill, row, kind!, targetSession, mode, ratio, first.Damage, sessions, blockId).ConfigureAwait(false);
                if (!sent || !await MonsterHitPlayerAsync(monster, targetSession, first, blockId, sessions, mapId).ConfigureAwait(false)) return false;
                return await CloseAsync().ConfigureAwait(false);
            }
            case MonsterSkills.Berserk or MonsterSkills.FluentWaist or MonsterSkills.Vampiric:
            {
                var mode = kind!.CarriesMode ? MonsterModeRoll(coefficient) : applied;
                var (sent, _) = await MonsterEffectOnAsync(monster, skill, row, kind, null, mode, ratio, 0, sessions, blockId).ConfigureAwait(false);
                return sent && await CloseAsync().ConfigureAwait(false);
            }
            case MonsterSkills.ElectricShock when target is not null:
            {
                var victims = PlayersAround(sessions, (target.PlayerX, target.PlayerY), row.Amount(MonsterSkills.RadiusColumn) ?? 0)
                    .Take(MonsterSkills.ElectricShockVictims)
                    .ToList();
                var tail = new tPoseidon.Core.Net.PacketWriter(2 + 2 * victims.Count);
                WriteVictimIds(tail, victims.Select(victim => victim.Value.PlayerEntityId).ToList());
                var shockObject = idAllocator.NewId();
                idAllocator.Release(shockObject);
                var header = new SwingFrame(Opcode.S2C_OP_00E0_TBD,
                    [.. SkillFrames.EffectHeader(monster.EntityId, target.PlayerEntityId, (ushort)skill.ActionId, (sbyte)skill.Row.EsClass, (ushort)skill.Id, 0, shockObject),
                     .. tail.ToArray()],
                    blockId, SwingAudience.Everyone);
                if (!await SendStrikeFramesAsync(new[] { header }, monster.EntityId, target.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false)) return false;
                foreach (var victim in victims)
                {
                    if (!await MonsterHitPlayerAsync(monster, victim, Magic(victim.Value, SkillActions.ElectricElement), blockId, sessions, mapId).ConfigureAwait(false)) return false;
                }
                return await CloseAsync().ConfigureAwait(false);
            }
            case MonsterSkills.Destruction:
            {
                var at = monster.GetInterpolatedPosition(now);
                var mode = MonsterModeRoll(coefficient);
                var victims = mode == applied
                    ? PlayersAround(sessions, (at.x, at.y), row.Amount(MonsterSkills.RadiusColumn) ?? 0).Take(MonsterSkills.DestructionVictims).ToList()
                    : new List<KeyValuePair<S2SessionContext, InGameSessionState>>();
                var tail = new tPoseidon.Core.Net.PacketWriter(3 + 2 * victims.Count);
                tail.WriteByte((byte)mode);
                WriteVictimIds(tail, victims.Select(victim => victim.Value.PlayerEntityId).ToList());
                if (!await SendStrikeFramesAsync(
                    new[] { MonsterHeaderFrame(monster.EntityId, monster.EntityId, skill.ActionId, skill.Id, tail.ToArray(), blockId) },
                    monster.EntityId, monster.EntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
                {
                    return false;
                }
                var blast = MonsterSkills.DestructionPower(monster.MaxHp, ratio);
                foreach (var victim in victims)
                {
                    if (!await MonsterHitPlayerAsync(monster, victim, Physical(victim.Value, blast), blockId, sessions, mapId).ConfigureAwait(false)) return false;
                }
                if (!await CloseAsync().ConfigureAwait(false)) return false;
                if (!await HoldMonsterAsync(monster, MonsterSkills.DestructionHoldMs, sessions, now).ConfigureAwait(false)) return false;
                if (mode != applied || kind is null) return true;
                var (destructSent, _) = await MonsterEffectOnAsync(monster, skill, row, kind, null, applied, 0f, 0, sessions, MeleeSwingFrames.NoBlock, announce: false).ConfigureAwait(false);
                return destructSent;
            }
            case MonsterSkills.Poison when target is not null:
            {
                if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(MonsterSkills.PoisonDo) is not { } poisonKind) return true;
                foreach (var stale in target.Effects.Snapshot().Where(held => held.Kind.ActionId == MonsterSkills.PoisonDo).ToList())
                {
                    if (target.Effects.Remove(stale) && !await EndSkillEffectAsync(stale, sessions).ConfigureAwait(false)) return false;
                }
                var header = new SwingFrame(Opcode.S2C_OP_00E0_TBD,
                    [.. SkillFrames.EffectHeader(monster.EntityId, monster.EntityId, (ushort)skill.ActionId, (sbyte)skill.Row.EsClass, (ushort)skill.Id, 0),
                     .. BitConverter.GetBytes(target.PlayerEntityId)],
                    blockId, SwingAudience.Everyone);
                if (!await SendStrikeFramesAsync(new[] { header }, monster.EntityId, target.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false)
                    || !await CloseAsync().ConfigureAwait(false))
                {
                    return false;
                }
                var poisonBlock = commandBlockIds.Next();
                var (sent, poison) = await MonsterEffectOnAsync(
                    monster, skill, row, poisonKind, targetSession, applied, (float)Math.Round((row.Value(MonsterSkills.DelayColumn) ?? 0f) * 1000.0), power,
                    sessions, poisonBlock).ConfigureAwait(false);
                return sent && await SendStrikeFramesAsync(
                    SkillFrames.EncodeAreaEnd(poisonBlock, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                    monster.EntityId, target.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
            }
        }
        return true;
    }

    async Task<bool> MonsterPeriodicOnPlayerAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect effect,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId)
    {
        if (effect.Kind.Periodic is not (tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.MonsterDot or tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.MonsterPoison
                or tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.DelayedStrike)
            || effect.ElapsedMs > effect.DurationMs || monsterManager.Get(effect.CasterEntityId) is not { IsDead: false } caster
            || session.Value.Stats is not { } victimStats)
        {
            return true;
        }
        SkillHit hit;
        if (effect.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.DelayedStrike)
        {
            if (effect.PeriodCount++ > 0 || monsterCatalog.GetById(caster.MonsterParamId) is not { } strikerDefinition) return true;
            lock (npcLoopRng)
            {
                hit = SkillDamage.Magic(
                    MonsterAttackerOf(caster, strikerDefinition), effect.Magnitude, PlayerTargetOf(session.Value, effect.Kind.StrikeElement),
                    effect.Amount, effect.Kind.StrikeMagicLoss, effect.Kind.StrikeElement, npcLoopRng);
            }
        }
        else if (effect.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.MonsterDot)
        {
            if (effect.Magnitude <= 0 || !RollsApplyRatio(effect.Action?.Value(MonsterSkills.ApplyRatioColumn) ?? 0f)) return true;
            hit = new SkillHit(MonsterSkills.DotDamage(effect.Magnitude, effect.Amount), SkillDamage.MagicType, SkillDamage.KindHit,
                MonsterSkills.DotElementOf(effect.Kind.ActionId), 0);
        }
        else
        {
            if (!MonsterSkills.PoisonStrikeDue(effect.ElapsedMs, effect.PeriodCount, (int)effect.Amount)) return true;
            effect.PeriodCount++;
            var damage = MonsterSkills.PoisonDamage(effect.Magnitude, victimStats.CurrentHp);
            if (damage <= 0) return true;
            hit = new SkillHit(damage, SkillDamage.PhysicalType, MonsterSkills.PoisonKind, SkillActions.NoElement, 0);
        }
        var blockId = commandBlockIds.Next();
        return await MonsterHitPlayerAsync(caster, session, hit, blockId, sessions, mapId).ConfigureAwait(false)
            && await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                caster.EntityId, session.Value.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<(bool Sent, bool Resisted)> DebuffResistedAsync(
        SkillDefinition skill, MonsterEntity monster, List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions)
    {
        if (skill.Row.DebuffBitCode == 0
            || monsterCatalog.GetById(monster.MonsterParamId) is not { DebuffSet: > 0 } resistant
            || !monsterDebuffResists.TryGetValue(resistant.DebuffSet, out var resistRow))
        {
            return (true, false);
        }
        (bool Blocked, uint Mask) roll;
        lock (npcLoopRng)
        {
            roll = DebuffResist.Roll(resistRow, skill.Row.DebuffBitCode, () => npcLoopRng.Next(DebuffResist.RollRange));
        }
        if (!roll.Blocked) return (true, false);
        Console.WriteLine($"{LogTag} [skill] 0x{monster.EntityId:X4} resists skill {skill.Id} (debuff bits 0x{roll.Mask:X})");
        var sent = await SendFromLoopAsync(
            sessions, Opcode.S2C_DEBUFF_RESISTED, SkillFrames.DebuffResisted(monster.EntityId, roll.Mask),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
        return (sent, true);
    }

    async Task<bool> MonsterChargingAsync(
        MonsterEntity monster,
        MonsterParamEntry definition,
        SkillDefinition skill,
        tPoseidon.Core.LibConfig.SkillTableRow row,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now)
    {
        if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(MonsterSkills.ChargingDo) is not { } chargeKind) return true;
        var at = monster.GetInterpolatedPosition(now);
        var radius = row.Amount(MonsterSkills.RadiusColumn) ?? 0;
        var victims = PlayersAround(sessions, (at.x, at.y), radius).Take(MonsterSkills.ChargingVictims).ToList();
        var blockId = commandBlockIds.Next();
        if (!await SendStrikeFramesAsync(
            [MonsterHeaderFrame(monster.EntityId, monster.EntityId, skill.ActionId, skill.Id, new[] { victims.Count > 0 ? (byte)1 : (byte)0 }, blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            monster.EntityId, monster.EntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }
        var attacker = MonsterAttackerOf(monster, definition);
        var geometry = zoneGeometryByMap.GetValueOrDefault(mapId);
        foreach (var victim in victims)
        {
            var player = victim.Value;
            var mode = MonsterModeRoll(row.Value(MonsterSkills.ApplyRatioColumn) ?? 0f);
            var applied = mode == tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied;
            (int X, int Y) landing = (player.PlayerX, player.PlayerY);
            SkillHit hit = default;
            if (applied)
            {
                var destination = KnockSkills.Destination((at.x, at.y), landing, radius);
                landing = KnockSkills.Walk(landing, destination, (x, y) => KnockBlocked(geometry, x, y));
                var power = MonsterSkills.ChargingPower(
                    KnockSkills.PushedDistance((player.PlayerX, player.PlayerY), landing), monster.CurrentHp, row.Value(MonsterSkills.RatioColumn) ?? 0f);
                lock (npcLoopRng)
                {
                    hit = SkillDamage.Physical(
                        attacker, power, PlayerTargetOf(player, SkillActions.NoElement), row.Value(MonsterSkills.ApplyRatioColumn) ?? 0f,
                        SkillActions.NoElement, npcLoopRng);
                }
            }
            var childBlock = commandBlockIds.Next();
            var (sent, _) = await MonsterEffectOnAsync(
                monster, skill, row, chargeKind, victim, mode, 0f, 0, sessions, childBlock,
                point: ((ushort)Math.Clamp(landing.X, 0, ushort.MaxValue), (ushort)Math.Clamp(landing.Y, 0, ushort.MaxValue))).ConfigureAwait(false);
            if (!sent || (applied && !await MonsterHitPlayerAsync(monster, victim, hit, childBlock, sessions, mapId).ConfigureAwait(false))) return false;
            if (applied && !player.IsDead)
            {
                player.PlayerX = (ushort)Math.Clamp(landing.X, 0, ushort.MaxValue);
                player.PlayerY = (ushort)Math.Clamp(landing.Y, 0, ushort.MaxValue);
            }
            if (!await SendStrikeFramesAsync(
                SkillFrames.EncodeAreaEnd(childBlock, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                monster.EntityId, player.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
            {
                return false;
            }
        }
        return true;
    }

    async Task<bool> MonsterWarpAsync(
        MonsterEntity monster,
        SkillDefinition skill,
        tPoseidon.Core.LibConfig.SkillTableRow row,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        DateTimeOffset now)
    {
        if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(MonsterSkills.Warp) is not { } warpKind) return true;
        var at = monster.GetInterpolatedPosition(now);
        var candidates = PlayersAround(sessions, (at.x, at.y), MonsterSkills.WarpSearchRadius).Take(MonsterSkills.WarpVictims).ToList();
        if (candidates.Count == 0) return true;
        KeyValuePair<S2SessionContext, InGameSessionState> chosen;
        lock (npcLoopRng)
        {
            chosen = candidates[npcLoopRng.Next(candidates.Count)];
        }
        var mode = MonsterModeRoll(row.Value(MonsterSkills.ApplyRatioColumn) ?? 0f);
        var radius = row.Amount(MonsterSkills.RadiusColumn) ?? 0;
        var delayMs = (int)Math.Round((row.Value(MonsterSkills.DelayTimeColumn) ?? 0f) * 1000.0);
        var point = (chosen.Value.PlayerX, chosen.Value.PlayerY);
        var blockId = commandBlockIds.Next();
        var (registered, _) = await MonsterEffectOnAsync(
            monster, skill, row, warpKind, null, mode, row.Amount(MonsterSkills.MapIdColumn) ?? 0, radius, sessions, blockId,
            point: point, tickMs: delayMs, durationMs: delayMs, announce: false).ConfigureAwait(false);
        if (!registered) return false;
        var tail = new tPoseidon.Core.Net.PacketWriter(7);
        tail.WriteByte((byte)mode);
        tail.WriteUInt16(point.PlayerX);
        tail.WriteUInt16(point.PlayerY);
        tail.WriteUInt16((ushort)radius);
        Console.WriteLine($"{LogTag} [skill] monster 0x{monster.EntityId:X4} opens a warp at ({point.PlayerX},{point.PlayerY}) radius {radius}, {delayMs} ms");
        return await SendStrikeFramesAsync(
            [MonsterHeaderFrame(monster.EntityId, monster.EntityId, skill.ActionId, skill.Id, tail.ToArray(), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            monster.EntityId, monster.EntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> MonsterObjectTickAsync(
        tPoseidon.Core.Game.Skills.Effects.SkillEffect effect,
        MonsterEntity monster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        if (effect.PeriodCount++ > 0) return true;
        effect.EndNow();
        if (effect.Kind.Periodic == tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.MonsterWarp)
        {
            if (effect.Point is not { } warpAt) return true;
            foreach (var warped in PlayersAround(sessions, (warpAt.X, warpAt.Y), effect.Magnitude).Take(MonsterSkills.WarpVictims))
            {
                var player = warped.Value;
                if (player.Effects.Snapshot().Any(held => held.IsApplied && held.Kind.ActionId == MonsterSkills.WarpDefenseScroll)) continue;
                var mapTo = effect.Amount > 0 ? (ushort)effect.Amount : player.MapId;
                Console.WriteLine($"{LogTag} [skill] monster 0x{monster.EntityId:X4} warps 0x{player.PlayerEntityId:X4} to map {mapTo}");
                if (player.RequestZoneChange is { } requestZoneChange)
                {
                    _ = requestZoneChange(
                        new tPoseidon.Core.Net.Dialogs.ZoneChangeRequest(mapTo, tPoseidon.Core.Game.Warp.TeleportArrival.ChangeZoneType, null, TimeSpan.Zero),
                        npcLoopCt);
                }
            }
            return true;
        }
        if (effect.Kind.Periodic != tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.SelfDestruct || monster.IsDead) return true;
        var hpBefore = monster.CurrentHp;
        monster.ApplyDamage(hpBefore);
        var (monsterX, monsterY) = monster.GetInterpolatedPosition(now);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: monster.EntityId, TargetEntityId: monster.EntityId,
            AttackerX: monsterX, AttackerY: monsterY, TargetX: monsterX, TargetY: monsterY,
            Damage: hpBefore, MobHpAfter: monster.CurrentHp, HitFlags: 0, HitKind: SkillDamage.KindHit, Killed: true,
            NextSwingAtUtc: now, MonsterParamId: monster.MonsterParamId, MobMaxHp: monster.MaxHp, MobHpBefore: hpBefore);
        var (additions, ownerEntityId) = await ResolveStrikeAsync(
            strike, null, mapId, sessions, zoneGeometry, now, battleEvents, SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
        Console.WriteLine($"{LogTag} [skill] monster 0x{monster.EntityId:X4} self-destructs");
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeSelfDestruct(strike, commandBlockIds.Next(), additions),
            monster.EntityId, monster.EntityId, ownerEntityId, sessions).ConfigureAwait(false);
    }

    SwingFrame EventHeaderFrame(ushort casterId, ushort secondId, int actionId, sbyte effectClass, SkillDefinition skill, byte[] tail, ushort blockId) =>
        new(Opcode.S2C_OP_00E0_TBD,
            [.. SkillFrames.EffectHeader(casterId, secondId, (ushort)actionId, effectClass, (ushort)skill.Id, (ushort)skill.Row.EState), .. tail],
            blockId, SwingAudience.Everyone);

    async Task<bool> EventEmoteAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        KeyValuePair<S2SessionContext, InGameSessionState> target)
    {
        var caster = session.Value;
        var tail = Array.Empty<byte>();
        if (castSkill.ActionId == EventSkills.HeartSpring)
        {
            var near = sessions
                .Where(entry => entry.Value.MapId == caster.MapId
                    && SkillAreaRules.Distance(caster.PlayerX, caster.PlayerY, entry.Value.PlayerX, entry.Value.PlayerY) <= EventSkills.HeartSpringRadius)
                .Select(entry => entry.Value.PlayerEntityId)
                .ToList();
            var list = new tPoseidon.Core.Net.PacketWriter(2 + 2 * near.Count);
            list.WriteUInt16((ushort)near.Count);
            foreach (var nearId in near) list.WriteUInt16(nearId);
            tail = list.ToArray();
        }
        var blockId = commandBlockIds.Next();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} event skill {castSkill.Id} (action {castSkill.ActionId}) on 0x{target.Value.PlayerEntityId:X4}");
        return await SendStrikeFramesAsync(
            [EventHeaderFrame(caster.PlayerEntityId, target.Value.PlayerEntityId, castSkill.ActionId, (sbyte)castSkill.Row.EsClass, castSkill, tail, blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            caster.PlayerEntityId, target.Value.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> CoupleHeartAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow? action,
        KeyValuePair<S2SessionContext, InGameSessionState> partner,
        DateTimeOffset now)
    {
        var caster = session.Value;
        if (action is null || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(castSkill.ActionId) is not { } heartKind) return true;
        if (caster.CouplePartnerId == 0 || partner.Value.Character?.CharacterId != caster.CouplePartnerId)
        {
            Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id}: 0x{partner.Value.PlayerEntityId:X4} is not the caster's partner");
            return true;
        }
        var flightMs = EventSkills.HeartFlightMs(SkillAreaRules.Distance(caster.PlayerX, caster.PlayerY, partner.Value.PlayerX, partner.Value.PlayerY));
        var shownMs = (int)Math.Clamp(
            (long)(action.Amount(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.SecondsColumn) ?? 0) * tPoseidon.Core.Game.Skills.Effects.SkillEffect.MillisecondsPerSecond,
            0, int.MaxValue);
        return await StartSkillEffectAsync(
            partner, sessions, castSkill, heartKind, action, null, now, caster.PlayerEntityId,
            magnitude: flightMs, durationMs: flightMs + EventSkills.HeartDoMs + shownMs).ConfigureAwait(false);
    }

    async Task<bool> CoupleHeartStepAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect heart,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        DateTimeOffset now)
    {
        if (heart.PeriodCount == 0 && heart.ElapsedMs >= heart.Magnitude)
        {
            heart.PeriodCount = 1;
            if (skillBook.Find(heart.SkillId) is not { } heartSkill
                || heart.Action is not { } heartRow
                || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(EventSkills.HeartDoOf(heart.Kind.ActionId)) is not { } heartDoKind)
            {
                return true;
            }
            return await StartSkillEffectAsync(session, sessions, heartSkill, heartDoKind, heartRow, null, now, heart.CasterEntityId).ConfigureAwait(false);
        }
        if (heart.PeriodCount != 1 || heart.ElapsedMs < heart.Magnitude + EventSkills.HeartDoMs) return true;
        heart.PeriodCount = 2;
        heart.FixedDeltas = new[]
        {
            (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatLk, EventSkills.HeartLuck(session.Value.CombatBase.Lk, heart.Amount)),
        };
        return await SendEffectChangesAsync(session, null, new[] { tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.StatLk }, sessions).ConfigureAwait(false);
    }

    async Task<bool> GmBuffAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow? action,
        KeyValuePair<S2SessionContext, InGameSessionState> cheered,
        DateTimeOffset now)
    {
        if (action is null || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(EventSkills.GmDoOf(castSkill.ActionId)) is not { } gmKind) return true;
        return await StartSkillEffectAsync(cheered, sessions, castSkill, gmKind, action, null, now, session.Value.PlayerEntityId).ConfigureAwait(false);
    }

    async Task<bool> GmRegionAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        tPoseidon.Core.LibConfig.SkillTableRow action,
        (ushort X, ushort Y) point,
        ushort mapId,
        DateTimeOffset now)
    {
        var caster = session.Value;
        var pointTail = new tPoseidon.Core.Net.PacketWriter(4);
        pointTail.WriteUInt16(point.X);
        pointTail.WriteUInt16(point.Y);
        var blockId = commandBlockIds.Next();
        if (!await SendStrikeFramesAsync(
            [EventHeaderFrame(caster.PlayerEntityId, caster.PlayerEntityId, EventSkills.GmRegionCast, -1, castSkill, pointTail.ToArray(), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
        {
            return false;
        }
        var radius = action.Amount(EventSkills.RadiusColumn) ?? 0;
        var cheeredPlayers = sessions
            .Where(entry => entry.Value.PlayerEntityId != caster.PlayerEntityId && !entry.Value.IsDead && entry.Value.MapId == mapId
                && SkillAreaRules.Distance(point.X, point.Y, entry.Value.PlayerX, entry.Value.PlayerY) <= radius)
            .ToList();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} at ({point.X},{point.Y}) radius {radius}: {cheeredPlayers.Count} player(s)");
        foreach (var cheered in cheeredPlayers)
        {
            if (!await GmBuffAsync(session, sessions, castSkill, action, cheered, now).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> EventPointAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillDefinition castSkill,
        (ushort X, ushort Y) point)
    {
        var caster = session.Value;
        var pointTail = new tPoseidon.Core.Net.PacketWriter(4);
        pointTail.WriteUInt16(point.X);
        pointTail.WriteUInt16(point.Y);
        var blockId = commandBlockIds.Next();
        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} event skill {castSkill.Id} (action {castSkill.ActionId}) at ({point.X},{point.Y})");
        return await SendStrikeFramesAsync(
            [EventHeaderFrame(caster.PlayerEntityId, caster.PlayerEntityId, castSkill.ActionId, (sbyte)castSkill.Row.EsClass, castSkill, pointTail.ToArray(), blockId),
             .. SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            caster.PlayerEntityId, caster.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    async Task<bool> HarconCastAsync(
        MonsterEntity monster,
        SkillDefinition skill,
        tPoseidon.Core.LibConfig.SkillTableRow row,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        DateTimeOffset now)
    {
        if (tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(skill.ActionId) is not { } harconKind) return true;
        var at = monster.GetInterpolatedPosition(now);
        IReadOnlyList<HarconSlot> picked;
        lock (npcLoopRng)
        {
            picked = HarconSkills.Pick(HarconSkills.Slots(row, (at.x, at.y)), row.Amount(HarconSkills.HitCountColumn) ?? 0, npcLoopRng.Next);
        }
        if (picked.Count == 0) return true;
        var plan = new HarconPlan(picked);
        var blockId = commandBlockIds.Next();
        var (registered, harcon) = await MonsterEffectOnAsync(
            monster, skill, row, harconKind, null, tPoseidon.Core.Game.Skills.Effects.SkillEffectMode.Applied,
            row.Value(HarconSkills.RatioColumn) ?? 0f, row.Amount(HarconSkills.PowerColumn) ?? 0, sessions, blockId,
            point: (at.x, at.y), tickMs: HarconSkills.StepMs, durationMs: plan.LastMs + HarconSkills.StepMs, announce: false).ConfigureAwait(false);
        if (!registered || harcon is null) return registered;
        harcon.SlotPlan = plan;
        var initial = plan.Initial;
        var tail = new tPoseidon.Core.Net.PacketWriter(2 + initial.Count * 7 + 4);
        tail.WriteUInt16((ushort)initial.Count);
        foreach (var slot in initial) WriteHarconSlot(tail, slot);
        tail.WriteUInt16(at.x);
        tail.WriteUInt16(at.y);
        Console.WriteLine($"{LogTag} [skill] monster 0x{monster.EntityId:X4} skill {skill.Id}: {picked.Count} slot(s), object 0x{harcon.ObjectId:X4}, last at {plan.LastMs} ms");
        return await SendStrikeFramesAsync(
            [new SwingFrame(Opcode.S2C_OP_00E0_TBD,
                [.. SkillFrames.EffectHeader(monster.EntityId, monster.EntityId, (ushort)skill.ActionId, (sbyte)skill.Row.EsClass, (ushort)skill.Id, 0, harcon.ObjectId), .. tail.ToArray()],
                blockId, SwingAudience.Everyone),
             .. SkillFrames.EncodeAreaEnd(blockId, Array.Empty<(Opcode Opcode, byte[] Payload)>())],
            monster.EntityId, monster.EntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false);
    }

    void WriteHarconSlot(tPoseidon.Core.Net.PacketWriter writer, HarconSlot slot)
    {
        writer.WriteByte(slot.Slot);
        writer.WriteUInt16((ushort)Math.Clamp(slot.X, 0, ushort.MaxValue));
        writer.WriteUInt16((ushort)Math.Clamp(slot.Y, 0, ushort.MaxValue));
        writer.WriteUInt16((ushort)Math.Clamp(slot.HitMs, 0, ushort.MaxValue));
    }

    async Task<bool> HarconTickAsync(
        tPoseidon.Core.Game.Skills.Effects.SkillEffect harcon,
        MonsterEntity monster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId)
    {
        if (harcon.SlotPlan is not { } plan || harcon.Action is not { } row || monsterCatalog.GetById(monster.MonsterParamId) is not { } definition)
        {
            harcon.EndNow();
            return true;
        }
        var hitRange = row.Amount(HarconSkills.HitRangeColumn) ?? 0;
        foreach (var (slot, step) in plan.TakeDue(harcon.ElapsedMs))
        {
            if (step == HarconStep.Add)
            {
                var added = new tPoseidon.Core.Net.PacketWriter(9);
                added.WriteUInt16(harcon.ObjectId);
                WriteHarconSlot(added, slot);
                if (!await SendFromLoopAsync(sessions, Opcode.S2C_EFFECT_SLOT_ADDED, added.ToArray(), MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)) return false;
                continue;
            }
            var fired = new tPoseidon.Core.Net.PacketWriter(3);
            fired.WriteUInt16(harcon.ObjectId);
            fired.WriteByte(slot.Slot);
            if (!await SendFromLoopAsync(sessions, Opcode.S2C_EFFECT_SLOT_FIRED, fired.ToArray(), MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)) return false;
            var attacker = MonsterAttackerOf(monster, definition);
            foreach (var victim in PlayersAround(sessions, (slot.X, slot.Y), hitRange))
            {
                SkillHit hit;
                lock (npcLoopRng)
                {
                    hit = SkillDamage.Magic(
                        attacker, harcon.Magnitude, PlayerTargetOf(victim.Value, MonsterSkills.NoPropertyElement), harcon.Amount,
                        MonsterSkills.MagicLoss, MonsterSkills.NoPropertyElement, npcLoopRng);
                }
                var hitBlock = commandBlockIds.Next();
                if (!await MonsterHitPlayerAsync(monster, victim, hit, hitBlock, sessions, mapId).ConfigureAwait(false)
                    || !await SendStrikeFramesAsync(
                        SkillFrames.EncodeAreaEnd(hitBlock, Array.Empty<(Opcode Opcode, byte[] Payload)>()),
                        monster.EntityId, victim.Value.PlayerEntityId, MonsterEntity.NoOwner, sessions).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }
        if (plan.Done) harcon.EndNow();
        return true;
    }

    const int GunIdealRangeNumerator = 3;
    const int GunIdealRangeShift = 2;

    async Task<bool> GunShotsAsync(
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var geometry = zoneGeometryByMap.GetValueOrDefault(mapId);
        foreach (var session in sessions)
        {
            var shooter = session.Value;
            if (shooter.GunShot is not { } order) continue;
            if (shooter.IsDead || monsterManager.Get(order.TargetEntityId) is not { IsDead: false } target || target.MapId != mapId)
            {
                shooter.GunShot = null;
                continue;
            }
            if (shooter.ChaseEndsAt != default)
            {
                if (now < shooter.ChaseEndsAt) continue;
                shooter.PlayerX = shooter.ChaseToX;
                shooter.PlayerY = shooter.ChaseToY;
                shooter.ChaseEndsAt = default;
            }
            var (gun, bullet) = GunGearOf(shooter);
            if (gun is null || bullet is null || bullet.Stack < order.Bullets || !gunParams.TryGetValue(gun.Item.ItemIndex, out var gunParam))
            {
                shooter.GunShot = null;
                continue;
            }
            var (targetX, targetY) = target.GetInterpolatedPosition(now);
            if (!combatInfoByType.TryGetValue(shooter.PlayerCharacterType, out var shooterReach))
            {
                shooter.GunShot = null;
                continue;
            }
            var gunReach = shooterReach with
            {
                MinAttackRange = gunParam.MinAttackRange,
                MaxAttackRange = gunParam.MaxAttackRange,
                ApproachOverride = (gunParam.MinAttackRange + gunParam.MaxAttackRange) * GunIdealRangeNumerator >> GunIdealRangeShift,
            };
            var approach = tPoseidon.Core.Game.Combat.MeleeApproach.Decide(
                gunReach, shooter.PlayerX, shooter.PlayerY, targetX, targetY, afterFirstSwing: false, npcLoopRng);
            if (approach.Kind != tPoseidon.Core.Game.Combat.MeleeApproachKind.InRange)
            {
                var moveType = tPoseidon.Core.Game.Combat.MeleeApproach.EffectiveMoveType(order.MoveType);
                var speed = playerMoveSpeeds is { } shooterSpeeds ? SpeedOfMoveType(shooterSpeeds, moveType, shooter.GameMasterSpeed) : 0f;
                var route = speed > 0 && geometry is not null
                    ? tPoseidon.Core.Game.Monsters.WanderRoute.FindToPoint(geometry, shooter.PlayerX, shooter.PlayerY, approach.X, approach.Y)
                    : null;
                if (route is null)
                {
                    shooter.GunShot = null;
                    continue;
                }
                var fromX = shooter.PlayerX;
                var fromY = shooter.PlayerY;
                shooter.PlayerMoveType = moveType;
                shooter.ChaseToX = (ushort)route.DestinationX;
                shooter.ChaseToY = (ushort)route.DestinationY;
                shooter.ChaseEndsAt = now.AddMilliseconds(Math.Ceiling(route.Length / speed * 1000.0));
                if (!await SendFromLoopAsync(
                    sessions, Opcode.ENTITY_MOVE,
                    new EntityMovementPacket
                    {
                        EntityId = shooter.PlayerEntityId, FromX = fromX, FromY = fromY, ToX = shooter.ChaseToX, ToY = shooter.ChaseToY, Flag = order.MoveType,
                    }.EncodePayload(),
                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                {
                    return false;
                }
                continue;
            }
            shooter.GunShot = null;
            if (!await NormalGunShotAsync(session, sessions, order, target, gun, bullet, mapId, geometry, now, battleEvents).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> NormalGunShotAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        GunOrder order,
        MonsterEntity target,
        tPoseidon.Core.Game.Items.WornItem gun,
        Item bullet,
        ushort mapId,
        ZoneGeometry? geometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var shooter = session.Value;
        if (monsterCatalog.GetById(target.MonsterParamId) is not { } definition) return true;
        var power = GunPowerOf(gun);
        if (shooter.Effects.Snapshot().FirstOrDefault(held => held.IsApplied && held.Kind.ActionId == tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.CustomizingGun) is { } customized)
        {
            power = tPoseidon.Core.Game.Combat.GunAttack.CustomizedPower(power, customized.Amount);
        }
        (long Damage, int Kind) shot;
        lock (npcLoopRng)
        {
            shot = tPoseidon.Core.Game.Combat.GunAttack.Shot(
                shooter.PlayerAc, shooter.PlayerLk, power, SharpSenseOf(shooter), order.Charged,
                MonsterTarget(target, definition, SkillActions.GunElement), targetIsPlayer: false, npcLoopRng.Next);
        }
        var hpBefore = target.CurrentHp;
        target.AddAttacker(shooter.PlayerEntityId);
        target.ApplyDamage(shot.Damage);
        var (targetX, targetY) = target.GetInterpolatedPosition(now);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: shooter.PlayerEntityId, TargetEntityId: target.EntityId,
            AttackerX: shooter.PlayerX, AttackerY: shooter.PlayerY, TargetX: targetX, TargetY: targetY,
            Damage: shot.Damage, MobHpAfter: target.CurrentHp, HitFlags: 0, HitKind: (uint)shot.Kind, Killed: target.IsDead,
            NextSwingAtUtc: now, MonsterParamId: target.MonsterParamId, MobMaxHp: target.MaxHp, MobHpBefore: hpBefore);
        if (strike.Killed && shooter.CurrentTargetEntityId == target.EntityId) shooter.CurrentTargetEntityId = 0;
        SpendBullets(bullet, order.Bullets);
        var (additions, ownerEntityId) = await ResolveStrikeAsync(
            strike, shooter, mapId, sessions, geometry, now, battleEvents,
            CombatExperience.NormalAttackDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
        Console.WriteLine($"{LogTag} [gun] 0x{shooter.PlayerEntityId:X4} shoots 0x{target.EntityId:X4}{(order.Charged ? " charged" : string.Empty)}: {shot.Damage} kind {shot.Kind}, hp {target.CurrentHp}/{target.MaxHp}, {order.Bullets} bullet(s)");
        return await SendStrikeFramesAsync(
            MeleeSwingFrames.EncodeGunShot(strike, commandBlockIds.Next(), additions),
            strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false);
    }

    async Task<bool> AreaAroundOwnerTickAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        tPoseidon.Core.Game.Skills.Effects.SkillEffect effect,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var owner = session.Value;
        if (effect.Kind.Periodic != tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.AreaAroundOwner
            || effect.Action is not { } wheelAction || owner.IsDead || owner.Stats is not { } wheelStats)
        {
            return true;
        }
        AreaCandidate CandidateOf(MonsterEntity monster)
        {
            var (x, y) = monster.GetInterpolatedPosition(now);
            return new AreaCandidate(monster.EntityId, x, y, monster.IsDead, monsterCatalog.GetById(monster.MonsterParamId)?.Type ?? 0, monster.OwnerEntityId);
        }
        var victims = SkillAreaShapes.Collect(
            SkillAreaShapes.HammerWheelTick, wheelAction, owner.PlayerEntityId, (owner.PlayerX, owner.PlayerY), null,
            monsterManager.GetByMap(mapId).Select(CandidateOf));
        var attacker = new SkillAttacker(
            owner.PlayerAp, owner.PlayerAc, owner.PlayerMa, owner.PlayerLk, wheelStats.Level,
            owner.PlayerDa, wheelStats.CurrentHp, owner.PlayerMaxHp,
            owner.PlayerHv, owner.PlayerDx, owner.PlayerDp, DarkBonus: DarkBonusOf(owner));
        foreach (var victimId in victims)
        {
            if (monsterManager.Get(victimId) is not { IsDead: false } victim || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition) continue;
            SkillHit hit;
            lock (npcLoopRng)
            {
                hit = SkillAreaShapes.Hit(SkillAreaShapes.HammerWheelTick, wheelAction, attacker, MonsterTarget(victim, victimDefinition, SkillActions.NoElement), npcLoopRng);
            }
            if (!await PeriodicStrikeAsync(owner, victim, hit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> DamageOverTimeTickAsync(
        tPoseidon.Core.Game.Skills.Effects.SkillEffect effect,
        MonsterEntity monster,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        if (effect.Kind.Periodic != tPoseidon.Core.Game.Skills.Effects.SkillPeriodicHit.DamageOverTime
            || effect.Action is not { } dotAction
            || monsterCatalog.GetById(monster.MonsterParamId) is not { } dotDefinition
            || sessions.FirstOrDefault(entry => entry.Value.PlayerEntityId == effect.CasterEntityId).Value is not { } dotCaster)
        {
            return true;
        }
        var damage = PolarBearPeriodic.MoleHammerDamage(
            dotAction, dotCaster.PlayerAp, dotDefinition.DpLv + monster.StatDelta(MonsterStat.Dp), SkillActions.ResistOf(dotDefinition, SkillActions.NoElement) - monster.ResistLoss(SkillActions.NoElement));
        var hit = new SkillHit(damage, SkillDamage.PhysicalType, SkillDamage.KindHit, SkillActions.NoElement, 0);
        return await PeriodicStrikeAsync(dotCaster, monster, hit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
    }

    SkillTarget MonsterTarget(MonsterEntity monster, MonsterParamEntry definition, int element) => new(
        definition.DpLv + monster.StatDelta(MonsterStat.Dp),
        definition.HvLv + monster.StatDelta(MonsterStat.Hv),
        definition.MdLv + monster.StatDelta(MonsterStat.Md),
        definition.LkLv + monster.StatDelta(MonsterStat.Lk),
        monster.CurrentHp, monster.Level, SkillActions.ResistOf(definition, element) - monster.ResistLoss(element),
        monster.MaxHp, definition.DpLv);

    async Task<bool> PeriodicStrikeAsync(
        InGameSessionState striker,
        MonsterEntity victim,
        SkillHit hit,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        var hpBefore = victim.CurrentHp;
        victim.AddAttacker(striker.PlayerEntityId);
        victim.ApplyDamage(hit.Damage);
        var (victimX, victimY) = victim.GetInterpolatedPosition(now);
        var strike = new tPoseidon.Core.Game.Combat.PlayerStrike(
            AttackerEntityId: striker.PlayerEntityId,
            TargetEntityId: victim.EntityId,
            AttackerX: striker.PlayerX,
            AttackerY: striker.PlayerY,
            TargetX: victimX,
            TargetY: victimY,
            Damage: hit.Damage,
            MobHpAfter: victim.CurrentHp,
            HitFlags: 0,
            HitKind: (uint)hit.Kind,
            Killed: victim.IsDead,
            NextSwingAtUtc: now,
            MonsterParamId: victim.MonsterParamId,
            MobMaxHp: victim.MaxHp,
            MobHpBefore: hpBefore);
        if (strike.Killed && striker.CurrentTargetEntityId == victim.EntityId) striker.CurrentTargetEntityId = 0;
        var (additions, ownerEntityId) = await ResolveStrikeAsync(
            strike, striker, mapId, sessions, zoneGeometry, now, battleEvents,
            SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
        Console.WriteLine($"{LogTag} [skill] 0x{striker.PlayerEntityId:X4} tick -> 0x{victim.EntityId:X4} damage {hit.Damage} kind {hit.Kind}, hp {victim.CurrentHp}/{victim.MaxHp}");
        return await SendStrikeFramesAsync(
            SkillFrames.EncodeStandaloneHit(strike, hit, additions),
            strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false);
    }

    tPoseidon.Core.LibConfig.SkillTableRow? LearnedAction(InGameSessionState learner, int skillId) =>
        learner.Skills.TryGetValue(skillId, out var learnedLevel) && skillBook.Find(skillId)?.ActionAt(learnedLevel) is { } learnedRow
            ? learnedRow
            : null;

    bool InCrazyMode(InGameSessionState player) =>
        player.Effects.Snapshot().Any(held => held.IsApplied && held.Kind.ActionId == CrazyHammer.ModeAction);

    async Task<bool> FillCrazyGaugeAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        string pointsColumn,
        ushort mapId,
        DateTimeOffset now)
    {
        var player = session.Value;
        if (player.IsDead || InCrazyMode(player)
            || LearnedAction(player, CrazyHammer.CrazyHammerSkill) is not { } crazy
            || skillBook.Find(CrazyHammer.CrazyHammerSkill) is not { } crazySkill)
        {
            return true;
        }
        var max = crazy.Amount(CrazyHammer.GaugeColumn) ?? 0;
        var points = crazy.Amount(pointsColumn) ?? 0;
        if (max <= 0 || points <= 0) return true;
        var ownerOnly = new List<KeyValuePair<S2SessionContext, InGameSessionState>> { session };
        if (player.CrazyGauge.NeedsAnnounce(mapId))
        {
            var gaugeObjectId = idAllocator.NewId();
            var previousObjectId = player.CrazyGauge.Announce(gaugeObjectId, mapId);
            if (previousObjectId != 0) idAllocator.Release(previousObjectId);
            var gaugeBlock = commandBlockIds.Next();
            if (!await SendFromLoopAsync(
                    ownerOnly, Opcode.S2C_OP_00E0_TBD,
                    CrazyHammerFrames.Gauge(player.PlayerEntityId, gaugeObjectId, (sbyte)crazySkill.Row.EsClass, player.CrazyGauge.Current, max),
                    gaugeBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)
                || !await SendFromLoopAsync(
                    ownerOnly, Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), gaugeBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return false;
            }
        }
        var fill = player.CrazyGauge.Fill(points, max);
        if (!fill.Changed) return true;
        if (!await SendFromLoopAsync(
                ownerOnly, Opcode.S2C_SKILL_GAUGE, CrazyHammerFrames.GaugeUpdate(player.CrazyGauge.ObjectId, fill.Current),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
        {
            return false;
        }
        if (!fill.Burst || tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(CrazyHammer.ModeAction) is not { } modeKind) return true;
        Console.WriteLine($"{LogTag} [skill] 0x{player.PlayerEntityId:X4} crazy hammer gauge full ({max}), mode starts");
        return await StartSkillEffectAsync(session, sessions, crazySkill, modeKind, crazy, null, now).ConfigureAwait(false);
    }

    async Task<bool> CrazyShockwaveAsync(
        InGameSessionState striker,
        tPoseidon.Core.Game.Combat.PlayerStrike strike,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort mapId,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents)
    {
        if (strike.Damage <= 0 || !InCrazyMode(striker)
            || LearnedAction(striker, CrazyHammer.QuintupleCrazySkill) is not { } quintuple)
        {
            return true;
        }
        AreaCandidate CandidateOf(MonsterEntity monster)
        {
            var (x, y) = monster.GetInterpolatedPosition(now);
            return new AreaCandidate(monster.EntityId, x, y, monster.IsDead, monsterCatalog.GetById(monster.MonsterParamId)?.Type ?? 0, monster.OwnerEntityId);
        }
        var struck = monsterManager.Get(strike.TargetEntityId) is { } struckMonster
            ? CandidateOf(struckMonster)
            : new AreaCandidate(strike.TargetEntityId, strike.TargetX, strike.TargetY, true, 0, MonsterEntity.NoOwner);
        var victims = SkillAreaShapes.Collect(
            CrazyHammer.Shockwave, quintuple, striker.PlayerEntityId, (striker.PlayerX, striker.PlayerY), struck,
            monsterManager.GetByMap(mapId).Select(CandidateOf));
        foreach (var victimId in victims.Skip(1))
        {
            if (monsterManager.Get(victimId) is not { IsDead: false } victim || monsterCatalog.GetById(victim.MonsterParamId) is not { } victimDefinition) continue;
            var damage = CrazyHammer.ShockwaveDamage(striker.PlayerAp, victimDefinition.DpLv + victim.StatDelta(MonsterStat.Dp));
            var hit = new SkillHit(damage, SkillDamage.PhysicalType, SkillDamage.KindHit, SkillActions.NoElement, 0);
            if (!await PeriodicStrikeAsync(striker, victim, hit, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return false;
        }
        return true;
    }

    async Task<bool> StartAttackAfterSkillAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ushort targetEntityId,
        int extraDelayMs,
        DateTimeOffset now)
    {
        var fighter = session.Value;
        if (fighter.IsDead || monsterManager.Get(targetEntityId) is not { IsDead: false }) return true;

        var alreadyAttacking = fighter.CurrentTargetEntityId != 0 && fighter.NextSwingAt != default;
        var interval = combatInfoByType.TryGetValue(fighter.PlayerCharacterType, out var fighterInfo)
            ? fighterInfo.PlayerSwingDelayMs(fighter.PlayerDx)
            : 0;
        fighter.CurrentTargetEntityId = targetEntityId;
        fighter.AttackMoveType = tPoseidon.Core.Game.Combat.MeleeApproach.RunMoveType;
        fighter.PlayerMoveType = tPoseidon.Core.Game.Combat.MeleeApproach.RunMoveType;
        fighter.NextSwingAt = now.AddMilliseconds(interval + extraDelayMs);
        fighter.SwingsOnTarget = 0;
        fighter.FollowSettled = false;
        fighter.SettlesOnArrival = false;
        fighter.ChaseEndsAt = default;
        fighter.LastChaseOrderAt = default;
        var (stoppedX, stoppedY) = PlayerPositionAt(fighter, now);
        fighter.PlayerX = (ushort)stoppedX;
        fighter.PlayerY = (ushort)stoppedY;
        fighter.Walk = null;
        if (!alreadyAttacking) fighter.AttackStopPending = true;
        if (fighter.Engagements.Refresh(targetEntityId, now) is { } crowdedOut)
        {
            await ReleaseMonsterOwnerAsync(fighter.MapId, crowdedOut, fighter.PlayerEntityId).ConfigureAwait(false);
        }
        if (playerMoveSpeeds is { } fighterSpeeds)
        {
            var runSpeed = SpeedOfMoveType(fighterSpeeds, tPoseidon.Core.Game.Combat.MeleeApproach.RunMoveType, fighter.GameMasterSpeed);
            var runSpeedQuarter = ProgressFrames.SpeedQuarter(runSpeed);
            if (runSpeedQuarter != fighter.AnnouncedSpeedQuarter)
            {
                fighter.AnnouncedSpeedQuarter = runSpeedQuarter;
                if (!await SendFromLoopAsync(
                    sessions, Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(fighter.PlayerEntityId, runSpeed),
                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }
        Console.WriteLine($"{LogTag} [skill] 0x{fighter.PlayerEntityId:X4} goes on attacking 0x{targetEntityId:X4}: first swing in {interval + extraDelayMs} ms");
        if (alreadyAttacking) return true;
        return await SendFromLoopAsync(
            new[] { session }, Opcode.S2C_OP_001E_TBD, Array.Empty<byte>(),
            MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false);
    }

    async Task<bool> EndCastAsync(
        KeyValuePair<S2SessionContext, InGameSessionState> session,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        SkillCast cast,
        Opcode opcode,
        byte[] payload,
        DateTimeOffset now)
    {
        session.Value.Cast = null;
        if (!await SendFromLoopAsync(
            new[] { session }, opcode, payload, MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
        {
            return false;
        }
        return cast.ResumeAttackOn == 0
            || await StartAttackAfterSkillAsync(
                session, sessions, cast.ResumeAttackOn, SkillActions.AttackResumeDelayMs, now).ConfigureAwait(false);
    }

    async Task<(SwingAdditions Additions, ushort OwnerEntityId)> ResolveStrikeAsync(
        tPoseidon.Core.Game.Combat.PlayerStrike strike,
        InGameSessionState? striker,
        ushort mapId,
        List<KeyValuePair<S2SessionContext, InGameSessionState>> sessions,
        ZoneGeometry? zoneGeometry,
        DateTimeOffset now,
        List<tPoseidon.Core.Game.Combat.MonsterBattleEvent> battleEvents,
        int experienceDivisor,
        bool takesOwnerWithoutDamage)
    {
        var struckMonster = monsterManager.Get(strike.TargetEntityId);
        var struckDefinition = monsterCatalog.GetById(strike.MonsterParamId);
        byte[]? ownerTaken = null;
        if (striker is not null && struckMonster is not null)
        {
            if (striker.Engagements.Refresh(strike.TargetEntityId, now) is { } crowdedOut)
            {
                await ReleaseMonsterOwnerAsync(mapId, crowdedOut, striker.PlayerEntityId).ConfigureAwait(false);
            }
            if ((strike.Damage > 0 || takesOwnerWithoutDamage) && struckMonster.OwnerEntityId == MonsterEntity.NoOwner)
            {
                struckMonster.TakeOwner(strike.AttackerEntityId, strike.MobHpBefore);
                ownerTaken = ProgressFrames.OwnerTaken(strike.TargetEntityId, strike.AttackerEntityId);
                Console.WriteLine($"{LogTag} [combat] monster 0x{strike.TargetEntityId:X4} is owned by 0x{strike.AttackerEntityId:X4} from HP {strike.MobHpBefore}");
            }
        }
        var ownerEntityId = struckMonster?.OwnerEntityId ?? MonsterEntity.NoOwner;

        IReadOnlyList<(Opcode Opcode, byte[] Payload)> ownerRewards = Array.Empty<(Opcode Opcode, byte[] Payload)>();
        if (strike.Killed && struckMonster is not null && struckDefinition is not null && ownerEntityId != MonsterEntity.NoOwner)
        {
            foreach (var session in sessions)
            {
                var owner = session.Value;
                if (owner.PlayerEntityId != ownerEntityId || owner.Stats is not { } ownerStats || owner.GainKillTmExp is not { } gainTm) continue;
                var huntFrames = owner.Quests?.CountKill(strike.MonsterParamId) ?? Array.Empty<(Opcode Opcode, byte[] Payload)>();
                if (owner.RunScriptEvent is { } killScripts) _ = killScripts(tPoseidon.Core.Game.Scripting.Lang.EventKind.MonsterKill, strike.MonsterParamId, null);
                if (owner.Character is { } ownerCharacter)
                {
                    await partyQuests.KilledAsync(ownerCharacter.CharacterId, strike.MonsterParamId, CancellationToken.None).ConfigureAwait(false);
                }
                var tm = CombatExperience.TmForKill(
                    struckDefinition.Tm, WireLimits.Int32Field(struckMonster.HpWhenOwned), WireLimits.Int32Field(struckMonster.MaxHp), struckMonster.Level, ownerStats.Level);
                var tmSplit = owner.Character is { } tmEarner ? partyShares.SplitTm(tmEarner.CharacterId, tm) : null;
                if (tmSplit is null)
                {
                    ownerRewards = huntFrames.Concat(gainTm(tm)).ToList();
                }
                else
                {
                    var ownTm = new List<(Opcode Opcode, byte[] Payload)>(huntFrames);
                    foreach (var (memberId, memberTm) in tmSplit)
                    {
                        var member = sessions.FirstOrDefault(s => s.Value.Character?.CharacterId == memberId);
                        if (member.Value?.GainKillTmExp is not { } memberGainTm) continue;
                        if (ReferenceEquals(member.Value, owner))
                        {
                            ownTm.AddRange(memberGainTm(memberTm));
                            continue;
                        }
                        await SendPartyShareFramesAsync(member, memberGainTm(memberTm).Select(f => (f.Opcode, f.Payload, SwingAudience.Attacker)).ToList()).ConfigureAwait(false);
                    }
                    ownerRewards = ownTm;
                    Console.WriteLine($"{LogTag} [party] TM {tm} shared: {string.Join(", ", tmSplit.Select(g => $"0x{g.CharacterId:X}={g.Gain}"))}");
                }
                Console.WriteLine($"{LogTag} [progress] 0x{ownerEntityId:X4} gets {tm} TM exp for monster 0x{strike.TargetEntityId:X4} (owned from HP {struckMonster.HpWhenOwned} of {struckMonster.MaxHp})");
            }
        }

        IReadOnlyList<(Opcode Opcode, byte[] Payload, SwingAudience Audience)> attackerRewards =
            Array.Empty<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>();
        if (striker is { Stats: { } strikerStats, GainHitExp: { } gainExp } && struckMonster is not null && struckDefinition is not null)
        {
            var share = CombatExperience.Share(struckDefinition.Exp, WireLimits.Int32Field(strike.Damage), WireLimits.Int32Field(strike.MobHpBefore), WireLimits.Int32Field(struckMonster.MaxHp));
            var hitExp = CombatExperience.ForHit(share, strikerStats.Level, struckMonster.Level, experienceDivisor);
            var expSplit = striker.Character is { } expEarner ? partyShares.SplitExp(expEarner.CharacterId, hitExp) : null;
            if (expSplit is null)
            {
                attackerRewards = gainExp(hitExp);
            }
            else
            {
                var ownExp = new List<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>();
                foreach (var (memberId, memberExp) in expSplit)
                {
                    var member = sessions.FirstOrDefault(s => s.Value.Character?.CharacterId == memberId);
                    if (member.Value?.GainExpTotal is not { } memberGainExp) continue;
                    if (ReferenceEquals(member.Value, striker))
                    {
                        ownExp.AddRange(memberGainExp(memberExp));
                        continue;
                    }
                    await SendPartyShareFramesAsync(member, memberGainExp(memberExp)).ConfigureAwait(false);
                }
                attackerRewards = ownExp;
                Console.WriteLine($"{LogTag} [party] exp {hitExp} shared: {string.Join(", ", expSplit.Select(g => $"0x{g.CharacterId:X}={g.Gain}"))}");
            }
            Console.WriteLine($"{LogTag} [progress] 0x{strike.AttackerEntityId:X4} gets {hitExp} exp for this hit (share {share}, divisor {experienceDivisor})");
        }

        if (strike.Killed && striker is { Stats: { } killerStats } && struckMonster is not null
            && SoulSeedRules.CountsKill(killerStats.Level, struckMonster.Level))
        {
            attackerRewards = attackerRewards
                .Concat(soulSeeds.Event(striker, SoulSeedRules.MonsterKillEvent, 1).Select(seedFrame => (seedFrame.Opcode, seedFrame.Payload, SwingAudience.Attacker)))
                .ToList();
        }

        if (strike.Killed && striker is { Character: { } bossKiller } && struckDefinition is not null
            && tPoseidon.Core.Game.Items.UniqueItemKinds.KilledAsBoss(struckDefinition.Type))
        {
            var bossMonsterId = (ushort)struckDefinition.Id;
            attackerRewards = attackerRewards
                .Append((Opcode.S2C_BOSS_KILLED, UniqueItemFrames.BossKilled(bossMonsterId), SwingAudience.Attacker))
                .ToList();
            await masterUniqueItems.BossKilledAsync(bossKiller.CharacterId, bossMonsterId, npcLoopCt).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} [unique] 0x{strike.AttackerEntityId:X4} killed boss {bossMonsterId}; boss kill sent to the Master");
        }

        List<(Opcode Opcode, byte[] Payload)>? dropFrames = null;
        if (strike.Killed
            && struckDefinition is not null
            && ownerEntityId != MonsterEntity.NoOwner
            && sessions.Any(s => s.Value.PlayerEntityId == ownerEntityId))
        {
            var dropSource = new DropSource(
                strike.TargetEntityId, strike.MonsterParamId, mapId, strike.TargetX, strike.TargetY, ownerEntityId, now);
            var rolledDrops = monsterDropRoller.Roll(
                struckDefinition.ItemDropIdx,
                struckDefinition.ItemDropCnt,
                itemId => itemCatalog.GetById(itemId)?.ObjectClass == ItemParamEntry.RareClass,
                npcLoopRng);
            if (uniqueItemPool.RollLegendKey(struckDefinition.Id, npcLoopRng.Next) is { } legendKey)
            {
                var counted = uniqueItemPool.AddCount((uint)legendKey, tPoseidon.Core.Game.Items.UniqueItemPool.LegendAmount);
                var legendKiller = sessions.FirstOrDefault(s => s.Value.PlayerEntityId == ownerEntityId).Value?.Character?.CharacterId ?? 0;
                if (counted.Units > 0) await masterUniqueItems.AddCountAsync(legendKiller, (uint)legendKey, counted.Units, npcLoopCt).ConfigureAwait(false);
                if (counted.IssuedItemId is { } uniqueDrop)
                {
                    rolledDrops = rolledDrops.Append(new RolledDrop(uniqueDrop, 1)).ToList();
                    Console.WriteLine($"{LogTag} [unique] monster {struckDefinition.Id} drops issued unique item {uniqueDrop}");
                }
            }
            var droppedMoney = MonsterDropRoller.MoneyAmount(
                struckDefinition.GelderRate, struckDefinition.GMin, struckDefinition.GMax, npcLoopRng) is { } moneyAmount
                ? MonsterDropBatch.Money(dropSource, moneyAmount, itemCatalog, idAllocator.NewId, zoneGeometry, npcLoopRng)
                : Array.Empty<FieldItem>();
            var droppedItems = MonsterDropBatch.Items(
                dropSource,
                rolledDrops.Select(drop => drop with { Count = globalItemManager.NewStack(drop.ItemId, (ushort)Math.Clamp(drop.Count, 0, ushort.MaxValue)) }).ToList(),
                itemCatalog, idAllocator.NewId, zoneGeometry, npcLoopRng);

            dropFrames = new List<(Opcode Opcode, byte[] Payload)>(droppedMoney.Count + droppedItems.Count);
            foreach (var money in droppedMoney)
            {
                groundItems.Add(money);
                dropFrames.Add((Opcode.S2C_MONEY_DROPPED, GroundItemFrames.Dropped(money)));
                Console.WriteLine($"{LogTag} [items] monster 0x{strike.TargetEntityId:X4} drops {money.Count} money as 0x{money.EntityId:X4} at ({money.X},{money.Y}) for 0x{ownerEntityId:X4}");
            }
            foreach (var item in droppedItems)
            {
                groundItems.Add(item);
                dropFrames.Add((Opcode.S2C_OP_000C_TBD, GroundItemFrames.Dropped(item)));
                Console.WriteLine($"{LogTag} [items] monster 0x{strike.TargetEntityId:X4} drops item {item.ItemId} x{item.Count} as 0x{item.EntityId:X4} at ({item.X},{item.Y}) for 0x{ownerEntityId:X4}");
            }
        }

        if (strike.Killed)
        {
            corpses.Add((strike.TargetEntityId, mapId, now + corpseRemovalDelay));
            monsterBattleCycle.Forget(strike.TargetEntityId);
            if (struckMonster is not null)
            {
                monsterRegenCycle.Release(struckMonster);
                monsterManager.ReportKilled(struckMonster, strike.AttackerEntityId);
            }
        }
        else if (struckMonster is not null)
        {
            if (monsterBattleCycle.Engage(struckMonster, strike.AttackerEntityId, now) is { } halted)
            {
                battleEvents.Add(halted);
            }
            else if (ownerEntityId == strike.AttackerEntityId
                && monsterBattleCycle.Retarget(struckMonster, strike.AttackerEntityId))
            {
                Console.WriteLine($"{LogTag} [combat] monster 0x{strike.TargetEntityId:X4} turns to its owner 0x{strike.AttackerEntityId:X4}");
            }
        }
        return (new SwingAdditions(ownerTaken, ownerRewards, attackerRewards, dropFrames), ownerEntityId);
    }
    while (!npcLoopCt.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(interval, npcLoopCt).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var sessionsByMap = new Dictionary<ushort, List<KeyValuePair<S2SessionContext, InGameSessionState>>>();
        foreach (var kv in liveSessions.ToArray())
        {
            if (!kv.Value.ClientReady) continue;
            if (!sessionsByMap.TryGetValue(kv.Value.MapId, out var list))
            {
                list = new List<KeyValuePair<S2SessionContext, InGameSessionState>>();
                sessionsByMap[kv.Value.MapId] = list;
            }
            list.Add(kv);
        }

        foreach (var mapSessions in sessionsByMap.Values)
        {
            foreach (var queued in mapSessions)
            {
                while (queued.Value.PendingNpcSkills.TryDequeue(out var queuedSkill))
                {
                    if (!await StartNpcSkillAsync(queued, mapSessions, queuedSkill, now).ConfigureAwait(false)) break;
                }
            }
        }

        for (var i = corpses.Count - 1; i >= 0; i--)
        {
            var corpse = corpses[i];
            if (now < corpse.RemoveAt) continue;
            corpses.RemoveAt(i);

            var dead = monsterManager.Get(corpse.EntityId);
            if (dead is null) continue;
            monsterManager.Remove(corpse.EntityId);
            monsterWanderCycle.Forget(corpse.EntityId);
            DropMonsterEffects(corpse.EntityId);
            if (!sessionsByMap.TryGetValue(corpse.MapId, out var watchers)) continue;

            var removal = new tPoseidon.Core.Net.PacketWriter(MeleeSwingFrames.DeathSize);
            removal.WriteUInt16(corpse.EntityId);
            removal.WriteByte(0);
            var removalPayload = removal.ToArray();
            foreach (var watcher in watchers)
            {
                try
                {
                    await watcher.Key.SendFrameAsync(Opcode.S2C_GROUND_ITEM_DESPAWN, removalPayload, (byte)npcLoopRng.Next(1, 256), npcLoopCt).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    liveSessions.TryRemove(watcher.Key, out _);
                }
                catch (System.IO.IOException)
                {
                    liveSessions.TryRemove(watcher.Key, out _);
                }
            }
        }

        foreach (var expired in groundItems.TakeExpired(now))
        {
            idAllocator.Release(expired.EntityId);
            if (expired.ItemUid != 0) itemStore.Delete(expired.ItemUid);
            Console.WriteLine($"{LogTag} [items] item 0x{expired.EntityId:X4} (item {expired.ItemId} x{expired.Count}) on map {expired.MapId} timed out");
            if (!sessionsByMap.TryGetValue(expired.MapId, out var itemWatchers)) continue;
            if (!await SendFromLoopAsync(
                itemWatchers, Opcode.S2C_GROUND_ITEM_DESPAWN, GroundItemFrames.Removed(expired.EntityId),
                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
            {
                return;
            }
        }

        var bornThisTick = 0;
        foreach (var regenMapId in monsterRegenCycle.MapIds)
        {
            sessionsByMap.TryGetValue(regenMapId, out var regenWatchers);
            for (var regenPass = 0; regenPass < regenPassesPerTick; regenPass++)
            {
                var born = monsterRegenCycle.Pass(regenMapId);
                if (born.Count == 0) break;
                bornThisTick += born.Count;
                if (regenWatchers is null) continue;
                foreach (var newborn in born)
                {
                    var newbornPayload = MobSpawnFrames.Encode(newborn, monsterCatalog.GetById(newborn.MonsterParamId));
                    foreach (var watcher in regenWatchers)
                    {
                        try
                        {
                            await watcher.Key.SendFrameAsync(Opcode.NPC_SPAWN, newbornPayload, (byte)npcLoopRng.Next(1, 256), npcLoopCt).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (ObjectDisposedException)
                        {
                            liveSessions.TryRemove(watcher.Key, out _);
                        }
                        catch (System.IO.IOException)
                        {
                            liveSessions.TryRemove(watcher.Key, out _);
                        }
                    }
                }
            }
        }

        if (!regenFillReported && bornThisTick == 0)
        {
            regenFillReported = true;
            Console.WriteLine($"{LogTag} monster regen filled the maps: {monsterManager.Count} monsters");
        }

        foreach (var (mapId, sessions) in sessionsByMap)
        {
            var mapFrames = new List<IReadOnlyList<(Opcode Opcode, byte[] Payload)>>();
            foreach (var idleEvent in npcIdleCycle.Tick(worldEntities.InMap(mapId).Where(npc => npcActivation.IsActive(npc.EntityId)).ToArray(), now))
            {
                mapFrames.Add(NpcIdleFrames.Encode(idleEvent, clientTextEncoding));
            }
            if (zoneGeometryByMap.TryGetValue(mapId, out var zoneGeometry))
            {
                var playerPositions = sessions
                    .Select(s => ((int)s.Value.PlayerX, (int)s.Value.PlayerY))
                    .ToArray();
                var wanderers = monsterManager.GetByMap(mapId)
                    .Where(m => !monsterBattleCycle.IsFighting(m.EntityId) && !m.IsHeld(now))
                    .ToArray();
                foreach (var wanderEvent in monsterWanderCycle.Tick(zoneGeometry, wanderers, playerPositions, now))
                {
                    mapFrames.Add(new[] { MonsterWanderFrames.Encode(wanderEvent) });
                }
            }

            foreach (var session in sessions)
            {
                foreach (var forgotten in session.Value.Engagements.TakeExpired(now))
                {
                    await ReleaseMonsterOwnerAsync(mapId, forgotten, session.Value.PlayerEntityId).ConfigureAwait(false);
                }
            }

            var attackers = new List<tPoseidon.Core.Game.Combat.PlayerMeleeAttacker>();
            var pvpSwings = new List<(InGameSessionState Attacker, InGameSessionState Victim)>();
            foreach (var session in sessions)
            {
                var attackerState = session.Value;
                if (attackerState.CurrentTargetEntityId == 0 || attackerState.NextSwingAt == default) continue;
                var chaseTarget = monsterManager.Get(attackerState.CurrentTargetEntityId);
                var pvpTarget = chaseTarget is null ? PvpTargetOf(attackerState, sessions.Select(entry => entry.Value)) : null;
                if ((chaseTarget is null || chaseTarget.IsDead) && pvpTarget is null) continue;
                if (attackerState.AttackStopPending)
                {
                    attackerState.AttackStopPending = false;
                    mapFrames.Add(new[]
                    {
                        (Opcode.S2C_OP_0019_TBD, GroundItemFrames.Stopped(
                            attackerState.PlayerEntityId, attackerState.PlayerX, attackerState.PlayerY,
                            tPoseidon.Core.Game.Combat.MeleeApproach.WalkMoveType)),
                    });
                }
                if (attackerState.ChaseEndsAt != default)
                {
                    if (now < attackerState.ChaseEndsAt) continue;
                    attackerState.PlayerX = attackerState.ChaseToX;
                    attackerState.PlayerY = attackerState.ChaseToY;
                    attackerState.ChaseEndsAt = default;
                    if (attackerState.SettlesOnArrival)
                    {
                        attackerState.SettlesOnArrival = false;
                        attackerState.FollowSettled = true;
                    }
                }
                if (playerMoveSpeeds is { } moveSpeeds
                    && combatInfoByType.TryGetValue(attackerState.PlayerCharacterType, out var baseReach))
                {
                    var reach = baseReach.WithRangeBonus(RangeBonusOf(attackerState));
                    var (targetX, targetY) = chaseTarget is not null ? chaseTarget.GetInterpolatedPosition(now) : PlayerPositionAt(pvpTarget!, now);
                    var (attackerX, attackerY) = PlayerPositionAt(attackerState, now);
                    var approach = tPoseidon.Core.Game.Combat.MeleeApproach.Decide(
                        reach, attackerX, attackerY, targetX, targetY,
                        attackerState.FollowSettled, npcLoopRng);
                    var afterFirstSwing = attackerState.FollowSettled;
                    var backingOff = approach.Kind == tPoseidon.Core.Game.Combat.MeleeApproachKind.BackOff;
                    var leaveAttack = approach.Kind == tPoseidon.Core.Game.Combat.MeleeApproachKind.GiveUp;
                    if (approach.Kind == tPoseidon.Core.Game.Combat.MeleeApproachKind.Move || backingOff)
                    {
                        var orderGap = backingOff
                            ? tPoseidon.Core.Game.Combat.MeleeApproach.BackOffGap
                            : tPoseidon.Core.Game.Combat.MeleeApproach.OrderGap;
                        var orderType = !backingOff && afterFirstSwing
                            ? tPoseidon.Core.Game.Combat.MeleeApproach.ChaseMoveType
                            : attackerState.AttackMoveType;
                        var effectiveOrderType = tPoseidon.Core.Game.Combat.MeleeApproach.EffectiveMoveType(orderType);
                        var chaseSpeed = SpeedOfMoveType(moveSpeeds, effectiveOrderType, attackerState.GameMasterSpeed);
                        var route = chaseSpeed > 0
                            && now - attackerState.LastChaseOrderAt >= orderGap
                            && zoneGeometryByMap.TryGetValue(mapId, out var chaseGeometry)
                            && chaseGeometry is not null
                            ? tPoseidon.Core.Game.Monsters.WanderRoute.FindWaypoints(
                                chaseGeometry, attackerX, attackerY, approach.X, approach.Y)
                            : null;
                        if (route is not null)
                        {
                            var chaseFromX = (ushort)attackerX;
                            var chaseFromY = (ushort)attackerY;
                            var chaseWalk = tPoseidon.Core.Game.Pets.OwnerWalk.Along(attackerX, attackerY, route, chaseSpeed, now);
                            attackerState.LastChaseOrderAt = now;
                            attackerState.PlayerMoveType = effectiveOrderType;
                            attackerState.SettlesOnArrival = backingOff;
                            attackerState.ChaseToX = (ushort)chaseWalk.ToX;
                            attackerState.ChaseToY = (ushort)chaseWalk.ToY;
                            attackerState.PlayerX = attackerState.ChaseToX;
                            attackerState.PlayerY = attackerState.ChaseToY;
                            attackerState.Walk = chaseWalk;
                            attackerState.ChaseEndsAt = chaseWalk.ArrivesAt;
                            var chaseFrames = new List<(Opcode Opcode, byte[] Payload)>(2);
                            var chaseSpeedQuarter = ProgressFrames.SpeedQuarter(chaseSpeed);
                            if (chaseSpeedQuarter != attackerState.AnnouncedSpeedQuarter)
                            {
                                attackerState.AnnouncedSpeedQuarter = chaseSpeedQuarter;
                                chaseFrames.Add((Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(attackerState.PlayerEntityId, chaseSpeed)));
                            }
                            chaseFrames.Add((Opcode.ENTITY_MOVE, new EntityMovementPacket
                            {
                                EntityId = attackerState.PlayerEntityId,
                                FromX = chaseFromX,
                                FromY = chaseFromY,
                                ToX = attackerState.ChaseToX,
                                ToY = attackerState.ChaseToY,
                                Flag = orderType,
                            }.EncodePayload()));
                            mapFrames.Add(chaseFrames);
                            continue;
                        }
                        if (afterFirstSwing || backingOff) continue;
                        leaveAttack = true;
                    }
                    if (leaveAttack)
                    {
                        chaseTarget?.RemoveAttacker(attackerState.PlayerEntityId);
                        attackerState.CurrentTargetEntityId = 0;
                        attackerState.NextSwingAt = default;
                        var leave = new tPoseidon.Core.Net.PacketWriter(MeleeSwingFrames.LeaveAttackSize);
                        leave.WriteUInt16(attackerState.PlayerEntityId);
                        mapFrames.Add(new[] { (Opcode.S2C_OP_0020_TBD, leave.ToArray()) });
                        continue;
                    }
                }
                if (pvpTarget is not null)
                {
                    pvpSwings.Add((attackerState, pvpTarget));
                    continue;
                }
                var crazyRow = InCrazyMode(attackerState) ? LearnedAction(attackerState, CrazyHammer.CrazyHammerSkill) : null;
                attackers.Add(new tPoseidon.Core.Game.Combat.PlayerMeleeAttacker(
                    attackerState.PlayerEntityId,
                    attackerState.CurrentTargetEntityId,
                    attackerState.PlayerX,
                    attackerState.PlayerY,
                    crazyRow is not null
                        ? CrazyHammer.NormalAttackAp(
                            attackerState.PlayerAp, attackerState.CombatBase.Ap, crazyRow, LearnedAction(attackerState, CrazyHammer.TripleCrazySkill))
                        : attackerState.PlayerAp,
                    attackerState.PlayerAc,
                    attackerState.PlayerLk,
                    attackerState.NextSwingAt,
                    crazyRow is not null ? CrazyHammer.CriticalBonus(LearnedAction(attackerState, CrazyHammer.QuadrupleCrazySkill)) : 0));
            }
            var battleEvents = new List<tPoseidon.Core.Game.Combat.MonsterBattleEvent>();
            for (var pendingTactics = harconTactics.Count; pendingTactics > 0 && harconTactics.TryDequeue(out var harconTactic); pendingTactics--)
            {
                if (monsterManager.Get(harconTactic.EntityId) is not { IsDead: false } tacticMonster) continue;
                if (tacticMonster.MapId != mapId)
                {
                    harconTactics.Enqueue(harconTactic);
                    continue;
                }
                if (harconTacticById.TryGetValue(harconTactic.TacticId, out var tacticRow))
                {
                    battleEvents.Add(new tPoseidon.Core.Game.Combat.MonsterBattleCast(tacticMonster.EntityId, tacticMonster.EntityId, tacticRow.SkillId, tacticRow.SkillLevel, tacticRow.Speech));
                }
            }
            if (!await GunShotsAsync(sessions, mapId, now, battleEvents).ConfigureAwait(false)) return;
            foreach (var strike in playerMeleeTicker.Tick(attackers, now, HardenedMonster, EvadeBlocked))
            {
                InGameSessionState? striker = null;
                foreach (var session in sessions)
                {
                    if (session.Value.PlayerEntityId != strike.AttackerEntityId) continue;
                    striker = session.Value;
                    Console.WriteLine($"{LogTag} [combat] 0x{strike.AttackerEntityId:X4} -> 0x{strike.TargetEntityId:X4} result={strike.HitKind} damage={strike.Damage} targetHp={strike.MobHpAfter}/{strike.MobMaxHp} (AP {session.Value.PlayerAp}, AC {session.Value.PlayerAc}, LK {session.Value.PlayerLk}, DX {session.Value.PlayerDx})");
                    session.Value.NextSwingAt = strike.Killed
                        ? default
                        : combatInfoByType.TryGetValue(session.Value.PlayerCharacterType, out var attackerInfo) && attackerInfo.AttackMs > 0
                            ? now.AddMilliseconds(
                                (strike.HitKind & tPoseidon.Core.Game.Combat.MeleeResolution.ResultKindMask)
                                    == tPoseidon.Core.Game.Combat.MeleeResolution.ResultCritical
                                    ? attackerInfo.PlayerCriticalSwingDelayMs(session.Value.PlayerDx)
                                    : attackerInfo.PlayerSwingDelayMs(session.Value.PlayerDx))
                            : strike.NextSwingAtUtc;
                    if (strike.Killed) session.Value.CurrentTargetEntityId = 0;
                    session.Value.SwingsOnTarget = strike.Killed ? 0 : session.Value.SwingsOnTarget + 1;
                    session.Value.FollowSettled = !strike.Killed;
                }
                var (strikeAdditions, ownerEntityId) = await ResolveStrikeAsync(
                    strike, striker, mapId, sessions, zoneGeometry, now, battleEvents,
                    CombatExperience.NormalAttackDivisor, takesOwnerWithoutDamage: false).ConfigureAwait(false);
                swingBlockId = commandBlockIds.Next();
                if (!await SendStrikeFramesAsync(
                    MeleeSwingFrames.Encode(strike, swingBlockId, strikeAdditions),
                    strike.AttackerEntityId, strike.TargetEntityId, ownerEntityId, sessions).ConfigureAwait(false))
                {
                    return;
                }
                if (striker is not null && sessions.FirstOrDefault(entry => entry.Value == striker) is { Value: not null } strikerSession)
                {
                    if (!await CrazyShockwaveAsync(striker, strike, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    if (!await FillCrazyGaugeAsync(strikerSession, sessions, CrazyHammer.AttackPointColumn, mapId, now).ConfigureAwait(false)) return;
                }
            }
            foreach (var (pvpAttacker, pvpVictim) in pvpSwings)
            {
                if (!await PvpSwingAsync(pvpAttacker, pvpVictim, sessions, mapId, now).ConfigureAwait(false)) return;
            }

            foreach (var session in sessions)
            {
                var hunter = session.Value;
                var sinceHuntTick = hunter.HuntTickedAt == default ? TimeSpan.Zero : now - hunter.HuntTickedAt;
                hunter.HuntTickedAt = now;
                if (hunter.Quests is not { } hunterQuests) continue;
                foreach (var (huntOpcode, huntPayload) in hunterQuests.AdvanceHunt((int)sinceHuntTick.TotalMilliseconds))
                {
                    if (!await SendFromLoopAsync(
                        new[] { session }, huntOpcode, huntPayload, MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                    {
                        return;
                    }
                }
            }

            if (!await TickSkillEffectsAsync(sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;

            foreach (var session in sessions)
            {
                var caster = session.Value;
                var sinceCooldownTick = caster.CooldownsTickedAt == default ? TimeSpan.Zero : now - caster.CooldownsTickedAt;
                caster.CooldownsTickedAt = now;
                caster.Cooldowns.Tick((int)sinceCooldownTick.TotalMilliseconds);

                if (caster.Cast is not { IsUnderWay: true } cast) continue;
                if (caster.IsDead || caster.Stats is not { } casterStats || caster.Character is not { } casterCharacter)
                {
                    caster.Cast = null;
                    continue;
                }

                var castElapsedMs = cast.Elapse(now);
                var castTarget = monsterManager.Get(cast.TargetEntityId);
                var castSkill = cast.Skill;
                var castConditions = castSkill.ConditionsAt(cast.Level);
                if (cast.Phase == SkillCastPhase.Chasing)
                {
                    var chasedPlayer = castTarget is null && SupportSkills.TargetsPlayers(castSkill.ActionId)
                        ? liveSessions.Values.FirstOrDefault(chasedSt => chasedSt.PlayerEntityId == cast.TargetEntityId
                            && chasedSt.IsDead == SupportSkills.TargetsDead(castSkill.ActionId))
                        : null;
                    var gaveUp = (cast.TargetPoint is null
                            && (castTarget is null || castTarget.MapId != mapId) && (chasedPlayer is null || chasedPlayer.MapId != mapId))
                        || cast.ChaseTimedOut(castElapsedMs);
                    if (!gaveUp && now < cast.ChaseArrivesAt) continue;
                    if (!gaveUp)
                    {
                        caster.PlayerX = cast.ChaseEnd.X;
                        caster.PlayerY = cast.ChaseEnd.Y;
                        var (reachedX, reachedY) = cast.TargetPoint is { } chasedPoint
                            ? (chasedPoint.X, chasedPoint.Y)
                            : chasedPlayer is not null
                                ? (chasedPlayer.PlayerX, chasedPlayer.PlayerY)
                                : castTarget!.GetInterpolatedPosition(now);
                        var castRange = SkillChecks.RangeOf(castConditions);
                        if (SkillChecks.IsWithinRange(castRange, caster.PlayerX, caster.PlayerY, reachedX, reachedY))
                        {
                            cast.StartCasting(cast.TargetEntityId, now);
                            if (cast.DelayLeftMs > 0)
                            {
                                if (!await SendFromLoopAsync(
                                    sessions, Opcode.S2C_OP_0019_TBD,
                                    new Packet0019Tbd
                                    {
                                        EntityId = caster.PlayerEntityId,
                                        WorldX = caster.PlayerX,
                                        WorldY = caster.PlayerY,
                                        StateFlag = caster.PlayerMoveType,
                                    }.EncodePayload(),
                                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)
                                    || !await SendFromLoopAsync(
                                        sessions, Opcode.S2C_OP_00F2_TBD,
                                        cast.TargetPoint is { } castingPoint
                                            ? SkillFrames.CastStartedAt(caster.PlayerEntityId, (ushort)castSkill.Id, (byte)cast.Level, castingPoint)
                                            : SkillFrames.CastStarted(caster.PlayerEntityId, (ushort)castSkill.Id, (byte)cast.Level, cast.TargetEntityId),
                                        MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                                {
                                    return;
                                }
                                Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} is in range of 0x{cast.TargetEntityId:X4}: skill {castSkill.Id} casts for {cast.DelayLeftMs} ms");
                                continue;
                            }
                        }
                        else
                        {
                            var walkSpeed = playerMoveSpeeds is { } chaseSpeeds
                                ? SpeedOfMoveType(chaseSpeeds, tPoseidon.Core.Game.Combat.MeleeApproach.RunMoveType, caster.GameMasterSpeed)
                                : 0f;
                            var (stopX, stopY) = SkillChecks.ChaseEnd(castRange, walkSpeed, caster.PlayerX, caster.PlayerY, reachedX, reachedY);
                            var walk = walkSpeed > 0 && zoneGeometry is not null
                                ? WanderRoute.FindToPoint(zoneGeometry, caster.PlayerX, caster.PlayerY, stopX, stopY)
                                : null;
                            if (walk is null)
                            {
                                gaveUp = true;
                            }
                            else
                            {
                                var walkFromX = caster.PlayerX;
                                var walkFromY = caster.PlayerY;
                                cast.Chase(
                                    cast.TargetEntityId, (ushort)walk.DestinationX, (ushort)walk.DestinationY,
                                    now.AddMilliseconds(Math.Ceiling(walk.Length / walkSpeed * 1000.0)), now);
                                if (!await SendFromLoopAsync(
                                    sessions, Opcode.ENTITY_MOVE,
                                    new EntityMovementPacket
                                    {
                                        EntityId = caster.PlayerEntityId,
                                        FromX = walkFromX,
                                        FromY = walkFromY,
                                        ToX = (ushort)walk.DestinationX,
                                        ToY = (ushort)walk.DestinationY,
                                        Flag = tPoseidon.Core.Game.Combat.MeleeApproach.RunMoveType,
                                    }.EncodePayload(),
                                    MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                                {
                                    return;
                                }
                                continue;
                            }
                        }
                    }
                    if (gaveUp)
                    {
                        Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} gives up skill {castSkill.Id}: target 0x{cast.TargetEntityId:X4} not reached");
                        if (!await EndCastAsync(session, sessions, cast, Opcode.SKILL_CAST_CANCEL, Array.Empty<byte>(), now).ConfigureAwait(false)) return;
                        continue;
                    }
                }
                else if (!cast.DelayIsOver(castElapsedMs))
                {
                    continue;
                }

                var castTargetDefinition = castTarget is null ? null : monsterCatalog.GetById(castTarget.MonsterParamId);
                var castAction = castSkill.ActionAt(cast.Level);
                mapKindById.TryGetValue(mapId, out var castMap);
                if (castSkill.Row.StateType is not (0 or SkillDefinition.NormalForm or 4)
                    || (castSkill.Has(SkillDefinition.NotInTownOption) && SkillChecks.IsTownMap(castMap.Type)))
                {
                    Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} may not use skill {castSkill.Id} here (state type {castSkill.Row.StateType}, map type {castMap.Type})");
                    if (!await SendFromLoopAsync(
                        new[] { session }, Opcode.S2C_SKILL_NOTICE, SkillFrames.Notice(SkillFailure.WrongPlaceNotice, 0, 0),
                        MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)
                        || !await EndCastAsync(
                            session, sessions, cast, Opcode.S2C_SKILL_REFUSED, SkillFrames.Refused(SkillFailure.RefusedWithNotice), now).ConfigureAwait(false))
                    {
                        return;
                    }
                    continue;
                }
                var castEffectKind = tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(castSkill.ActionId);
                if (cast.TargetPoint is { } emotePoint && EventSkills.IsPointEmote(castSkill.ActionId))
                {
                    caster.Cast = null;
                    if (await PayForCastAsync(session, castSkill, cast.Level, castConditions, sessions, mapId, now) is null) return;
                    if (!await EventPointAsync(session, sessions, castSkill, emotePoint).ConfigureAwait(false)) return;
                    continue;
                }
                if (cast.TargetPoint is { } groundPoint && castAction is not null)
                {
                    caster.Cast = null;
                    if (await PayForCastAsync(session, castSkill, cast.Level, castConditions, sessions, mapId, now) is null) return;
                    if (!await GroundSkillAsync(session, sessions, castSkill, castAction, groundPoint, mapId, now).ConfigureAwait(false)) return;
                    continue;
                }
                if (GunSkills.HitsAround(castSkill.ActionId) && castAction is not null && cast.TargetEntityId == caster.PlayerEntityId)
                {
                    caster.Cast = null;
                    if (await PayForCastAsync(session, castSkill, cast.Level, castConditions, sessions, mapId, now) is null) return;
                    if (!await GunAreaAsync(session, sessions, castSkill, castAction, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    continue;
                }
                if (ObjectHits.IsSelf(castSkill.ActionId) && (castAction is not null || FormSkills.IsSelf(castSkill.ActionId))
                    && cast.TargetEntityId == caster.PlayerEntityId)
                {
                    caster.Cast = null;
                    if (await PayForCastAsync(session, castSkill, cast.Level, castConditions, sessions, mapId, now) is null) return;
                    var selfDone = castAction is null || FormSkills.IsSelf(castSkill.ActionId)
                        ? await FormSelfAsync(session, sessions, castSkill).ConfigureAwait(false)
                        : await ObjectSelfAsync(session, sessions, castSkill, castAction, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
                    if (!selfDone) return;
                    continue;
                }
                if (SkillAreaShapes.Find(castSkill.ActionId) is { } selfArea && !castSkill.AsksForTarget
                    && castAction is not null && cast.TargetEntityId == caster.PlayerEntityId)
                {
                    caster.Cast = null;
                    if (await PayForCastAsync(session, castSkill, cast.Level, castConditions, sessions, mapId, now) is null) return;
                    if (!await AreaSkillAsync(session, sessions, castSkill, castAction, selfArea, null, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    continue;
                }
                if (EventSkills.IsSelf(castSkill.ActionId) && cast.TargetEntityId == caster.PlayerEntityId)
                {
                    caster.Cast = null;
                    if (await PayForCastAsync(session, castSkill, cast.Level, castConditions, sessions, mapId, now) is null) return;
                    if (!await EventEmoteAsync(session, sessions, castSkill, session).ConfigureAwait(false)) return;
                    continue;
                }
                if (castEffectKind is { OnTarget: false } && castAction is not null && cast.TargetEntityId == caster.PlayerEntityId)
                {
                    caster.Cast = null;
                    if (await PayForCastAsync(session, castSkill, cast.Level, castConditions, sessions, mapId, now) is not { } selfMpCost) return;
                    if (!await StartSkillEffectAsync(session, sessions, castSkill, castEffectKind, castAction, null, now).ConfigureAwait(false)) return;
                    if (cast.ResumeAttackOn != 0
                        && !await StartAttackAfterSkillAsync(session, sessions, cast.ResumeAttackOn, SkillActions.AttackResumeDelayMs, now).ConfigureAwait(false))
                    {
                        return;
                    }
                    continue;
                }
                if (EventSkills.IsTargeted(castSkill.ActionId) && castTarget is null)
                {
                    var aimedAt = liveSessions.FirstOrDefault(aimedSt => aimedSt.Value.PlayerEntityId == cast.TargetEntityId);
                    if (aimedAt.Value is null || aimedAt.Value.MapId != mapId || aimedAt.Value.IsDead)
                    {
                        if (!await EndCastAsync(
                            session, sessions, cast, Opcode.S2C_SKILL_REFUSED, SkillFrames.Refused(SkillFailure.General), now).ConfigureAwait(false))
                        {
                            return;
                        }
                        continue;
                    }
                    caster.Cast = null;
                    if (await PayForCastAsync(session, castSkill, cast.Level, castConditions, sessions, mapId, now) is null) return;
                    var eventDone = EventSkills.IsCoupleHeart(castSkill.ActionId)
                        ? await CoupleHeartAsync(session, sessions, castSkill, castAction, aimedAt, now).ConfigureAwait(false)
                        : EventSkills.IsGmTarget(castSkill.ActionId)
                            ? await GmBuffAsync(session, sessions, castSkill, castAction, aimedAt, now).ConfigureAwait(false)
                            : await EventEmoteAsync(session, sessions, castSkill, aimedAt).ConfigureAwait(false);
                    if (!eventDone) return;
                    continue;
                }
                if (SupportSkills.TargetsPlayers(castSkill.ActionId) && castTarget is null && castAction is not null)
                {
                    var cured = liveSessions.FirstOrDefault(curedSt => curedSt.Value.PlayerEntityId == cast.TargetEntityId);
                    if (cured.Value is null || cured.Value.MapId != mapId || cured.Value.IsDead != SupportSkills.TargetsDead(castSkill.ActionId))
                    {
                        if (!await EndCastAsync(
                            session, sessions, cast, Opcode.S2C_SKILL_REFUSED, SkillFrames.Refused(SkillFailure.General), now).ConfigureAwait(false))
                        {
                            return;
                        }
                        continue;
                    }
                    caster.Cast = null;
                    if (await PayForCastAsync(session, castSkill, cast.Level, castConditions, sessions, mapId, now) is null) return;
                    var supported = castSkill.ActionId switch
                    {
                        SupportSkills.BasicHealing => await BasicHealingAsync(session, sessions, castSkill, castAction, cured, null).ConfigureAwait(false),
                        SupportSkills.Revival => await RevivalAsync(session, sessions, castSkill, castAction, cured, now).ConfigureAwait(false),
                        ScrollSkills.RainOfHeart => tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(ScrollSkills.RainOfHeart) is not { } rainKind
                            || await StartSkillEffectAsync(cured, sessions, castSkill, rainKind, castAction, null, now, caster.PlayerEntityId).ConfigureAwait(false),
                        _ => await CureAsync(session, sessions, castSkill, castAction, cured, null).ConfigureAwait(false),
                    };
                    if (!supported) return;
                    continue;
                }
                if (castTarget is null
                    || castTarget.MapId != mapId
                    || castTarget.IsDead
                    || castTargetDefinition is null
                    || castAction is null
                    || (!SkillActions.MayTarget(castTargetDefinition) && castSkill.ActionId is not (SkillActions.Cure or SupportSkills.BasicHealing))
                    || castSkill.ActionId == SupportSkills.Revival)
                {
                    Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} loses skill {castSkill.Id}: target 0x{cast.TargetEntityId:X4} is gone or may not be hit");
                    if (!await EndCastAsync(
                        session, sessions, cast, Opcode.S2C_SKILL_REFUSED, SkillFrames.Refused(SkillFailure.General), now).ConfigureAwait(false))
                    {
                        return;
                    }
                    continue;
                }

                caster.Cast = null;
                if (await PayForCastAsync(session, castSkill, cast.Level, castConditions, sessions, mapId, now) is not { } mpCost) return;
                if (castSkill.ActionId == SkillActions.Cure)
                {
                    if (!await CureAsync(session, sessions, castSkill, castAction, null, castTarget).ConfigureAwait(false)) return;
                    continue;
                }
                if (castSkill.ActionId == SupportSkills.BasicHealing)
                {
                    if (!await BasicHealingAsync(session, sessions, castSkill, castAction, null, castTarget).ConfigureAwait(false)) return;
                    continue;
                }
                if (ObjectHits.IsTargeted(castSkill.ActionId))
                {
                    if (!await ObjectTargetAsync(session, sessions, castSkill, castAction, castTarget, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    continue;
                }
                if (SkillAreaShapes.Find(castSkill.ActionId) is { } targetArea)
                {
                    if (!await AreaSkillAsync(session, sessions, castSkill, castAction, targetArea, castTarget, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    continue;
                }
                if (ThrowSkills.IsThrow(castSkill.ActionId))
                {
                    if (!await ThrowSkillAsync(session, sessions, castSkill, castAction, castTarget, castTargetDefinition, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    continue;
                }
                if (castSkill.ActionId is GunSkills.PowerShot or GunSkills.DoubleShot)
                {
                    if (!await GunShotAsync(session, sessions, castSkill, castAction, castTarget, castTargetDefinition, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    continue;
                }
                if (ChainHits.Find(castSkill.ActionId) is { } chainShape)
                {
                    if (!await ChainHitsAsync(session, sessions, castSkill, castAction, chainShape, castTarget, cast, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    continue;
                }
                if (PolarBearHits.CountOf(castSkill.ActionId) > 0)
                {
                    if (!await PolarBearHitsAsync(session, sessions, castSkill, castAction, castTarget, castTargetDefinition, cast, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    continue;
                }
                if (castSkill.ActionId == SkillActions.TornadoBomb)
                {
                    if (!await TornadoBombAsync(session, sessions, castSkill, castAction, castTarget, castTargetDefinition, cast, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    continue;
                }
                if (castSkill.ActionId == SkillAreaRules.ManaRing)
                {
                    if (!await ManaRingAsync(session, sessions, castSkill, castAction, castTarget, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false)) return;
                    continue;
                }
                if (castEffectKind is { OnTarget: true })
                {
                    if (!await StartSkillEffectAsync(session, sessions, castSkill, castEffectKind, castAction, castTarget, now).ConfigureAwait(false)) return;
                    if (cast.ResumeAttackOn != 0
                        && !await StartAttackAfterSkillAsync(session, sessions, cast.ResumeAttackOn, SkillActions.AttackResumeDelayMs, now).ConfigureAwait(false))
                    {
                        return;
                    }
                    continue;
                }

                if (castSkill.ActionId == SkillHitShapes.HeavyHit
                    && !await MassiveAttackAsync(caster, sessions, castSkill, castAction, castTarget, mapId, now).ConfigureAwait(false))
                {
                    return;
                }
                var castElement = SkillActions.ElementOf(castSkill.ActionId);
                var castShape = SkillHitShapes.Find(castSkill.ActionId);
                var castEnhancerId = SkillActions.EnhancerOf(castSkill.ActionId);
                var castEnhancer = castEnhancerId != 0 && caster.Skills.TryGetValue(castEnhancerId, out var enhancerLevel)
                    ? skillBook.Find(castEnhancerId)?.ActionAt(enhancerLevel)
                    : null;
                var castHits = SkillActions.HitsOf(castSkill.ActionId, castEnhancer);
                var castCasterTwice = castShape is { TargetInHeader: false } || castSkill.ActionId == SkillActions.ManaArrow;
                var castTail = castShape?.Tail ?? (castSkill.ActionId == SkillActions.ManaArrow ? SkillTail.Positions : SkillTail.None);
                var castTakesOwner = castShape?.TakesOwner ?? castSkill.ActionId != SkillActions.ManaArrow;
                var skillStrike = default(tPoseidon.Core.Game.Combat.PlayerStrike);
                for (var castHit = 0; castHit < castHits && !castTarget.IsDead; castHit++)
                {
                    var skillAttacker = new SkillAttacker(
                        caster.PlayerAp, caster.PlayerAc, caster.PlayerMa, caster.PlayerLk, casterStats.Level,
                        caster.PlayerDa, casterStats.CurrentHp, caster.PlayerMaxHp,
                        caster.PlayerHv, caster.PlayerDx, WeaponDa: WeaponDaOf(casterCharacter.CharacterId), DarkBonus: DarkBonusOf(caster),
                        ElementBonus: castShape is { ElementSlot: >= 0 } elementShape ? elementShape.ElementStep * ElementTotalOf(caster, elementShape.ElementSlot) : 0);
                    var skillTarget = new SkillTarget(
                        castTargetDefinition.DpLv + castTarget.StatDelta(MonsterStat.Dp),
                        castTargetDefinition.HvLv + castTarget.StatDelta(MonsterStat.Hv),
                        castTargetDefinition.MdLv + castTarget.StatDelta(MonsterStat.Md),
                        castTargetDefinition.LkLv + castTarget.StatDelta(MonsterStat.Lk),
                        castTarget.CurrentHp, castTarget.Level, SkillActions.ResistOf(castTargetDefinition, castElement) - castTarget.ResistLoss(castElement),
                        castTarget.MaxHp, castTargetDefinition.DpLv);
                    var skillHit = SkillActions.Resolve(castSkill.ActionId, castAction, skillAttacker, skillTarget, npcLoopRng, castEnhancer);
                    var counterPunched = false;
                    if (castSkill.ActionId == SkillRiders.Uppercut
                        && caster.Skills.TryGetValue(SkillRiders.CounterPunch, out var counterLevel)
                        && skillBook.Find(SkillRiders.CounterPunch)?.ActionAt(counterLevel) is { } counterRow
                        && SkillRiders.Gates(skillHit, castTarget.CurrentHp))
                    {
                        SkillHit counter;
                        lock (npcLoopRng)
                        {
                            counter = SkillDamage.Physical(
                                skillAttacker, WireLimits.Saturate(caster.PlayerAp * (double)(counterRow.Value(SkillHitShapes.RatioColumn) ?? 0f)), skillTarget,
                                SkillRiders.CounterPunchCoefficient(caster.PlayerAc), SkillActions.NoElement, npcLoopRng);
                        }
                        if (counter.Damage > 0)
                        {
                            skillHit = skillHit with { Damage = skillHit.Damage + counter.Damage };
                            counterPunched = true;
                        }
                    }
                    var hpBeforeSkill = castTarget.CurrentHp;
                    castTarget.AddAttacker(caster.PlayerEntityId);
                    castTarget.ApplyDamage(skillHit.Damage);
                    skillStrike = new tPoseidon.Core.Game.Combat.PlayerStrike(
                        AttackerEntityId: caster.PlayerEntityId,
                        TargetEntityId: castTarget.EntityId,
                        AttackerX: caster.PlayerX,
                        AttackerY: caster.PlayerY,
                        TargetX: castTarget.CurrentX,
                        TargetY: castTarget.CurrentY,
                        Damage: skillHit.Damage,
                        MobHpAfter: castTarget.CurrentHp,
                        HitFlags: 0,
                        HitKind: (uint)skillHit.Kind,
                        Killed: castTarget.IsDead,
                        NextSwingAtUtc: now,
                        MonsterParamId: castTarget.MonsterParamId,
                        MobMaxHp: castTarget.MaxHp,
                        MobHpBefore: hpBeforeSkill);
                    Console.WriteLine($"{LogTag} [skill] 0x{caster.PlayerEntityId:X4} skill {castSkill.Id} (action {castSkill.ActionId}) -> 0x{castTarget.EntityId:X4} kind={skillHit.Kind} damage={skillHit.Damage} targetHp={castTarget.CurrentHp}/{castTarget.MaxHp}, MP {casterStats.CurrentMp} (-{mpCost})");
                    if (skillStrike.Killed) caster.CurrentTargetEntityId = 0;

                    var (skillAdditions, skillOwnerEntityId) = await ResolveStrikeAsync(
                        skillStrike, caster, mapId, sessions, zoneGeometry, now, battleEvents,
                        SkillActions.ExperienceDivisor, takesOwnerWithoutDamage: castTakesOwner).ConfigureAwait(false);
                    var skillTmFrames = (castHit == castHits - 1 || castTarget.IsDead) && caster.GainSkillTmExp is { } gainSkillTm
                        ? gainSkillTm(castShape is { FlatTm: true }
                            ? castSkill.Row.SkillExp
                            : SkillActions.TmExpForUse(castSkill.Row.SkillExp, casterStats.Level, castTarget.Level))
                        : Array.Empty<(Opcode Opcode, byte[] Payload)>();
                    swingBlockId = commandBlockIds.Next();
                    if (!await SendStrikeFramesAsync(
                        SkillFrames.EncodeHit(
                            skillStrike, castSkill, skillHit, swingBlockId, skillAdditions, skillTmFrames,
                            casterTwice: castCasterTwice,
                            tail: castTail == SkillTail.None ? null : SkillFrames.TailFor(castTail, skillStrike, castAction.Amount(SkillHitShapes.CountColumn) ?? 0, counterPunched)),
                        skillStrike.AttackerEntityId, skillStrike.TargetEntityId, skillOwnerEntityId, sessions).ConfigureAwait(false))
                    {
                        return;
                    }
                    if (!await RideAsync(session, sessions, castSkill, castAction, skillHit, castTarget, now).ConfigureAwait(false)) return;
                }

                if (cast.ResumeAttackOn != 0 && SkillHitShapes.KeepsEarlierAttack(castSkill.ActionId))
                {
                    if (!await StartAttackAfterSkillAsync(
                        session, sessions, cast.ResumeAttackOn, SkillActions.AttackResumeDelayMs, now).ConfigureAwait(false))
                    {
                        return;
                    }
                }
                else if (!skillStrike.Killed)
                {
                    var holdsGun = itemStore.GetForCharacterBySlotKind(casterCharacter.CharacterId, SlotKind.Equipment)
                        .Any(worn => itemCatalog.GetById(worn.ItemIndex)?.SubType == SkillActions.GunItemSubType);
                    var restartDelayMs = combatInfoByType.TryGetValue(caster.PlayerCharacterType, out var restartInfo)
                        ? SkillHitShapes.RestartDelayMs(castSkill.ActionId, restartInfo.AttackMs, restartInfo.AttackReadyMs, SkillActions.AttackStartDelayMs)
                        : SkillActions.AttackStartDelayMs;
                    if (SkillActions.StartsAttackAfterwards(castSkill.ActionId, holdsGun)
                        && !await StartAttackAfterSkillAsync(
                            session, sessions, castTarget.EntityId, restartDelayMs, now).ConfigureAwait(false))
                    {
                        return;
                    }
                }
            }
            foreach (var session in sessions)
            {
                var walker = session.Value;
                if (walker.PortalWalk is not { } portalWalk || now < portalWalk.ArrivesAt) continue;
                walker.PortalWalk = null;
                walker.PlayerX = portalWalk.End.X;
                walker.PlayerY = portalWalk.End.Y;
                await portalWalk.OnArrival().ConfigureAwait(false);
            }

            await puzzles.StepAsync(mapId, now).ConfigureAwait(false);

            foreach (var session in sessions)
            {
                var digger = session.Value;
                if (digger.Dig is not { Begun: false } walkedDig || walkedDig.BeginsAt == default || now < walkedDig.BeginsAt) continue;
                if (walkedDig.WalkEnd is { } digArrival)
                {
                    digger.PlayerX = digArrival.X;
                    digger.PlayerY = digArrival.Y;
                    walkedDig.WalkEnd = null;
                }
                await drillController.BeginAsync(digger).ConfigureAwait(false);
            }

            foreach (var session in sessions)
            {
                var picker = session.Value;
                if (picker.PickupItemEntityId == 0 || now < picker.PickupAt) continue;
                var pickedEntityId = picker.PickupItemEntityId;
                picker.PickupItemEntityId = 0;
                if (picker.PickupWalkEnd is { } walkedTo)
                {
                    picker.PlayerX = walkedTo.X;
                    picker.PlayerY = walkedTo.Y;
                    picker.PickupWalkEnd = null;
                }
                if (picker.IsDead || picker.Character is not { } pickerCharacter || picker.Stats is not { } pickerStats) continue;
                if (groundItems.Find(pickedEntityId) is not { } picked || picked.MapId != mapId) continue;
                if (!PickupRules.IsWithinReach(picker.PlayerX, picker.PlayerY, picked.X, picked.Y)) continue;
                if (!PickupRules.MayTake(picked, picker.PlayerEntityId, now))
                {
                    Console.WriteLine($"{LogTag} [items] 0x{picker.PlayerEntityId:X4} may not take item 0x{pickedEntityId:X4} yet: it belongs to 0x{picked.OwnerEntityId:X4}");
                    continue;
                }
                if (itemCatalog.GetById(picked.ItemId) is not { } pickedDefinition) continue;

                var pickerOnly = new List<KeyValuePair<S2SessionContext, InGameSessionState>> { session };
                var pickupFrames = new List<(Opcode Opcode, byte[] Payload, ushort BlockId, bool ToPickerOnly)>();
                if (picked.IsMoney)
                {
                    if (!PickupRules.MoneyFits(pickerStats.Money, picked.Count, pickerStats.WalletLimit))
                    {
                        Console.WriteLine($"{LogTag} [items] 0x{picker.PlayerEntityId:X4} cannot hold {picked.Count} more money (has {pickerStats.Money})");
                        pickupFrames.Add((Opcode.S2C_MONEY_FULL, GroundItemFrames.MoneyFull(), MeleeSwingFrames.NoBlock, true));
                    }
                    else
                    {
                        pickerStats.GrantMoney(picked.Count, StatChangeSource.Item);
                        groundItems.Remove(pickedEntityId);
                        idAllocator.Release(pickedEntityId);
                        swingBlockId = commandBlockIds.Next();
                        pickupFrames.Add((Opcode.S2C_ITEM_PICKED_UP,
                            GroundItemFrames.PickedUp(picker.PlayerEntityId, picked, GroundItemFrames.MoneyPickupCount), swingBlockId, false));
                        pickupFrames.Add((Opcode.S2C_MONEY, GroundItemFrames.Money(pickerStats.Money), swingBlockId, true));
                        pickupFrames.Add((Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), swingBlockId, false));
                        pickupFrames.Add((Opcode.S2C_GROUND_ITEM_DESPAWN, GroundItemFrames.Removed(pickedEntityId), MeleeSwingFrames.NoBlock, false));
                        Console.WriteLine($"{LogTag} [items] 0x{picker.PlayerEntityId:X4} picks up {picked.Count} money (0x{pickedEntityId:X4}): now {pickerStats.Money}");
                    }
                }
                else
                {
                    var bag = itemStore.GetForCharacterBySlotKind(pickerCharacter.CharacterId, SlotKind.Inventory);
                    var bagWeight = 0;
                    Item? stack = null;
                    byte bagMixSlot = 0;
                    foreach (var held in bag)
                    {
                        bagWeight += (itemCatalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits;
                        if (stack is null && pickedDefinition.IsStackable && held.ItemIndex == picked.ItemId) stack = held;
                    }
                    var pickedUnits = picked.ItemClass == Item.RareClass ? 1 : picked.Count;
                    if (!PickupRules.Fits(picker.PlayerMaxWt, bagWeight, bag.Count, stack is not null, pickedDefinition.Weight, pickedUnits))
                    {
                        Console.WriteLine($"{LogTag} [items] 0x{picker.PlayerEntityId:X4} cannot carry item {picked.ItemId} x{picked.Count}: bag weight {bagWeight} of {picker.PlayerMaxWt}, {bag.Count} items");
                        if (tPoseidon.Core.Game.Drilling.AutoDrillRules.PickableCount(picker.PlayerMaxWt, bagWeight, pickedDefinition.Weight, picked.Count) <= 0)
                        {
                            foreach (var (autoOpcode, autoPayload, autoToPicker) in drillController.StopAutoFrames(picker))
                            {
                                pickupFrames.Add((autoOpcode, autoPayload, MeleeSwingFrames.NoBlock, autoToPicker));
                            }
                        }
                        pickupFrames.Add((Opcode.S2C_PICKUP_REFUSED, Array.Empty<byte>(), MeleeSwingFrames.NoBlock, true));
                    }
                    else
                    {
                        var pickedCount = (ushort)Math.Clamp(picked.Count, 0, ushort.MaxValue);
                        byte bagChange;
                        ulong bagItemUid;
                        ushort bagItemCount;
                        if (stack is not null)
                        {
                            bagChange = PickupRules.StackGrew;
                            bagItemUid = stack.ItemUid;
                            bagItemCount = itemStore.AdjustStack(stack.ItemUid, pickedCount);
                        }
                        else if (picked.ItemUid != 0
                            && itemStore.AssignToCharacter(picked.ItemUid, pickerCharacter.CharacterId, SlotKind.Inventory, FreeBagPosition(bag))
                            && itemStore.FindByUid(picked.ItemUid) is { } returned)
                        {
                            bagChange = PickupRules.ItemCreated;
                            bagItemUid = returned.ItemUid;
                            bagItemCount = returned.Stack;
                            bagMixSlot = returned.ItemSlot;
                        }
                        else
                        {
                            var created = itemStore.GrantToCharacter(
                                pickerCharacter.CharacterId, picked.ItemId, picked.ItemClass, SlotKind.Inventory, FreeBagPosition(bag), pickedCount);
                            bagChange = PickupRules.ItemCreated;
                            bagItemUid = created.ItemUid;
                            bagItemCount = created.Stack;
                            bagMixSlot = created.ItemSlot;
                        }
                        groundItems.Remove(pickedEntityId);
                        idAllocator.Release(pickedEntityId);
                        if (tPoseidon.Core.Game.Items.UniqueItemKinds.KindOf(pickedDefinition.Options) != tPoseidon.Core.Game.Items.UniqueItemKinds.None)
                        {
                            await masterUniqueItems.PickedUpAsync(pickerCharacter.CharacterId, bagItemUid, picked.ItemId, npcLoopCt).ConfigureAwait(false);
                        }
                        pickupFrames.Add((Opcode.S2C_ITEM_PICKED_UP,
                            GroundItemFrames.PickedUp(picker.PlayerEntityId, picked, pickedCount), MeleeSwingFrames.NoBlock, false));
                        pickupFrames.Add((Opcode.S2C_ADD_TO_INVENTORY,
                            GroundItemFrames.AddedToBag(bagChange, picked.ItemClass, (uint)picked.ItemId, BagItemIds.OnWire(bagItemUid), bagItemCount, bagMixSlot), MeleeSwingFrames.NoBlock, true));
                        pickupFrames.Add((Opcode.S2C_GROUND_ITEM_DESPAWN, GroundItemFrames.Removed(pickedEntityId), MeleeSwingFrames.NoBlock, false));
                        Console.WriteLine($"{LogTag} [items] 0x{picker.PlayerEntityId:X4} picks up item {picked.ItemId} x{pickedCount} (0x{pickedEntityId:X4}): bag item 0x{bagItemUid:X} now holds {bagItemCount} (change {bagChange})");
                    }
                }

                foreach (var (opcode, payload, blockId, toPickerOnly) in pickupFrames)
                {
                    if (!await SendFromLoopAsync(
                        toPickerOnly ? pickerOnly : sessions, opcode, payload, blockId, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                    {
                        return;
                    }
                }
            }

            foreach (var session in sessions)
            {
                var revived = session.Value;
                if (revived.ResurrectionCompletesAt == default || now < revived.ResurrectionCompletesAt) continue;
                if (revived.Stats is not { } revivedStats || revived.Character is null) continue;
                revived.ResurrectionCompletesAt = default;
                revived.IsDead = false;
                await systemLink.RevivedAsync(revived.Character.CharacterId, npcLoopCt).ConfigureAwait(false);
                revived.ProtectedUntil = now + PlayerRevivalFrames.Protection;
                var respawnPayload = tPoseidon.Kr2014.Wire.PlayerSpawnLoginPacket.FromCharacter(
                    revived.Character, revived.PlayerEntityId, itemStore, (revived.PlayerX, revived.PlayerY),
                    revived.AnnouncedSpeedQuarter, GameMasterController.SpawnFlagsOf(revived)).EncodePayload();
                Console.WriteLine($"{LogTag} [combat] player 0x{revived.PlayerEntityId:X4} is alive again: HP {revivedStats.CurrentHp}, MP {revivedStats.CurrentMp}");
                try
                {
                    foreach (var (huntOpcode, huntPayload) in revived.Quests?.ResumeHuntAfterResurrection() ?? Array.Empty<(Opcode Opcode, byte[] Payload)>())
                    {
                        await revived.SendFrameAsync(huntOpcode, huntPayload, (byte)npcLoopRng.Next(1, 256), npcLoopCt).ConfigureAwait(false);
                    }
                    foreach (var watcher in sessions)
                    {
                        await watcher.Value.SendFrameAsync(Opcode.NPC_SPAWN, respawnPayload, (byte)npcLoopRng.Next(1, 256), npcLoopCt).ConfigureAwait(false);
                    }
                    await revived.SendFrameAsync(Opcode.S2C_HEALTH_STATUS,
                        tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(revivedStats.CurrentHp).EncodePayload(), (byte)npcLoopRng.Next(1, 256), npcLoopCt).ConfigureAwait(false);
                    await revived.SendFrameAsync(Opcode.S2C_MANA_STATUS,
                        tPoseidon.Core.Net.Packets.ManaStatusPacket.Of(revivedStats.CurrentMp).EncodePayload(), (byte)npcLoopRng.Next(1, 256), npcLoopCt).ConfigureAwait(false);
                    await revived.SendFrameAsync(Opcode.S2C_RESURRECTION_DONE, Array.Empty<byte>(), (byte)npcLoopRng.Next(1, 256), npcLoopCt).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    liveSessions.TryRemove(session.Key, out _);
                }
                catch (System.IO.IOException)
                {
                    liveSessions.TryRemove(session.Key, out _);
                }
            }

            foreach (var session in sessions)
            {
                var owner = session.Value;
                if (owner.IsDead || owner.Pet is not { } followingPet || followingPet.MapId != mapId || now < followingPet.NextFollowAt) continue;
                followingPet.NextFollowAt = now + tPoseidon.Core.Game.Pets.PetRules.FollowPeriod;
                var (petX, petY) = followingPet.PositionAt(now);
                var (ownerX, ownerY) = owner.Walk is { } ownerWalk && ownerWalk.ToX == owner.PlayerX && ownerWalk.ToY == owner.PlayerY
                    ? ownerWalk.PositionAt(now)
                    : (owner.PlayerX, owner.PlayerY);
                var petFrames = new List<(Opcode Opcode, byte[] Payload)>();
                if (tPoseidon.Core.Game.Pets.PetRules.IsTooFar(followingPet.ToX, followingPet.ToY, ownerX, ownerY))
                {
                    zoneGeometryByMap.TryGetValue(mapId, out var petGeometry);
                    var (targetX, targetY) = tPoseidon.Core.Game.Pets.PetRules.FollowTarget(petGeometry, petX, petY, ownerX, ownerY);
                    var petMoveType = tPoseidon.Core.Game.Combat.MeleeApproach.EffectiveMoveType(owner.PlayerMoveType);
                    var petSpeed = playerMoveSpeeds is { } petSpeeds ? SpeedOfMoveType(petSpeeds, petMoveType) : 0f;
                    (int X, int Y, double Length)? petPath = petGeometry is null
                        ? (targetX, targetY, Math.Sqrt(Math.Pow(targetX - petX, 2) + Math.Pow(targetY - petY, 2)))
                        : (WanderRoute.Find(petGeometry, petX, petY, targetX, targetY)
                            ?? WanderRoute.Find(petGeometry, petX, petY, ownerX, ownerY)) is { } petRoute
                            ? (petRoute.DestinationX, petRoute.DestinationY, petRoute.Length)
                            : null;
                    if (petPath is not { } walk || petSpeed <= 0)
                    {
                        petFrames.Add((Opcode.S2C_ENTITY_TELEPORT, PetFrames.Teleported(followingPet.EntityId, targetX, targetY)));
                        followingPet.PlaceAt(targetX, targetY, now);
                    }
                    else
                    {
                        var petQuarter = ProgressFrames.SpeedQuarter(petSpeed);
                        if (petQuarter != followingPet.SpeedQuarter)
                        {
                            followingPet.SpeedQuarter = petQuarter;
                            petFrames.Add((Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(followingPet.EntityId, petSpeed)));
                        }
                        petFrames.Add((Opcode.ENTITY_MOVE, new EntityMovementPacket
                        {
                            EntityId = followingPet.EntityId,
                            FromX = (ushort)petX,
                            FromY = (ushort)petY,
                            ToX = (ushort)walk.X,
                            ToY = (ushort)walk.Y,
                            Flag = petMoveType,
                        }.EncodePayload()));
                        followingPet.MoveTo(petX, petY, walk.X, walk.Y, now, TimeSpan.FromMilliseconds(Math.Ceiling(walk.Length / petSpeed * 1000.0)));
                    }
                    followingPet.NextEmoteAt = now + tPoseidon.Core.Game.Pets.PetRules.NextIdleEmote(npcLoopRng);
                }
                else if (!followingPet.IsMoving(now) && now >= followingPet.NextEmoteAt)
                {
                    petFrames.Add((Opcode.S2C_OP_003F_TBD, PetFrames.Emote(followingPet.EntityId, tPoseidon.Core.Game.Pets.PetRules.IdleEmote)));
                    followingPet.NextEmoteAt = now + tPoseidon.Core.Game.Pets.PetRules.NextIdleEmote(npcLoopRng);
                }
                foreach (var (petOpcode, petPayload) in petFrames)
                {
                    if (!await SendFromLoopAsync(sessions, petOpcode, petPayload, MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false)) return;
                }
            }

            await UpdateSectorViewsAsync(mapId, sessions.Select(session => session.Value).ToList(), now).ConfigureAwait(false);

            foreach (var session in sessions)
            {
                var resting = session.Value;
                if (resting.IsDead || resting.Stats is not { } restingStats) continue;
                var sinceLastRecovery = resting.RecoveryTickedAt == default ? TimeSpan.Zero : now - resting.RecoveryTickedAt;
                if (sinceLastRecovery > longestRecoveryTick) sinceLastRecovery = longestRecoveryTick;
                resting.RecoveryTickedAt = now;
                var hpBefore = restingStats.CurrentHp;
                var mpBefore = restingStats.CurrentMp;
                var recovered = resting.Recovery.Tick(
                    (int)sinceLastRecovery.TotalMilliseconds,
                    hpBefore,
                    resting.PlayerMaxHp,
                    mpBefore,
                    resting.PlayerMaxMp,
                    resting.Pet is { } restingPet
                        ? tPoseidon.Core.Game.Pets.PetRules.WithRecoveryBonus(ManaRecoveryOf(resting), restingPet.Recovery)
                        : ManaRecoveryOf(resting));
                if (recovered.Hp is null && recovered.Mp is null) continue;
                restingStats.SetVitals(recovered.Hp ?? hpBefore, recovered.Mp ?? mpBefore);
                try
                {
                    if (recovered.Hp is { } hpNow)
                    {
                        await resting.SendFrameAsync(Opcode.S2C_HEALTH_STATUS,
                            tPoseidon.Core.Net.Packets.HealthStatusPacket.Of(hpNow).EncodePayload(), (byte)npcLoopRng.Next(1, 256), npcLoopCt).ConfigureAwait(false);
                    }
                    if (recovered.Mp is { } mpNow)
                    {
                        await resting.SendFrameAsync(Opcode.S2C_MANA_STATUS,
                            tPoseidon.Core.Net.Packets.ManaStatusPacket.Of(mpNow).EncodePayload(), (byte)npcLoopRng.Next(1, 256), npcLoopCt).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    liveSessions.TryRemove(session.Key, out _);
                }
                catch (System.IO.IOException)
                {
                    liveSessions.TryRemove(session.Key, out _);
                }
            }

            var harconBattles = harcon.BattlesOn(mapId);
            foreach (var harconBattle in harconBattles)
            {
                foreach (var (fighterId, foeId) in harconBattle.Engagements(
                    fighterId => HarconFighterAt(fighterId, mapId, now), monsterBattleCycle.IsFighting, HarconReachOf))
                {
                    var chase = harconBattle.SideOf(fighterId) == tPoseidon.Core.Game.HarconDefence.HarconSide.Wave
                        ? tPoseidon.Core.Game.HarconDefence.HarconBattle.WaveChase
                        : (TimeSpan?)null;
                    if (monsterManager.Get(fighterId) is { } engaging && monsterBattleCycle.Engage(engaging, foeId, now, chase) is { } harconHalted)
                    {
                        battleEvents.Add(harconHalted);
                    }
                }
            }
            var battleTargets = new Dictionary<ushort, tPoseidon.Core.Game.Combat.MonsterBattleTarget>();
            foreach (var harconBattle in harconBattles)
            {
                foreach (var fighterId in harconBattle.Fighters)
                {
                    if (monsterManager.Get(fighterId) is { IsDead: false } fighter && fighter.MapId == mapId
                        && monsterCatalog.GetById(fighter.MonsterParamId) is { } fighterDefinition)
                    {
                        battleTargets[fighterId] = MonsterBattleTargetOf(fighter, fighterDefinition, now);
                    }
                }
            }
            foreach (var session in sessions)
            {
                var targetState = session.Value;
                if (targetState.IsDead || targetState.Stats is not { } targetStats) continue;
                var (battleTargetX, battleTargetY) = PlayerPositionAt(targetState, now);
                battleTargets[targetState.PlayerEntityId] = new tPoseidon.Core.Game.Combat.MonsterBattleTarget(
                    (ushort)battleTargetX,
                    (ushort)battleTargetY,
                    targetState.PlayerDp,
                    targetState.PlayerHv,
                    targetState.PlayerLk,
                    targetStats.Level,
                    targetStats.CurrentHp,
                    targetState.IsProtected(now));
            }
            battleEvents.AddRange(monsterBattleCycle.Tick(
                mapId, zoneGeometry, monsterManager.GetByMap(mapId), battleTargets, now));

            foreach (var raisedEvent in battleEvents.ToList())
            {
                var battleEvent = raisedEvent;
                if (raisedEvent is tPoseidon.Core.Game.Combat.MonsterBattleCast monsterCast)
                {
                    if (!await MonsterCastAsync(monsterCast, sessions, mapId, now).ConfigureAwait(false)) return;
                    continue;
                }
                if (raisedEvent is tPoseidon.Core.Game.Combat.MonsterBattleSwung monsterBlow
                    && monsterManager.Get(monsterBlow.TargetEntityId) is { } struckFighter)
                {
                    if (!await MonsterSwingsAtMonsterAsync(monsterBlow, struckFighter, sessions, mapId, now).ConfigureAwait(false)) return;
                    continue;
                }
                if (raisedEvent is tPoseidon.Core.Game.Combat.MonsterBattleSwung raisedBlow)
                {
                    var (interceptedBlow, interceptSent) = await InterceptBlowAsync(raisedBlow, sessions, mapId, zoneGeometry, now, battleEvents).ConfigureAwait(false);
                    if (!interceptSent) return;
                    battleEvent = interceptedBlow;
                }
                ushort dyingPlayerId = 0;
                IReadOnlyList<(Opcode Opcode, byte[] Payload)>? deathFramesForVictim = null;
                IReadOnlyList<(Opcode Opcode, byte[] Payload)>? deathFramesForOthers = null;
                if (battleEvent is tPoseidon.Core.Game.Combat.MonsterBattleSwung blow)
                {
                    swingBlockId = commandBlockIds.Next();
                    foreach (var session in sessions)
                    {
                        var victim = session.Value;
                        if (victim.PlayerEntityId != blow.TargetEntityId || victim.Stats is not { } victimStats) continue;
                        var hpAfter = victimStats.TakeHit(blow.Damage);
                        victim.Recovery.PauseAfterDamage();
                        if (victim.Dig is not null && blow.Damage > 0)
                        {
                            if (blow.Fatal) victim.Dig = null;
                            else await drillController.InterruptAsync(victim).ConfigureAwait(false);
                        }
                        if (victim.Cast is { IsUnderWay: true } interrupted
                            && (blow.Fatal
                                || (interrupted.Skill.Has(SkillDefinition.CancelledByDamageOption)
                                    && !skillBook.GrantsAbility(victim.Skills, SkillBook.InvincibleCastingAbility))))
                        {
                            victim.Cast = null;
                            Console.WriteLine($"{LogTag} [skill] 0x{victim.PlayerEntityId:X4} skill {interrupted.Skill.Id} is interrupted by a hit from 0x{blow.EntityId:X4}");
                            if (!blow.Fatal && !await SendFromLoopAsync(
                                new[] { session }, Opcode.SKILL_CAST_CANCEL, Array.Empty<byte>(),
                                MeleeSwingFrames.NoBlock, npcLoopRng, npcLoopCt).ConfigureAwait(false))
                            {
                                return;
                            }
                        }
                        Console.WriteLine($"{LogTag} [combat] monster 0x{blow.EntityId:X4} -> 0x{blow.TargetEntityId:X4} result={blow.Result} damage={blow.Damage} playerHp={hpAfter} (DP {victim.PlayerDp}, HV {victim.PlayerHv}, LK {victim.PlayerLk})");
                        if (!blow.Fatal) continue;

                        var wasAttacking = victim.CurrentTargetEntityId != 0;
                        if (wasAttacking)
                        {
                            monsterManager.Get(victim.CurrentTargetEntityId)?.RemoveAttacker(victim.PlayerEntityId);
                            victim.CurrentTargetEntityId = 0;
                            victim.NextSwingAt = default;
                            victim.SwingsOnTarget = 0;
                        }
                        victim.ChaseEndsAt = default;
                        victim.IsDead = true;
                        victim.Engagements.TakeAll();
                        foreach (var orphaned in monsterManager.GetByMap(mapId)) orphaned.ReleaseOwner(victim.PlayerEntityId);
                        var expAfter = victimStats.Die(tPoseidon.Core.Game.PvP.PkRules.LosesExpOnDeath(victim.PvpMode));
                        dyingPlayerId = victim.PlayerEntityId;
                        deathFramesForVictim = PlayerDeathFrames.ForVictim(
                                victim.PlayerEntityId, victim.PlayerX, victim.PlayerY, victim.PlayerMoveType, wasAttacking, expAfter)
                            .Concat(victim.Quests?.SuspendHuntForDeath() ?? Array.Empty<(Opcode Opcode, byte[] Payload)>())
                            .ToList();
                        deathFramesForOthers = PlayerDeathFrames.ForOthers(
                            victim.PlayerEntityId, victim.PlayerX, victim.PlayerY, victim.PlayerMoveType, wasAttacking);
                        Console.WriteLine($"{LogTag} [combat] player 0x{victim.PlayerEntityId:X4} died to monster 0x{blow.EntityId:X4}: exp {expAfter} (lost {victimStats.DeathExpLoss})");
                        if (victim.Character is { } deadCharacter)
                        {
                            await systemLink.DiedAsync(deadCharacter.CharacterId, npcLoopCt).ConfigureAwait(false);
                        }
                    }
                }
                else if (battleEvent is tPoseidon.Core.Game.Combat.MonsterBattleLeft left)
                {
                    Console.WriteLine($"{LogTag} [combat] monster 0x{left.EntityId:X4} leaves the fight: {left.Reason}");
                }

                foreach (var session in sessions)
                {
                    var insideSwingBlock = deathFramesForVictim is null
                        ? null
                        : session.Value.PlayerEntityId == dyingPlayerId ? deathFramesForVictim : deathFramesForOthers;
                    var battleFrames = MonsterBattleFrames.Encode(battleEvent, swingBlockId, insideSwingBlock);
                    try
                    {
                        foreach (var (opcode, payload, blockId) in battleFrames)
                        {
                            var rk = (byte)npcLoopRng.Next(1, 256);
                            await session.Key.SendFrameAsync(opcode, payload, rk, blockId, npcLoopCt).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        liveSessions.TryRemove(session.Key, out _);
                    }
                    catch (System.IO.IOException)
                    {
                        liveSessions.TryRemove(session.Key, out _);
                    }
                }
            }

            foreach (var frames in mapFrames)
            {
                foreach (var session in sessions)
                {
                    try
                    {
                        foreach (var (opcode, payload) in frames)
                        {
                            var rk = (byte)npcLoopRng.Next(1, 256);
                            await session.Value.SendFrameAsync(opcode, payload, rk, npcLoopCt).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        liveSessions.TryRemove(session.Key, out _);
                    }
                    catch (System.IO.IOException)
                    {
                        liveSessions.TryRemove(session.Key, out _);
                    }
                }
            }
        }
    }
}, npcLoopCt);

await Task.WhenAll(
    primaryHost.RunAsync(stopSignal.Token),
    swingLoopTask,
    npcLoopTask,
    partyQuestTimerTask);

if (masterClient is not null)
{
    await masterClient.DisposeAsync().ConfigureAwait(false);
}

await worldHeartbeat.DisposeAsync();

internal static class PortalClick
{
    public const int Margin = 0x18;
}

internal sealed record PortalWalk(DateTimeOffset ArrivesAt, (ushort X, ushort Y) End, Func<Task> OnArrival);

internal sealed record GunOrder(ushort TargetEntityId, ushort Bullets, bool Charged, byte MoveType);

internal static class WorldPass
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);
}

internal sealed record SectorObject(ushort EntityId, int X, int Y, bool Moves, Opcode Opcode, Func<byte[]> Payload);

internal enum DigMove
{
    Began,
    Walking,
    InPlace,
    Refused,
}

internal sealed class InGameSessionState
{
    public ushort MapId { get; set; }
    public ushort PlayerEntityId { get; set; }
    public ushort PlayerX { get; set; }
    public ushort PlayerY { get; set; }
    public bool ClientReady { get; set; }
    public bool GameMasterAttribute { get; set; }
    public float GameMasterSpeed { get; set; } = 1f;
    public bool GameMasterImmortal { get; set; }
    public bool GameMasterInvisible { get; set; }
    public InstantChatSign? InstantChatSign { get; set; }
    public ushort? InstantChatState { get; set; }
    public bool IsProtected(DateTimeOffset now) => GameMasterImmortal || now < ProtectedUntil;
    public required Func<tPoseidon.Core.Common.Opcode, byte[], byte, System.Threading.CancellationToken, System.Threading.Tasks.Task> SendFrameAsync { get; init; }

    public ushort CurrentTargetEntityId { get; set; }
    public ushort PlayerAttackSpeedStat { get; set; }

    public bool AutoAttackActive { get; set; }
    public DateTimeOffset NextSwingAt { get; set; }
    public long PlayerAp { get; set; }
    public long PlayerMa { get; set; }
    public long PlayerAc { get; set; }
    public int PlayerDx { get; set; }
    public int PlayerCharacterType { get; set; }
    public int PlayerLk { get; set; }
    public int PlayerDp { get; set; }
    public int PlayerHv { get; set; }
    public int PlayerMd { get; set; }
    public ulong CouplePartnerId { get; set; }
    public bool CoupleSelecting { get; set; }
    public CoupleMaking? CoupleMaking { get; set; }
    public bool MarriageExchange { get; set; }
    public byte PvpMode { get; set; }
    public byte PkRecordMode { get; set; }
    public ushort FiestaReturnZone { get; set; }
    public tPoseidon.Core.Game.Drilling.SurpriseSpot? SurpriseSpot { get; set; }
    public int? MissionOffer { get; set; }
    public tPoseidon.Core.Game.Missions.MissionRun? Mission { get; set; }
    public ushort? HarconLobbyMap { get; set; }

    public System.Collections.Concurrent.ConcurrentQueue<int> PendingNpcSkills { get; } = new();
    public (ushort Map, ushort X, ushort Y)? MyCampReturn { get; set; }
    public tPoseidon.Core.Game.Fiesta.FiestaEntryRecord? FiestaEntry { get; set; }
    public TradeSession? Trade { get; set; }
    public GuardianLogin? Guardians { get; set; }
    public tPoseidon.Core.Game.Soul.SoulHold? Soul { get; set; }
    public HonorTitleList? HonorTitles { get; set; }
    public ProfileTitle ProfileTitle { get; set; }
    public FortuneHold? Fortune { get; set; }
    public tPoseidon.Core.Game.Characters.DisguiseState Disguise { get; } = new();
    public tPoseidon.Core.Game.SoulSeed.SoulSeedGrowth Seed { get; } = new();
    public bool SeedBusy { get; set; }
    public bool LookChangeBusy { get; set; }
    public tPoseidon.Core.Game.Items.RecycleKind? Recycle { get; set; }
    public tPoseidon.Core.Game.Items.UpgradeWindow Upgrade { get; set; }
    public bool UpgradeBusy { get; set; }
    public int HarconShopNpc { get; set; }
    public tPoseidon.Core.Game.PlayerShop.PlayerShopStall? Stall { get; set; }
    public ushort? VisitingKeeper { get; set; }
    public bool ShopSearching { get; set; }
    public tPoseidon.Core.Game.PlayerShop.PlayerShopSearchResult? ShopSearch { get; set; }
    public bool CardIdentify { get; set; }
    public tPoseidon.Core.Game.Cards.CardIdentifyGame? CardGame { get; set; }
    public ItemParamEntry? PendingMonsterCard { get; set; }
    public bool GuardianBusy { get; set; }
    public GunOrder? GunShot { get; set; }
    public Func<tPoseidon.Core.Net.Dialogs.ZoneChangeRequest, CancellationToken, Task>? RequestZoneChange { get; set; }
    public tPoseidon.Core.Game.Stats.StatManager? Stats { get; set; }
    public bool IsDead { get; set; }
    public long PlayerMaxHp { get; set; }
    public tPoseidon.Core.Game.Combat.EngagementList Engagements { get; } = new();
    public NaturalRecovery Recovery { get; } = new();

    public Func<Task>? RefreshPartyBonus { get; set; }
    public tPoseidon.Core.Game.Items.TimedItemWatch TimedItems { get; } = new();
    public tPoseidon.Core.Game.Pets.OwnerWalk? Walk { get; set; }
    public (int Column, int Row)? SeenSector { get; set; }
    public Func<ulong, Task>? ReleaseExpiredItem { get; set; }
    public RecoveryAmounts RecoveryAmounts { get; set; }
    public int RecoveryType { get; set; }
    public DateTimeOffset RecoveryTickedAt { get; set; }
    public Func<long, IReadOnlyList<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>>? GainHitExp { get; set; }
    public Func<long, IReadOnlyList<(Opcode Opcode, byte[] Payload)>>? GainKillTmExp { get; set; }
    public long PlayerMaxMp { get; set; }
    public Character? Character { get; set; }
    public DateTimeOffset ResurrectionCompletesAt { get; set; }
    public DateTimeOffset ProtectedUntil { get; set; }
    public byte PlayerMoveType { get; set; }
    public byte AnnouncedSpeedQuarter { get; set; } = EntitySpeedPacket.RunSpeedQuarter;
    public int SwingsOnTarget { get; set; }
    public bool FollowSettled { get; set; }
    public bool SettlesOnArrival { get; set; }
    public byte AttackMoveType { get; set; }
    public ushort ChaseToX { get; set; }
    public ushort ChaseToY { get; set; }
    public DateTimeOffset ChaseEndsAt { get; set; }
    public DateTimeOffset LastChaseOrderAt { get; set; }
    public bool AttackStopPending { get; set; }
    public int PlayerMaxWt { get; set; }
    public bool SeesHiddenItems { get; set; }
    public ushort PickupItemEntityId { get; set; }
    public DateTimeOffset PickupAt { get; set; }
    public (ushort X, ushort Y)? PickupWalkEnd { get; set; }
    public IReadOnlyDictionary<int, int> Skills { get; set; } = new Dictionary<int, int>();
    public SkillCast? Cast { get; set; }
    public SkillCooldowns Cooldowns { get; } = new();
    public DateTimeOffset CooldownsTickedAt { get; set; }
    public Func<long, IReadOnlyList<(Opcode Opcode, byte[] Payload)>>? GainSkillTmExp { get; set; }
    public Func<long, IReadOnlyList<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>>? GainExpTotal { get; set; }
    public Func<long, IReadOnlyList<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>>? GainRewardExp { get; set; }
    public Func<long, IReadOnlyList<(Opcode Opcode, byte[] Payload)>>? GainRewardTmExp { get; set; }
    public Action? RecomputeStats { get; set; }
    public int PlayerDa { get; set; }
    public DrillDig? Dig { get; set; }
    public bool AutoDrill { get; set; }
    public DateTimeOffset? AutoDrillCoolTimeAt { get; set; }
    public MailState Mail { get; } = new();
    public CashBoxState CashBox { get; } = new();
    public IReadOnlySet<int> ActiveSets { get; set; } = new HashSet<int>();
    public PortalWalk? PortalWalk { get; set; }
    public (int X, int Y)? LastDigCell { get; set; }
    public NpcScriptExecutor? Quests { get; set; }
    public Func<tPoseidon.Core.Game.Scripting.Lang.EventKind, long?, string?, Task>? RunScriptEvent { get; set; }
    public DateTimeOffset HuntTickedAt { get; set; }
    public tPoseidon.Core.Game.Pets.PetActor? Pet { get; set; }
    public tPoseidon.Core.Game.Skills.Effects.SkillEffectBook Effects { get; } = new(tPoseidon.Core.Game.Skills.Effects.SkillEffectBook.PlayerCapacities);
    public tPoseidon.Core.Game.Characters.EffectiveStats CombatBase { get; set; }
    public tPoseidon.Core.Game.Skills.CrazyHammerGauge CrazyGauge { get; } = new();
    public Dictionary<byte, short> ResistBonus { get; } = new();
    public (int SkillId, int Level, ulong ItemUid)? ScrollCast { get; set; }
    public DateTimeOffset EffectsTickedAt { get; set; }
}
