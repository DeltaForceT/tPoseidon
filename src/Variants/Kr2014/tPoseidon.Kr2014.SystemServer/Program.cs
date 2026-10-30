using tPoseidon.Core.Common;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

ConsoleFileLog.EnableIfConfigured();
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
var friendStore = new tPoseidon.Core.Data.Postgres.PostgresFriendStore(postgres.DataSource);
var friendServer = new tPoseidon.Kr2014.SystemServer.FriendServer(
    friendStore,
    socialRoom,
    tPoseidon.Core.Common.ClientText.Encoding,
    systemIni?.GetInt("FriendOfflineSMSLimitCount", tPoseidon.Core.Data.FriendRules.OfflineMessageLimit) ?? tPoseidon.Core.Data.FriendRules.OfflineMessageLimit,
    Log);
var partyRules = tPoseidon.Core.Net.Social.PartyRules.FromIni(
    systemIni?.GetInt("PartyLimitLevel", tPoseidon.Core.Net.Social.PartyRules.DefaultLimitLevel) ?? tPoseidon.Core.Net.Social.PartyRules.DefaultLimitLevel,
    systemIni?.GetInt("PartyMemberDepotInterval", tPoseidon.Core.Net.Social.PartyRules.DefaultDepotInterval) ?? tPoseidon.Core.Net.Social.PartyRules.DefaultDepotInterval);
var reportServer = new tPoseidon.Kr2014.SystemServer.ReportServer(
    new tPoseidon.Core.Data.Postgres.PostgresReportStore(postgres.DataSource),
    socialRoom,
    tPoseidon.Core.Common.ClientText.Encoding,
    (byte)ServerIdentity.System1.ServerIndex,
    Log);
var gameLinks = new tPoseidon.Kr2014.SystemServer.GameServerLinks(tPoseidon.Core.Net.S2.S2ServerHost.DefaultKeyTablePath(), Log)
{
    Text = tPoseidon.Core.Common.ClientText.Encoding,
};
var instantChatRooms = new tPoseidon.Core.Net.Social.InstantChatRooms(
    tPoseidon.Core.Common.ClientText.Encoding,
    (ushort)(systemIni?.GetInt("ChatNoticeMakeLimitLevel", tPoseidon.Core.Net.Social.InstantChatRooms.DefaultMakeLimitLevel) ?? tPoseidon.Core.Net.Social.InstantChatRooms.DefaultMakeLimitLevel),
    (ushort)(systemIni?.GetInt("ChatNoticeJoinLimitLevel", tPoseidon.Core.Net.Social.InstantChatRooms.DefaultJoinLimitLevel) ?? tPoseidon.Core.Net.Social.InstantChatRooms.DefaultJoinLimitLevel));
var chatServer = new tPoseidon.Kr2014.SystemServer.ChatServer(socialRoom, instantChatRooms, gameLinks, tPoseidon.Core.Common.ClientText.Encoding, Log);
var partyServer = new tPoseidon.Kr2014.SystemServer.PartyServer(
    new tPoseidon.Core.Net.Social.PartyService(tPoseidon.Core.Common.ClientText.Encoding, partyRules, TimeProvider.System),
    socialRoom,
    gameLinks,
    tPoseidon.Core.Common.ClientText.Encoding,
    Log);
var variantDataRoot = Environment.GetEnvironmentVariable("MT_VARIANT_DATA_ROOT")
    ?? Path.GetFullPath(RepoRoot.Data("variants", "Kr2014"));
var libConfigPath = new[]
{
    Environment.GetEnvironmentVariable("MT_LIBCONFIG_PATH") ?? "",
    Path.Combine(variantDataRoot, "libconfig.dat"),
    Path.Combine(variantDataRoot, "libconfig_eng.dat"),
    RepoRoot.Data("libconfig_kr_en.xml"),
}.FirstOrDefault(File.Exists);
var guildShopTables = libConfigPath is null
    ? tPoseidon.Core.LibConfig.GuildShopTables.Empty
    : new tPoseidon.Core.LibConfig.LibConfigXmlReader(libConfigPath).LoadGuildShopTables();
Log($"guild shop from libconfig: {guildShopTables.Goods.Count} goods (GuildShopGoodsTable)");
var guildEmblemItems = new tPoseidon.Core.Data.Postgres.PostgresItemStore(postgres.DataSource);
var guildEmblemProperties = new tPoseidon.Core.Data.Postgres.PostgresItemPropertyStore(postgres.DataSource);
var guildServer = new tPoseidon.Kr2014.SystemServer.GuildServer(
    new tPoseidon.Core.Net.Social.GuildService(
        new tPoseidon.Core.Data.Postgres.PostgresGuildRulesStore(postgres.DataSource),
        new tPoseidon.Core.Data.Postgres.PostgresGuildShopStore(postgres.DataSource),
        guildShopTables,
        tPoseidon.Core.Common.ClientText.Encoding,
        TimeProvider.System,
        (characterId, itemUid) => tPoseidon.Core.Game.Guild.GuildEmblemIcon.Resolve(guildEmblemItems, guildEmblemProperties, characterId, itemUid)),
    new tPoseidon.Core.Data.Postgres.PostgresGuildMatchStore(postgres.DataSource),
    TimeProvider.System,
    socialRoom,
    gameLinks,
    tPoseidon.Core.Common.ClientText.Encoding,
    Log,
    (systemIni?.GetInt("GuildGameTestMode", 0) ?? 0) != 0,
    tPoseidon.Core.Data.GuildCoinRules.FromEnvironment(Environment.GetEnvironmentVariable(tPoseidon.Core.Data.GuildCoinRules.PerWinVariable)));

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
    tPoseidon.Core.Common.ClientText.Encoding,
    TimeProvider.System,
    libConfigPath is null
        ? new Dictionary<string, string>()
        : new tPoseidon.Core.LibConfig.LibConfigXmlReader(libConfigPath).LoadNameStringTable(CoupleService.SystemServerStringTable),
    new tPoseidon.Core.Net.Social.FriendOfflineCoupleMemos(
        friendStore,
        systemIni?.GetInt("FriendOfflineSMSLimitCount", tPoseidon.Core.Data.FriendRules.OfflineMessageLimit) ?? tPoseidon.Core.Data.FriendRules.OfflineMessageLimit,
        tPoseidon.Core.Common.ClientText.Encoding));
friendServer.ZoneInfoOf = gameLinks.ZoneInfoOf;
friendServer.OrderFriendGame = gameLinks.SendReportedAsync;
friendServer.OrderPlayerGame = gameLinks.SendOnceAsync;
guildServer.Friends = friendStore;
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
    tPoseidon.Core.Common.ClientText.Encoding,
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
    tPoseidon.Core.Common.ClientText.Encoding,
    Log);

var giftServer = new tPoseidon.Kr2014.SystemServer.GiftServer(
    new tPoseidon.Core.Data.Postgres.PostgresGiftStore(postgres.DataSource),
    socialRoom,
    tPoseidon.Core.Common.ClientText.Encoding,
    Log)
{
    GiftAccepted = coupleServer.GiftAccepted,
};

var itemPrices = new tPoseidon.Kr2014.SystemServer.ItemPriceServer();
var angelServer = new tPoseidon.Kr2014.SystemServer.AngelServer(
    new AngelService(
        new tPoseidon.Core.Data.Postgres.PostgresAngelStore(postgres.DataSource),
        tPoseidon.Core.Common.ClientText.Encoding,
        TimeProvider.System),
    socialRoom,
    gameLinks,
    friendStore,
    tPoseidon.Core.Common.ClientText.Encoding,
    Log);

var cardBattleItems = new tPoseidon.Core.Data.Postgres.PostgresItemStore(postgres.DataSource);
var cardBattleServer = new tPoseidon.Kr2014.SystemServer.CardBattleServer(
    new CardBattleService(
        new tPoseidon.Core.Data.Postgres.PostgresCardBattleStore(postgres.DataSource),
        name => socialRoom.FindNamed(name) is { } named ? new CardBattlePlayer(named.Character.CharacterId, named.Character.Name, named.Character.Level) : null,
        (characterId, card) => cardBattleItems.GetForCharacterBySlotKind(characterId, tPoseidon.Core.Data.SlotKind.Inventory)
            .Any(item => item.ItemUid == tPoseidon.Core.Net.World.BagItemIds.Stored(card.ItemUid) && item.ItemIndex == card.ItemId),
        Random.Shared.Next,
        tPoseidon.Core.Common.ClientText.Encoding,
        libConfigPath is null
            ? null
            : new tPoseidon.Core.Game.CardBattle.SpecialCardSets(new tPoseidon.Core.LibConfig.LibConfigXmlReader(libConfigPath).LoadCardSpecialSets())),
    socialRoom,
    gameLinks,
    tPoseidon.Core.Common.ClientText.Encoding,
    Log);

var rankingStore = new tPoseidon.Core.Data.Postgres.PostgresRankingStore(postgres.DataSource);
var rankServer = new tPoseidon.Kr2014.SystemServer.RankServer(
    new RankService(rankingStore, tPoseidon.Core.Common.ClientText.Encoding),
    rankingStore,
    socialRoom,
    Log);

var gameMasterReader = libConfigPath is null ? null : new tPoseidon.Core.LibConfig.LibConfigXmlReader(libConfigPath);
var gameMasterChat = new GameMasterChat(
    gameMasterReader?.LoadNameStringTable(GameMasterChat.ResultTable) ?? new Dictionary<string, string>(),
    gameMasterReader?.LoadNameStringTable(GameMasterChat.HelpTable) ?? new Dictionary<string, string>());
MasterServerClient? masterClient = null;

async Task MasterFrameAsync(InterServerFrame frame, CancellationToken ct)
{
    if (frame.Opcode == InterServerOpcode.SysCmdCheckValidity)
    {
        if (masterClient is { IsConnected: true } checker)
        {
            await checker.SendAsync(new InterServerFrame
            {
                Opcode = InterServerOpcode.SysCmdCheckValidityAckOk,
                Header = new InterServerHeader
                {
                    SourceServerIndex = ServerIdentity.System1.ServerIndex,
                    DestServerIndex = 0,
                    CharUid = 0,
                    RequestId = 0,
                },
                Body = [],
            }, ct).ConfigureAwait(false);
        }
        return;
    }
    if (frame.Opcode != InterServerOpcode.SysCmdMegaphoneMessage) return;
    var post = MegaphoneMessages.DecodePost(frame.Body, tPoseidon.Core.Common.ClientText.Encoding);
    var shout = tPoseidon.Core.Net.World.SpecialItemFrames.MegaphoneShouted(post.Kind, post.Message, tPoseidon.Core.Common.ClientText.Encoding);
    var sent = 0;
    foreach (var member in socialRoom.Members)
    {
        try
        {
            var attempts = await tPoseidon.Kr2014.SystemServer.MegaphoneBroadcast.SendWithRetryAsync(
                token => member.Context.SendFrameAsync(tPoseidon.Core.Net.World.SpecialItemFrames.MegaphoneShout, shout, (byte)Random.Shared.Next(1, 256), token), ct).ConfigureAwait(false);
            sent++;
            if (attempts > 1) Log($"[megaphone] delivered to 0x{member.Character.CharacterId:X} after {attempts} attempts");
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            Log($"[megaphone] not delivered to 0x{member.Character.CharacterId:X}: {ex.Message}");
        }
    }
    Log($"[megaphone] {post.Code} shown to {sent} player(s): {post.Message}");
    if (masterClient is { IsConnected: true } master)
    {
        await master.SendAsync(new InterServerFrame
        {
            Opcode = InterServerOpcode.SysCmdMegaphoneMessageAckOk,
            Header = new InterServerHeader
            {
                SourceServerIndex = ServerIdentity.System1.ServerIndex,
                DestServerIndex = 0,
                CharUid = frame.Header.CharUid,
                RequestId = 0,
            },
            Body = MegaphoneMessages.EncodeAck(post.Code),
        }, ct).ConfigureAwait(false);
    }
}
var gameMasterService = new GameMasterService(postgres.GmAuthority, tPoseidon.Core.Common.ClientText.Encoding)
    {
        Friends = new tPoseidon.Core.Data.Postgres.PostgresFriendStore(postgres.DataSource),
        Guilds = new tPoseidon.Core.Data.Postgres.PostgresGuildStore(postgres.DataSource),
        MegaphoneNotices = new tPoseidon.Core.Data.Postgres.PostgresMegaphoneQueueStore(postgres.DataSource),
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
    new tPoseidon.Core.Data.Postgres.PostgresCharacterDirectory(postgres.DataSource),
    socialRoom,
    gameLinks,
    new tPoseidon.Core.Data.Postgres.PostgresGmRateEventStore(postgres.DataSource),
    tPoseidon.Core.Common.ClientText.Encoding,
    systemIni?.GetBool("GameMasterToolLoopbackOnly", true) ?? true,
    Log);
gameMasterServer.GuildDissolver = guildServer.ForceDissolveAsync;
gameMasterServer.GuildRooms = guildServer.GameRooms;
friendServer.IsGameMaster = gameMasterServer.IsGameMaster;
reportServer.IsGameMaster = gameMasterServer.IsGameMaster;

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
        itemPrices.Register(ctx.Dispatcher, ctx);
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
    await friendServer.TypeChangedAsync(characterId, kind, job, ct).ConfigureAwait(false);
    await partyServer.KindChangedAsync(characterId, kind, job, ct).ConfigureAwait(false);
};
gameLinks.OnRenamed = async (characterId, _, newName, ct) =>
{
    socialRoom.Find(characterId)?.Rename(newName);
    guildServer.Renamed(characterId, newName);
    await friendServer.RenamedAsync(characterId, newName, ct).ConfigureAwait(false);
    await partyServer.RenamedAsync(characterId, newName, ct).ConfigureAwait(false);
};
gameLinks.OnCoupleMessage = coupleServer.LinkMessageAsync;
gameLinks.OnReport = async (report, ct) =>
{
    await angelServer.ReportAsync(report, ct).ConfigureAwait(false);
    await friendServer.ReportAsync(report, ct).ConfigureAwait(false);
    await cardBattleServer.ReportAsync(report, ct).ConfigureAwait(false);
    await coupleServer.ReportAsync(report, ct).ConfigureAwait(false);
    await gameMasterServer.ReportAsync(report, ct).ConfigureAwait(false);
    await chatServer.ReportAsync(report, ct).ConfigureAwait(false);
};
gameLinks.OnLevelChanged = async (characterId, level, ct) =>
{
    await angelServer.LevelChangedAsync(characterId, level, ct).ConfigureAwait(false);
    await partyServer.LevelChangedAsync(characterId, level, ct).ConfigureAwait(false);
};
gameLinks.OnGuildMatchResult = guildServer.MatchResultAsync;
gameLinks.OnGuildRoomInvalid = guildServer.RoomInvalidAsync;
gameLinks.OnGameMasterChat = gameMasterServer.ChatAsync;
gameMasterServer.InstantChatRemover = chatServer.RemoveRoomOf;
var campGifts = new tPoseidon.Core.Data.MyCampGifts(
    new tPoseidon.Core.Data.Postgres.PostgresMyCampStore(postgres.DataSource),
    new tPoseidon.Core.Data.Postgres.PostgresCharacterDirectory(postgres.DataSource),
    new tPoseidon.Core.Data.Postgres.PostgresCharStore(postgres.DataSource));
gameMasterServer.CampItemGiver = (characterName, itemIndex, count) =>
{
    if (campGifts.Find(characterName) is not { } owner) return false;
    campGifts.Give(owner, itemIndex, count);
    return true;
};
var membership = new tPoseidon.Core.Data.Postgres.PostgresMembershipStore(postgres.DataSource);
gameMasterServer.MemberSetter = membership.SetMemberOfCharacter;
if (libConfigPath is not null)
{
    gameMasterServer.KnownCampItems = new tPoseidon.Core.LibConfig.LibConfigXmlReader(libConfigPath).LoadMyCampItems().Select(entry => entry.Id).ToHashSet();
}
gameLinks.OnLinked = gameMasterServer.GameServerLinkedAsync;
var gameLinkTasks = new[] { ServerEndpointConfig.GamePort, ServerEndpointConfig.GameAltPort }
    .Select(gamePort => gameLinks.RunAsync(ServerEndpointConfig.MasterHost, gamePort, stopCts.Token))
    .ToArray();
var rankJobTask = rankServer.RunJobAsync(stopCts.Token);
var guildRankJobTask = new tPoseidon.Kr2014.SystemServer.GuildRankJob(new tPoseidon.Core.Data.Postgres.PostgresGuildStore(postgres.DataSource), Log).RunAsync(stopCts.Token);
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
        onBrokeredFrame: MasterFrameAsync,
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
    await guildRankJobTask.ConfigureAwait(false);
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
