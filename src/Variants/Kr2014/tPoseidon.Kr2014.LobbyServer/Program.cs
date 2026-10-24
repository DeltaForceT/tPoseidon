using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using LoginRequestPacket = tPoseidon.Kr2014.Wire.LoginRequestPacket;

ConsoleFileLog.EnableIfConfigured();
var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : ServerEndpointConfig.LobbyPort;
const string LogTag = "[lobby]";
Console.WriteLine($"{LogTag} {ServerEndpointConfig.Summary()}");

var serverListReply = new ServerListPacket
{
    PublisherUrl = "http://www.chinesegamer.com",
    OccupiedEntry = ServerEntry.Empty,
};
var serverListPlaintext = serverListReply.EncodePayload();
Console.WriteLine($"{LogTag} SERVER_LIST encoder ready ({serverListPlaintext.Length} bytes plaintext)");

var pg = await PostgresStoreBootstrap.InitializeAsync();
var accountStore = pg.Store;
var launchTickets = new PostgresLaunchTicketStore(pg.DataSource);
Console.WriteLine($"{LogTag} account store ready (Postgres-backed); use mt-account-create to register operators");

System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
var clientText = System.Text.Encoding.GetEncoding(949);
var lobbyStatesReset = accountStore.ResetLobbyStates();
Console.WriteLine($"{LogTag} lobby start: {lobbyStatesReset} account(s) left in the lobby or server-select state set to 0");
var lobbySessions = new System.Collections.Concurrent.ConcurrentDictionary<S2SessionContext, uint>();

var host = new S2ServerHost
{
    LogTag = LogTag,
    Port = port,
    KeyTablePath = S2ServerHost.DefaultKeyTablePath(),
    SessionsLogIn = true,
    OnSessionClosed = ctx =>
    {
        if (lobbySessions.TryRemove(ctx, out var closedAccount) && accountStore.EndLobbySession(closedAccount))
        {
            Console.WriteLine($"{LogTag} {ctx.Remote} account {closedAccount} left the lobby before picking a server -> state 0");
        }
        return Task.CompletedTask;
    },
    OnSessionAccepted = (ctx, _) =>
    {
        uint? sessionAccountId = null;

        ctx.Dispatcher.Register(Opcode.LOGIN_REQUEST, async (_, payload, innerCt) =>
        {
            LoginRequestPacket pkt;
            try
            {
                pkt = LoginRequestPacket.DecodePayload(payload.Span);
            }
            catch (InvalidPacketException ex)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} bad LoginRequest: {ex.Message}");
                return;
            }

            Console.WriteLine($"{LogTag} {ctx.Remote} LOGIN_REQUEST user='{pkt.Username}' (pw {pkt.Password.Length} chars)");

            var loginGate = ctx.Guard.CheckLogin(pkt.Username);
            if (loginGate.LockedOut)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} login for '{pkt.Username}' refused: too many failed logins -- closing socket like a failed login");
                await Task.Delay(loginGate.Delay, innerCt).ConfigureAwait(false);
                await ctx.Session.DisposeAsync().ConfigureAwait(false);
                return;
            }

            if (!LaunchTicket.VerifyLogin(accountStore, launchTickets, pkt.Username, pkt.Password, ctx.Remote?.Address ?? System.Net.IPAddress.None))
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} auth failed for '{pkt.Username}' -- sending 0x{LobbyLogin.RefusalOpcode:X4} [{(uint)LobbyLoginRefusal.WrongIdOrPassword}]");
                await Task.Delay(ctx.Guard.LoginFailed(pkt.Username), innerCt).ConfigureAwait(false);
                await SendFrameAsync(LobbyLogin.RefusalOpcode, 0x8C, LobbyLogin.RefusalPayload(LobbyLoginRefusal.WrongIdOrPassword)).ConfigureAwait(false);
                return;
            }
            var account = accountStore.GetAccountByUsername(pkt.Username);
            if (account is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} VerifyPassword passed but account row not found for '{pkt.Username}' -- closing socket");
                await ctx.Session.DisposeAsync().ConfigureAwait(false);
                return;
            }

            var lobbyState = accountStore.GetLobbyAccountState(account.AccountId)
                ?? new LobbyAccountState(0, 0, 0, null);
            var runningPenalty = lobbyState.GameAccess == LobbyLogin.BlockedGameAccess ? accountStore.FindRunningPenalty(account.AccountId) : null;
            if (lobbyState.GameAccess == LobbyLogin.BlockedGameAccess && runningPenalty is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} account {account.AccountId} '{pkt.Username}': game access 1 without a running penalty row (penalty data error); login allowed");
            }
            var accessRefusal = LobbyLogin.Decide(lobbyState, runningPenalty, DateTimeOffset.UtcNow);
            if (accessRefusal != LobbyLoginRefusal.None)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} login for '{pkt.Username}' refused: {accessRefusal} ({(uint)accessRefusal}), account state {lobbyState.State}");
                if (accessRefusal == LobbyLoginRefusal.GameBlocked && runningPenalty is not null)
                {
                    await SendFrameAsync(LobbyLogin.BlockNoticeOpcode, 0x8C, LobbyLogin.BlockNoticePayload(runningPenalty, TimeZoneInfo.Local, clientText)).ConfigureAwait(false);
                }
                await SendFrameAsync(LobbyLogin.RefusalOpcode, 0x8C, LobbyLogin.RefusalPayload(accessRefusal)).ConfigureAwait(false);
                return;
            }

            var rawTokenBytes = Guid.NewGuid().ToByteArray();
            long authcode =
                  ((long)rawTokenBytes[0]      ) | ((long)rawTokenBytes[1] <<  8)
                | ((long)rawTokenBytes[2] << 16) | ((long)rawTokenBytes[3] << 24)
                | ((long)rawTokenBytes[4] << 32) | ((long)rawTokenBytes[5] << 40)
                | ((long)rawTokenBytes[6] << 48) | ((long)(rawTokenBytes[7] & 0x0F) << 56);
            var clientIp = ctx.Remote?.Address ?? System.Net.IPAddress.None;
            accountStore.IssueGameAuthcode(account.AccountId, authcode, clientIp);





            var token = accountStore.IssueSessionToken(account.AccountId, TimeSpan.FromMinutes(5));
            Console.WriteLine($"{LogTag} {ctx.Remote} auth OK account={account.AccountId} authcode=0x{authcode:X16} (session token 0x{token.Value:X8}, state=2)");
            ctx.Guard.LoginSucceeded(pkt.Username);


























            await SendFrameAsync((ushort)Opcode.SERVER_LIST, 0xA1, serverListPlaintext);









            var worldName   = Environment.GetEnvironmentVariable("MT_WORLD_NAME")   ?? "ModernTrickster";
            var channelName = Environment.GetEnvironmentVariable("MT_CHANNEL_NAME") ?? "Main";
            var loginResult = LoginResultPacket.Create(account.AccountId, authcode, worldName, channelName);
            sessionAccountId = account.AccountId;
            lobbySessions[ctx] = account.AccountId;
            await SendFrameAsync((ushort)Opcode.LOGIN_RESULT, 0x8C, loginResult.EncodePayload());
            Console.WriteLine($"{LogTag} {ctx.Remote} sent SERVER_LIST + LOGIN_RESULT world='{worldName}' channel='{channelName}' (sendKey now 0x{ctx.SessionInfo.SendKey:X2})");

            async Task SendFrameAsync(ushort op, byte randKey, byte[] payload)
            {
                var frame = ctx.Cipher.Encrypt(
                    opcode: op,
                    sequence: 0,
                    randKey: randKey,
                    flags: PackingFlags.KeyUpdate | PackingFlags.PayloadEncrypted,
                    plaintextPayload: payload,
                    session: ctx.SessionInfo);
                await ctx.Session.SendAsync(frame, innerCt).ConfigureAwait(false);
            }
        });

        ctx.Dispatcher.Register(Opcode.SELECT_SERVER, async (_, payload, innerCt) =>
        {
            SelectServerPacket pkt;
            try
            {
                pkt = SelectServerPacket.DecodePayload(payload.Span);
            }
            catch (InvalidPacketException ex)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} bad SelectServer: {ex.Message}");
                return;
            }
            Console.WriteLine($"{LogTag} {ctx.Remote} SELECT_SERVER server={pkt.ServerIndex} channel={pkt.ChannelIndex}");
            if (sessionAccountId is not { } selectingAccount)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} SELECT_SERVER before a successful login; no channel hand-off");
                return;
            }
            if (!accountStore.TransitionToServerSelected(selectingAccount, (short)pkt.ServerIndex, (short)pkt.ChannelIndex))
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} SELECT_SERVER account={selectingAccount}: account is not in the logged-in state 2; the channel will refuse it");
            }






            var handoff = new ChannelHandoffPacket
            {
                IpAddress = ServerEndpointConfig.PublicIp,
                ReservedByte = 0x37,
                ChannelPort = ServerEndpointConfig.ChannelPort,
            };
            var handoffFrame = ctx.Cipher.Encrypt(
                opcode: (ushort)Opcode.CHANNEL_HANDOFF,
                sequence: 0,
                randKey: 0xE0,
                flags: PackingFlags.KeyUpdate | PackingFlags.PayloadEncrypted,
                plaintextPayload: handoff.EncodePayload(),
                session: ctx.SessionInfo);
            await ctx.Session.SendAsync(handoffFrame, innerCt).ConfigureAwait(false);
            Console.WriteLine($"{LogTag} {ctx.Remote} sent CHANNEL_HANDOFF -> {handoff.IpAddress}:{handoff.ChannelPort} (sendKey now 0x{ctx.SessionInfo.SendKey:X2})");
        });

        return Task.CompletedTask;
    },
};

using var stopSignal = S2ServerHost.WireCtrlC();
var penaltyRelease = ReleasePenaltiesAsync(stopSignal.Token);
await host.RunAsync(stopSignal.Token);
await penaltyRelease;

async Task ReleasePenaltiesAsync(CancellationToken ct)
{
    using var timer = new PeriodicTimer(LobbyLogin.PenaltyReleasePeriod);
    try
    {
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var released = accountStore.ReleaseEndedPenalties();
            if (released > 0)
            {
                Console.WriteLine($"{LogTag} penalty release: {released} account(s) whose penalty ended may log in again");
            }
        }
    }
    catch (OperationCanceledException)
    {
    }
}
