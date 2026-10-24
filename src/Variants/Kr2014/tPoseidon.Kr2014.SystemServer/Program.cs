using tPoseidon.Core.Common;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

ConsoleFileLog.EnableIfConfigured();
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : ServerEndpointConfig.SystemPort;
const string LogTag = "[sserver1]";
void Log(string message) => Console.WriteLine($"{LogTag} {message}");

Log("SystemServer (Kr2014 variant; social subsystems: Friend/Couple/Party/Gift/GM) — mesh registration only in this build");

var systemIni = ServerIniLocator.TryLoad(
    ServerIniLocator.ServerKind.System,
    ServerIniLocator.Variant.Kr2014,
    1,
    Log);
if (systemIni is not null)
{
    var iniMaxClient = systemIni.GetInt("MaxClient", -1);
    var iniLingualId = systemIni.GetInt("LingualID", -1);
    var iniMasterId = systemIni.GetString("MasterServerID") ?? "?";
    var iniFriendServer = systemIni.GetBool("FriendServer", false);
    var iniUseRank = systemIni.GetBool("UseRankSystem", false);
    Log($"SystemServer.ini knobs: MasterServerID={iniMasterId}, MaxClient={iniMaxClient}, LingualID={iniLingualId}, FriendServer={iniFriendServer}, UseRankSystem={iniUseRank}");
}

using var stopCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stopCts.Cancel();
};

var postgres = await tPoseidon.Core.Data.Postgres.PostgresStoreBootstrap.InitializeAsync().ConfigureAwait(false);
var accountStore = postgres.Store;
var sessionLogins = new tPoseidon.Core.Net.S2.LoginSessionHandler(accountStore);
var socialRoom = new tPoseidon.Kr2014.SystemServer.SocialRoom();
var friendServer = new tPoseidon.Kr2014.SystemServer.FriendServer(
    new tPoseidon.Core.Data.Postgres.PostgresFriendStore(postgres.DataSource),
    socialRoom,
    System.Text.Encoding.GetEncoding(949),
    systemIni?.GetInt("FriendOfflineSMSLimitCount", tPoseidon.Core.Data.FriendRules.OfflineMessageLimit) ?? tPoseidon.Core.Data.FriendRules.OfflineMessageLimit,
    Log);
var partyRules = tPoseidon.Core.Net.Social.PartyRules.FromIni(
    systemIni?.GetInt("PartyLimitLevel", tPoseidon.Core.Net.Social.PartyRules.DefaultLimitLevel) ?? tPoseidon.Core.Net.Social.PartyRules.DefaultLimitLevel,
    systemIni?.GetInt("PartyMemberDepotInterval", tPoseidon.Core.Net.Social.PartyRules.DefaultDepotInterval) ?? tPoseidon.Core.Net.Social.PartyRules.DefaultDepotInterval);
var reportServer = new tPoseidon.Kr2014.SystemServer.ReportServer(
    new tPoseidon.Core.Data.Postgres.PostgresReportStore(postgres.DataSource),
    socialRoom,
    System.Text.Encoding.GetEncoding(949),
    (byte)ServerIdentity.System1.ServerIndex,
    Log);
var gameLinks = new tPoseidon.Kr2014.SystemServer.GameServerLinks(tPoseidon.Core.Net.S2.S2ServerHost.DefaultKeyTablePath(), Log);
var instantChatRooms = new tPoseidon.Core.Net.Social.InstantChatRooms(
    System.Text.Encoding.GetEncoding(949),
    (ushort)(systemIni?.GetInt("ChatNoticeMakeLimitLevel", tPoseidon.Core.Net.Social.InstantChatRooms.DefaultMakeLimitLevel) ?? tPoseidon.Core.Net.Social.InstantChatRooms.DefaultMakeLimitLevel),
    (ushort)(systemIni?.GetInt("ChatNoticeJoinLimitLevel", tPoseidon.Core.Net.Social.InstantChatRooms.DefaultJoinLimitLevel) ?? tPoseidon.Core.Net.Social.InstantChatRooms.DefaultJoinLimitLevel));
var chatServer = new tPoseidon.Kr2014.SystemServer.ChatServer(socialRoom, instantChatRooms, gameLinks, System.Text.Encoding.GetEncoding(949), Log);
var partyServer = new tPoseidon.Kr2014.SystemServer.PartyServer(
    new tPoseidon.Core.Net.Social.PartyService(System.Text.Encoding.GetEncoding(949), partyRules, TimeProvider.System),
    socialRoom,
    gameLinks,
    System.Text.Encoding.GetEncoding(949),
    Log);
var guildServer = new tPoseidon.Kr2014.SystemServer.GuildServer(
    new tPoseidon.Core.Net.Social.GuildService(
        new tPoseidon.Core.Data.Postgres.PostgresGuildRulesStore(postgres.DataSource),
        System.Text.Encoding.GetEncoding(949),
        TimeProvider.System),
    new tPoseidon.Core.Data.Postgres.PostgresGuildMatchStore(postgres.DataSource),
    TimeProvider.System,
    socialRoom,
    gameLinks,
    System.Text.Encoding.GetEncoding(949),
    Log,
    (systemIni?.GetInt("GuildGameTestMode", 0) ?? 0) != 0);

var variantDataRoot = Environment.GetEnvironmentVariable("MT_VARIANT_DATA_ROOT")
    ?? Path.GetFullPath(RepoRoot.Data("variants", "Kr2014"));
var libConfigPath = new[]
{
    Path.Combine(variantDataRoot, "libconfig.dat"),
    Path.Combine(variantDataRoot, "libconfig_eng.dat"),
    RepoRoot.Data("libconfig_kr_en.xml"),
}.FirstOrDefault(File.Exists);
if (libConfigPath is not null)
{
    guildServer.RoomCaptions = tPoseidon.Core.Net.Social.GuildRoomCaptions.From(
        new tPoseidon.Core.LibConfig.LibConfigXmlReader(libConfigPath).LoadNameValueTable(tPoseidon.Core.Net.Social.GuildRoomCaptions.TextTable));
}
var coupleTables = libConfigPath is null
    ? tPoseidon.Core.LibConfig.CoupleTables.Empty
    : new tPoseidon.Core.LibConfig.LibConfigXmlReader(libConfigPath).LoadCoupleTables();
Log($"couple tables from libconfig: {coupleTables.DeleteQuestIds.Count} quests and {coupleTables.DeleteSkillIds.Count} skills removed at break-up (CoupleDeleteInfo)");
var coupleService = new CoupleService(
    new tPoseidon.Core.Data.Postgres.PostgresCoupleRulesStore(postgres.DataSource),
    coupleTables.DeleteQuestIds,
    coupleTables.DeleteSkillIds,
    coupleTables.EventPoints,
    System.Text.Encoding.GetEncoding(949),
    TimeProvider.System);
friendServer.MemoSent = (sender, target) => CoupleNote(coupleService.Memo(sender, target));
chatServer.Whispered = (sender, target) => CoupleNote(coupleService.Whisper(sender, target));
partyServer.PartyChanged = members => { foreach (var note in coupleService.PartyChanged(members)) CoupleNote(note); };
void CoupleNote(string? note)
{
    if (note is not null) Log($"[couple] {note}");
}
var weddingHalls = new WeddingHallService(
    new tPoseidon.Core.Data.Postgres.PostgresCoupleRulesStore(postgres.DataSource),
    coupleService.IsOpen,
    coupleService.Refresh,
    System.Text.Encoding.GetEncoding(949),
    TimeProvider.System,
    systemIni?.GetBool("IsWeddingTest", false) ?? false);
var zoneTypes = new Dictionary<int, int>();
if (libConfigPath is not null)
{
    foreach (var mapKind in new tPoseidon.Core.LibConfig.LibConfigXmlReader(libConfigPath).LoadMapKinds()) zoneTypes[mapKind.Id] = mapKind.Type;
}
var coupleServer = new tPoseidon.Kr2014.SystemServer.CoupleServer(
    coupleService,
    weddingHalls,
    new tPoseidon.Core.Data.Postgres.PostgresWeddingBoardStore(postgres.DataSource),
    socialRoom,
    gameLinks,
    zone => zoneTypes.TryGetValue(zone, out var zoneType) ? zoneType : null,
    System.Text.Encoding.GetEncoding(949),
    Log);

var giftServer = new tPoseidon.Kr2014.SystemServer.GiftServer(
    new tPoseidon.Core.Data.Postgres.PostgresGiftStore(postgres.DataSource),
    socialRoom,
    System.Text.Encoding.GetEncoding(949),
    Log)
{
    GiftAccepted = coupleServer.GiftAccepted,
};

var angelServer = new tPoseidon.Kr2014.SystemServer.AngelServer(
    new AngelService(
        new tPoseidon.Core.Data.Postgres.PostgresAngelStore(postgres.DataSource),
        System.Text.Encoding.GetEncoding(949),
        TimeProvider.System),
    socialRoom,
    gameLinks,
    System.Text.Encoding.GetEncoding(949),
    Log);

var cardBattleItems = new tPoseidon.Core.Data.Postgres.PostgresItemStore(postgres.DataSource);
var cardBattleServer = new tPoseidon.Kr2014.SystemServer.CardBattleServer(
    new CardBattleService(
        new tPoseidon.Core.Data.Postgres.PostgresCardBattleStore(postgres.DataSource),
        name => socialRoom.FindNamed(name) is { } named ? new CardBattlePlayer(named.Character.CharacterId, named.Character.Name, named.Character.Level) : null,
        (characterId, card) => cardBattleItems.GetForCharacterBySlotKind(characterId, tPoseidon.Core.Data.SlotKind.Inventory)
            .Any(item => item.ItemUid == tPoseidon.Core.Net.World.BagItemIds.Stored(card.ItemUid) && item.ItemIndex == card.ItemId),
        Random.Shared.Next,
        System.Text.Encoding.GetEncoding(949),
        libConfigPath is null
            ? null
            : new tPoseidon.Core.Game.CardBattle.SpecialCardSets(new tPoseidon.Core.LibConfig.LibConfigXmlReader(libConfigPath).LoadCardSpecialSets())),
    socialRoom,
    gameLinks,
    System.Text.Encoding.GetEncoding(949),
    Log);

var rankingStore = new tPoseidon.Core.Data.Postgres.PostgresRankingStore(postgres.DataSource);
var rankServer = new tPoseidon.Kr2014.SystemServer.RankServer(
    new RankService(rankingStore, System.Text.Encoding.GetEncoding(949)),
    rankingStore,
    socialRoom,
    Log);

var gameMasterReader = libConfigPath is null ? null : new tPoseidon.Core.LibConfig.LibConfigXmlReader(libConfigPath);
var gameMasterChat = new GameMasterChat(
    gameMasterReader?.LoadNameStringTable(GameMasterChat.ResultTable) ?? new Dictionary<string, string>(),
    gameMasterReader?.LoadNameStringTable(GameMasterChat.HelpTable) ?? new Dictionary<string, string>());
MasterServerClient? masterClient = null;
var gameMasterService = new GameMasterService(postgres.GmAuthority, System.Text.Encoding.GetEncoding(949))
    {
        Friends = new tPoseidon.Core.Data.Postgres.PostgresFriendStore(postgres.DataSource),
        Guilds = new tPoseidon.Core.Data.Postgres.PostgresGuildStore(postgres.DataSource),
        GuildRules = new tPoseidon.Core.Data.Postgres.PostgresGuildRulesStore(postgres.DataSource),
        BlockRecords = new MasterRoutedGmBlockStore(
            () => masterClient,
            new tPoseidon.Core.Data.Postgres.PostgresGmBlockStore(postgres.DataSource),
            ServerIdentity.System1.ServerIndex,
            Log),
    };
Log($"[gm] {gameMasterService.RestoreBlocks()} active block(s) loaded from the database");
var gameMasterServer = new tPoseidon.Kr2014.SystemServer.GameMasterServer(
    gameMasterService,
    gameMasterChat,
    postgres.GmAuthority,
    socialRoom,
    gameLinks,
    new tPoseidon.Core.Data.Postgres.PostgresGmRateEventStore(postgres.DataSource),
    System.Text.Encoding.GetEncoding(949),
    systemIni?.GetBool("GameMasterToolLoopbackOnly", true) ?? true,
    Log);
gameMasterServer.GuildDissolver = guildServer.ForceDissolveAsync;
gameMasterServer.GuildRooms = guildServer.GameRooms;

var clientHost = new tPoseidon.Core.Net.S2.S2ServerHost
{
    LogTag = LogTag,
    Port = port,
    KeyTablePath = tPoseidon.Core.Net.S2.S2ServerHost.DefaultKeyTablePath(),
    SessionsLogIn = true,
    OnSessionAccepted = (ctx, _) =>
    {
        ctx.Dispatcher.Register(Opcode.SESSION_INIT, async (_, payload, loginCt) =>
        {
            var loginGate = ctx.Guard.CheckLogin(null);
            if (loginGate.LockedOut)
            {
                Log($"{ctx.Remote} login refused: too many failed logins from this address");
                await Task.Delay(loginGate.Delay, loginCt).ConfigureAwait(false);
                await ctx.Session.DisposeAsync().ConfigureAwait(false);
                return;
            }
            var login = sessionLogins.Handle(payload.Span);
            if (!login.Accepted)
            {
                Log($"{ctx.Remote} login refused ({login.RejectReason}): {login.Message}");
                await Task.Delay(ctx.Guard.LoginFailed(null), loginCt).ConfigureAwait(false);
                await ctx.Session.DisposeAsync().ConfigureAwait(false);
                return;
            }
            ctx.Guard.LoginSucceeded(null);
            var socialMember = new tPoseidon.Kr2014.SystemServer.SocialMember(ctx, login.Character!);
            if (socialRoom.Enter(socialMember) is { } displaced)
            {
                Log($"{displaced.Context.Remote} replaced by a new session for character 0x{login.Character!.CharacterId:X}");
                await displaced.Context.Session.DisposeAsync().ConfigureAwait(false);
            }
            await guildServer.LoggedInAsync(socialMember, loginCt).ConfigureAwait(false);
            await ctx.SendFrameAsync(
                Opcode.SESSION_OPEN, tPoseidon.Kr2014.Wire.SessionOpenPacket.EncodePayload(),
                (byte)Random.Shared.Next(1, 256), loginCt).ConfigureAwait(false);
            await partyServer.LoggedInAsync(socialMember, loginCt).ConfigureAwait(false);
            await angelServer.LoggedInAsync(socialMember, loginCt).ConfigureAwait(false);
            Log($"{ctx.Remote} {login.Message}: character 0x{login.Character!.CharacterId:X} ('{login.Character.Name}') of account {login.AccountId} is in");
        });
        ctx.Dispatcher.Register(Opcode.SESSION_PARAM_3, (_, _, _) => Task.CompletedTask);
        friendServer.Register(ctx.Dispatcher, ctx);
        chatServer.Register(ctx.Dispatcher, ctx);
        reportServer.Register(ctx.Dispatcher, ctx);
        partyServer.Register(ctx.Dispatcher, ctx);
        guildServer.Register(ctx.Dispatcher, ctx);
        coupleServer.Register(ctx.Dispatcher, ctx);
        giftServer.Register(ctx.Dispatcher, ctx);
        angelServer.Register(ctx.Dispatcher, ctx);
        rankServer.Register(ctx.Dispatcher, ctx);
        cardBattleServer.Register(ctx.Dispatcher, ctx);
        gameMasterServer.Register(ctx.Dispatcher, ctx);
        return Task.CompletedTask;
    },
    OnSessionClosed = async ctx =>
    {
        gameMasterServer.Closed(ctx);
        if (socialRoom.Leave(ctx) is { } member)
        {
            await friendServer.ClosedAsync(member, stopCts.Token).ConfigureAwait(false);
            await partyServer.ClosedAsync(member, stopCts.Token).ConfigureAwait(false);
            await guildServer.ClosedAsync(member, stopCts.Token).ConfigureAwait(false);
            coupleServer.Closed(member);
            angelServer.Closed(member);
            gameMasterServer.Closed(member);
            await cardBattleServer.ClosedAsync(member, stopCts.Token).ConfigureAwait(false);
            await chatServer.ClosedAsync(member, stopCts.Token).ConfigureAwait(false);
        }
    },
};
var clientHostTask = clientHost.RunAsync(stopCts.Token);
gameLinks.OnEnterZone = async (characterId, zone, ct) =>
{
    await partyServer.ZoneEnteredAsync(characterId, zone, ct).ConfigureAwait(false);
    await guildServer.ZoneEnteredAsync(characterId, ct).ConfigureAwait(false);
    await coupleServer.ZoneEnteredAsync(characterId, zone, ct).ConfigureAwait(false);
    await gameMasterServer.ZoneEnteredAsync(characterId, ct).ConfigureAwait(false);
};
gameLinks.OnLeaveZone = partyServer.ZoneLeftAsync;
gameLinks.OnBeginPartyQuest = partyServer.BeginQuest;
gameLinks.OnPartyQuestKill = partyServer.QuestKillAsync;
gameLinks.OnCancelPartyQuest = partyServer.CancelQuest;
gameLinks.OnDeadChanged = partyServer.DeadChangedAsync;
gameLinks.OnKindChanged = async (characterId, kind, job, ct) =>
{
    if (socialRoom.Find(characterId) is { } changed) changed.CharacterType = kind;
    await partyServer.KindChangedAsync(characterId, kind, job, ct).ConfigureAwait(false);
};
gameLinks.OnCoupleMessage = coupleServer.LinkMessageAsync;
gameLinks.OnReport = async (report, ct) =>
{
    await angelServer.ReportAsync(report, ct).ConfigureAwait(false);
    await cardBattleServer.ReportAsync(report, ct).ConfigureAwait(false);
    await coupleServer.ReportAsync(report, ct).ConfigureAwait(false);
    await gameMasterServer.ReportAsync(report, ct).ConfigureAwait(false);
    await chatServer.ReportAsync(report, ct).ConfigureAwait(false);
};
gameLinks.OnLevelChanged = angelServer.LevelChangedAsync;
gameLinks.OnGuildMatchResult = guildServer.MatchResultAsync;
gameLinks.OnGuildRoomInvalid = guildServer.RoomInvalidAsync;
gameLinks.OnGameMasterChat = gameMasterServer.ChatAsync;
gameMasterServer.InstantChatRemover = chatServer.RemoveRoomOf;
gameLinks.OnLinked = gameMasterServer.GameServerLinkedAsync;
var gameLinkTasks = new[] { ServerEndpointConfig.GamePort, ServerEndpointConfig.GameAltPort }
    .Select(gamePort => gameLinks.RunAsync(ServerEndpointConfig.MasterHost, gamePort, stopCts.Token))
    .ToArray();
var rankJobTask = rankServer.RunJobAsync(stopCts.Token);
var partyExpiryTask = partyServer.ExpireOfflineMembersAsync(partyRules.OfflineMemberKeepTime / 10, stopCts.Token);
var gameMasterRateTask = gameMasterServer.RunRateEventsAsync(stopCts.Token);
Log($"listening on {ServerEndpointConfig.ClientBindAddress}:{port}");

try
{
    var meshPassword = SiblingIdParser.Parse(systemIni?.GetString("MasterServerID") ?? string.Empty).Password;
    masterClient = await MasterServerRegistration.ConnectAndRegisterAsync(
        ServerEndpointConfig.MasterHost,
        ServerEndpointConfig.MasterPort,
        ServerIdentity.System1,
        Log,
        stopCts.Token,
        onBrokeredFrame: null,
        password: meshPassword).ConfigureAwait(false);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Console.Error.WriteLine($"{LogTag} MasterServer registration failed: {ex.Message} — continuing without mesh");
}

try
{
    await clientHostTask.ConfigureAwait(false);
    await partyExpiryTask.ConfigureAwait(false);
    await gameMasterRateTask.ConfigureAwait(false);
    await rankJobTask.ConfigureAwait(false);
    await Task.WhenAll(gameLinkTasks).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
}
finally
{
    if (masterClient is not null)
    {
        await masterClient.DisposeAsync().ConfigureAwait(false);
    }
}

Log("shutdown");
