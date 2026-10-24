using System.Net;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.InterServer;
using tPoseidon.Kr2014.MasterServer.Dispatcher;
using tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

ConsoleFileLog.EnableIfConfigured();
var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : ServerEndpointConfig.MasterPort;
const string LogTag = "[master]";

void Log(string message) => Console.WriteLine($"{LogTag} {message}");

var bootTime = DateTimeOffset.UtcNow;

ZoneRoutingTable? zoneTable = null;
IniFile? masterIni = null;
{
    var iniPath = ServerIniLocator.Resolve(ServerIniLocator.ServerKind.Master, ServerIniLocator.Variant.Kr2014);
    var jsonPath = RepoRoot.Data("master-zone-routing.json");
    if (File.Exists(iniPath))
    {
        try
        {
            masterIni = IniFile.LoadFromFile(iniPath);
            zoneTable = ZoneRoutingTable.LoadFromIni(masterIni);
            Log($"MasterServer.ini loaded from {iniPath}: {zoneTable.Count} zone routes parsed");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log($"MasterServer.ini load FAILED ({ex.GetType().Name}: {ex.Message}); falling through to JSON");
            masterIni = null;
            zoneTable = null;
        }
    }
    if (zoneTable is null && File.Exists(jsonPath))
    {
        try
        {
            zoneTable = ZoneRoutingTable.LoadFromFile(jsonPath);
            Log($"zone-routing JSON loaded from {jsonPath}: {zoneTable.Count} explicit zones, default=idx{zoneTable.DefaultServerIndex}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log($"zone-routing JSON load FAILED ({ex.GetType().Name}: {ex.Message}); falling back to legacy odd/even stub");
        }
    }
    if (zoneTable is null)
    {
        Log("no zone-routing config found; using legacy odd/even stub (gserver1=odd, gserver2=even)");
    }
}

var expectedSiblingPasswords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var uniqueItemControl = false;
if (masterIni is not null)
{
    var maxClient = masterIni.GetInt("MaxClient", -1);
    var megaphoneDelay = masterIni.GetInt("MegaphoneDelay", -1);
    var controlUniqueItem = masterIni.GetBool("bControlUniqueItem", false);
    uniqueItemControl = controlUniqueItem;
    var userCountDelay = masterIni.GetInt("UserCountDelay", -1);
    var worldSection = ZoneRoutingTable.FindWorldSection(masterIni, 0);
    var siblingIds = worldSection?.GetAll("ID") ?? Array.Empty<string>();
    Log($"MasterServer.ini knobs: MaxClient={maxClient}, MegaphoneDelay={megaphoneDelay}ms, UserCountDelay={userCountDelay}ms, UniqueItemControl={(controlUniqueItem ? "on" : "off")}, siblings={siblingIds.Count}");
    foreach (var sibling in siblingIds)
    {
        Log($"  registered sibling: {sibling}");
        var colon = sibling.IndexOf(':');
        if (colon > 0)
        {
            var name = sibling.Substring(0, colon).Trim();
            var pw = sibling.Substring(colon + 1).Trim();
            expectedSiblingPasswords[name] = pw;
        }
    }
    Log($"inter-server password registry: {expectedSiblingPasswords.Count} sibling(s) loaded; mesh-login enforcement {(expectedSiblingPasswords.Count > 0 ? "ON" : "OFF")}");
}

ServerIdentity ResolveGameServer(ushort zoneId) =>
    zoneTable is not null
        ? zoneTable.ResolveGameServer(zoneId)
        : ZoneRouter.ResolveGameServer(zoneId);

IGuideHistoryStore guideHistoryStore = new InMemoryGuideHistoryStore();
IQuestStore questStore = new InMemoryQuestStore();
IQuestHistoryStore questHistoryStore = new InMemoryQuestHistoryStore();
IItemStore itemStore = new InMemoryItemStore();
ICharStore charStore = new InMemoryCharStore();
ISkillStore skillStore = new InMemorySkillStore();
IPartyQuestStore? partyQuestStore = null;
IOccupationStore? occupationStore = null;
IPostStore? postStore = null;
IChargedItemStore? chargedItemStore = null;
ITradeStore? tradeStore = null;
IGuardianStateStore? guardianStore = null;
IHonorTitleStore? honorTitleStore = null;
IMemoryPortStore? memoryPortStore = null;
IFortuneStore? fortuneStore = null;
IGmBlockStore? gmBlockStore = null;
ISoulSeedStateStore? soulSeedStore = null;
IEquipStatusUpgradeStore? equipUpgradeStore = null;
IItemRechargeStore? itemRechargeStore = null;
IUniqueItemStore? uniqueItemStore = null;
ISoulStore? soulStore = null;
try
{
    var pg = await PostgresStoreBootstrap.InitializeAsync().ConfigureAwait(false);
    guideHistoryStore = new PostgresGuideHistoryStore(pg.DataSource);
    questStore = new PostgresQuestStore(pg.DataSource);
    questHistoryStore = new PostgresQuestHistoryStore(pg.DataSource);
    itemStore = new PostgresItemStore(pg.DataSource);
    charStore = new PostgresCharStore(pg.DataSource);
    skillStore = new PostgresSkillStore(pg.DataSource);
    partyQuestStore = new PostgresPartyQuestStore(pg.DataSource);
    occupationStore = new PostgresOccupationStore(pg.DataSource);
    postStore = new PostgresPostStore(pg.DataSource);
    chargedItemStore = new PostgresChargedItemStore(pg.DataSource);
    tradeStore = new PostgresTradeStore(pg.DataSource);
    guardianStore = new PostgresGuardianStateStore(pg.DataSource);
    honorTitleStore = new PostgresHonorTitleStore(pg.DataSource);
    memoryPortStore = new PostgresMemoryPortStore(pg.DataSource);
    fortuneStore = new PostgresFortuneStore(pg.DataSource);
    gmBlockStore = new PostgresGmBlockStore(pg.DataSource);
    soulSeedStore = new PostgresSoulSeedStateStore(pg.DataSource);
    equipUpgradeStore = new PostgresEquipStatusUpgradeStore(pg.DataSource);
    itemRechargeStore = new PostgresItemRechargeStore(pg.DataSource);
    uniqueItemStore = new PostgresUniqueItemStore(pg.DataSource);
    soulStore = new PostgresSoulStore(pg.DataSource);
    Log($"postgres bootstrap OK; cXxxStore family backed by Postgres impls");
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log($"postgres bootstrap FAILED ({ex.GetType().Name}: {ex.Message}); cXxxStore family falling back to in-memory impls");
}

await using var host = new MasterServerHost(new IPEndPoint(ServerEndpointConfig.InterServerBindAddress, port), Log);

var masterContext = new MasterContext(host, ResolveGameServer, bootTime, Log, expectedSiblingPasswords, zoneTable);
var dispatcher = new InterServerDispatcher(masterContext)
    .Register(InterServerOpcode.McmdSvrLogin, new SvrLoginHandler())
    .Register(InterServerOpcode.McmdPrepareEnteringZone, new PrepareEnteringZoneHandler())
    .Register(
        new[] { InterServerOpcode.McmdPrepareEnteringZoneAckOk, InterServerOpcode.McmdPrepareEnteringZoneAckCancel },
        new PrepareEnteringZoneAckRelayHandler())
    .Register(InterServerOpcode.McmdLeaveZoneAckOk, new LeaveZoneAckHandler())
    .Register(ZoneUserCountHandler.Requests, new ZoneUserCountHandler())
    .Register(InterServerOpcode.EnterZoneDoneAck, new EnterZoneDoneAckHandler())
    .Register(
        new[] { InterServerOpcode.TpGuideHistoryLoad, InterServerOpcode.TpGuideHistoryUpsertSystem, InterServerOpcode.TpGuideHistoryUpsertTutorial },
        new GuideHistoryHandler(guideHistoryStore))
    .Register(InterServerOpcode.TpQuestStoreCall, new QuestStoreHandler(questStore, questHistoryStore))
    .Register(InterServerOpcode.TpItemStoreCall, new ItemStoreHandler(itemStore))
    .Register(InterServerOpcode.TpCharStoreCall, new CharStoreHandler(charStore));
if (partyQuestStore is not null)
{
    dispatcher.Register(PartyQuestHandler.Requests, new PartyQuestHandler(partyQuestStore));
}
if (postStore is not null)
{
    dispatcher.Register(PostHandler.Requests, new PostHandler(postStore));
}
if (chargedItemStore is not null)
{
    dispatcher.Register(CashBoxHandler.Requests, new CashBoxHandler(chargedItemStore));
}
if (tradeStore is not null)
{
    dispatcher.Register(TradeHandler.Requests, new TradeHandler(tradeStore));
}
if (guardianStore is not null)
{
    dispatcher.Register(GuardianHandler.Requests, new GuardianHandler(guardianStore));
}
if (honorTitleStore is not null)
{
    dispatcher.Register(HonorTitleHandler.Requests, new HonorTitleHandler(honorTitleStore));
}
if (memoryPortStore is not null)
{
    dispatcher.Register(MemoryPortHandler.Requests, new MemoryPortHandler(memoryPortStore));
}
if (fortuneStore is not null)
{
    dispatcher.Register(FortuneHandler.Requests, new FortuneHandler(fortuneStore));
}
if (gmBlockStore is not null)
{
    dispatcher.Register(GmBlockHandler.Requests, new GmBlockHandler(gmBlockStore));
}
if (soulSeedStore is not null && guardianStore is not null)
{
    dispatcher.Register(SoulSeedHandler.Requests, new SoulSeedHandler(soulSeedStore, guardianStore));
}
if (equipUpgradeStore is not null)
{
    dispatcher.Register(EquipStatusUpgradeHandler.Requests, new EquipStatusUpgradeHandler(equipUpgradeStore));
}
if (itemRechargeStore is not null)
{
    dispatcher.Register(ItemRechargeHandler.Requests, new ItemRechargeHandler(itemRechargeStore));
}
if (guardianStore is not null && equipUpgradeStore is not null)
{
    dispatcher.Register(GuardianRenameHandler.Requests, new GuardianRenameHandler(guardianStore, equipUpgradeStore));
    dispatcher.Register(GuardianCardsHandler.Requests, new GuardianCardsHandler(guardianStore, equipUpgradeStore));
}
if (uniqueItemStore is not null)
{
    dispatcher.Register(UniqueItemHandler.Requests, new UniqueItemHandler(uniqueItemStore));
}
if (soulStore is not null)
{
    dispatcher.Register(SoulHandler.Requests, new SoulHandler(soulStore));
}
if (occupationStore is not null)
{
    dispatcher.Register(new[] { InterServerOpcode.CcmdChangeOccupation, InterServerOpcode.TpJobQuestState, InterServerOpcode.CcmdUpdateOccupationIndex }, new ChangeOccupationHandler(occupationStore));
}

host.OnFrame = dispatcher.HandleAsync;

host.Start();
var routingDescription = zoneTable is not null
    ? $"data-driven ({zoneTable.Count} explicit zones, default=idx{zoneTable.DefaultServerIndex})"
    : "legacy odd/even stub (gserver1=odd, gserver2=even)";
Log($"MasterServer broker up on {ServerEndpointConfig.InterServerBindAddress}:{port}; zone routing: {routingDescription}");

using var uniqueDutyStop = new CancellationTokenSource();
Task? uniqueDutyTask = null;
if (uniqueItemStore is not null && uniqueItemControl)
{
    var uniqueDuty = new tPoseidon.Kr2014.MasterServer.UniqueItemDutyLoop(uniqueItemStore, async (charUid, end, now) =>
    {
        var expireBody = UniqueItemMessages.EncodeDutyExpire(charUid, end, now);
        foreach (var sibling in host.Siblings.Where(sibling => sibling.RegisteredServerIndex is not null).ToArray())
        {
            await sibling.SendAsync(new InterServerFrame
            {
                Opcode = InterServerOpcode.CcmdUniqueDutyExpire,
                Header = new InterServerHeader
                {
                    SourceServerIndex = 0,
                    DestServerIndex = sibling.RegisteredServerIndex!.Value,
                    CharUid = charUid,
                    RequestId = 0,
                },
                Body = expireBody,
            }, CancellationToken.None).ConfigureAwait(false);
        }
    }, Log);
    try
    {
        await uniqueDuty.StartAsync().ConfigureAwait(false);
        uniqueDutyTask = uniqueDuty.RunAsync(uniqueDutyStop.Token);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Log($"unique item duty start FAILED ({ex.GetType().Name}: {ex.Message}); duty loop off");
    }
}
else
{
    Log($"unique item duty loop off (bControlUniqueItem {(uniqueItemControl ? "on" : "off")}, store {(uniqueItemStore is null ? "missing" : "ready")})");
}

var stop = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.TrySetResult();
};
await stop.Task.ConfigureAwait(false);
Log("shutdown requested; closing broker");
await uniqueDutyStop.CancelAsync().ConfigureAwait(false);
if (uniqueDutyTask is not null)
{
    try
    {
        await uniqueDutyTask.ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        Log("unique item duty loop stopped");
    }
}
