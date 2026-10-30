using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using tPoseidon.Kr2014.Common;
using ChannelIntroPacket = tPoseidon.Kr2014.Wire.ChannelIntroPacket;
using CharacterListBuilder = tPoseidon.Kr2014.Wire.CharacterListBuilder;
using CharCreatedReplyPacket = tPoseidon.Kr2014.Wire.CharCreatedReplyPacket;

ConsoleFileLog.EnableIfConfigured();
var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : ServerEndpointConfig.ChannelPort;
const string LogTag = "[channel]";
const byte SecondPasswordRandKey = 0x4D;
Console.WriteLine($"{LogTag} {ServerEndpointConfig.Summary()}");


var kr2014LoginIni = ServerIniLocator.TryLoad(
    ServerIniLocator.ServerKind.Login,
    ServerIniLocator.Variant.Kr2014,
    1,
    msg => Console.WriteLine($"{LogTag} {msg}"));
if (kr2014LoginIni is not null)
{
    var iniMaxClient = kr2014LoginIni.GetInt("MaxClient", -1);
    var iniHeartbeat = kr2014LoginIni.GetInt("Heartbeat", -1);
    var iniServerId = kr2014LoginIni.GetString("ServerID") ?? "?";
    var iniWorld = kr2014LoginIni.GetString("ServerWorld") ?? "?";
    var iniFreeCreate = kr2014LoginIni.GetBool("FreeCreate", false);
    var iniFreeLogin = kr2014LoginIni.GetBool("FreeLogin", false);
    Console.WriteLine($"{LogTag} LoginServer.ini knobs: ServerID={iniServerId}, World={iniWorld}, MaxClient={iniMaxClient}, Heartbeat={iniHeartbeat}ms, FreeCreate={iniFreeCreate}, FreeLogin={iniFreeLogin}");
}
var iniVersion = kr2014LoginIni?.GetString("Version");
var expectedClientVersion = ClientVersion.TryParse(iniVersion, out var configuredVersion) ? configuredVersion : ClientVersion.Kr2014;
Console.WriteLine($"{LogTag} client version {expectedClientVersion} ({(iniVersion is null ? "the KR 2014 client" : "LoginServer.ini Version")}); ChannelIntro with another version is refused with 0x{ChannelLogin.RefusalOpcode:X4} [0x{(uint)ChannelLoginRefusal.VersionMismatch:X}]");
var heartbeatAnswerTimeout = ClientHeartbeat.AnswerTimeoutFromIni(kr2014LoginIni?.GetInt("Heartbeat", -1) ?? -1);
Console.WriteLine($"{LogTag} heartbeat: S2C 0x07EC after {ClientHeartbeat.FirstPingDelay.TotalSeconds:0}s, then every {ClientHeartbeat.PingInterval.TotalSeconds:0}s; no answer within {heartbeatAnswerTimeout.TotalMilliseconds:0}ms closes the session");

var pg = await PostgresStoreBootstrap.InitializeAsync();
var accountStore = pg.Store;

IItemStore itemStore = new PostgresItemStore(pg.DataSource);
ISkillStore skillStore = new PostgresSkillStore(pg.DataSource);
IMyCampStore myCampStore = new PostgresMyCampStore(pg.DataSource);
var deletedCharStore = new PostgresDeletedCharStore(pg.DataSource);
var safetyPasswordStore = new PostgresSafetyPasswordStore(pg.DataSource);
var secondPasswords = new tPoseidon.Core.Net.Social.SecondPasswordService(safetyPasswordStore, tPoseidon.Core.Common.ClientText.Encoding, TimeProvider.System);
var gmBlockStore = new PostgresGmBlockStore(pg.DataSource);
var characterCreationManager = new tPoseidon.Core.Game.Characters.CharacterCreationManager(
    accountStore, itemStore, skillStore, myCampStore);
Console.WriteLine($"{LogTag} account store ready (Postgres-backed); ChannelIntro is checked against the lobby authcode");

var LocalHostIp = ServerEndpointConfig.PublicIp;
var GameServerPort = ServerEndpointConfig.GamePort;
var LoginServerPort = ServerEndpointConfig.LoginPort;

var channelSessions = new System.Collections.Concurrent.ConcurrentDictionary<uint, S2SessionContext>();
var handedOff = new System.Collections.Concurrent.ConcurrentDictionary<S2SessionContext, byte>();
var worldStatesReset = accountStore.ResetWorldStates(ChannelLogin.World, ChannelLogin.Island);
Console.WriteLine($"{LogTag} channel start: {worldStatesReset} account(s) still marked logged in set to 0, as the KR LoginServer restart does");

var host = new S2ServerHost
{
    LogTag = LogTag,
    Port = port,
    KeyTablePath = S2ServerHost.DefaultKeyTablePath(),
    SessionsLogIn = true,
    ClientsKeepAlive = true,
    OnSessionClosed = ctx =>
    {
        foreach (var entry in channelSessions)
        {
            if (ReferenceEquals(entry.Value, ctx) && channelSessions.TryRemove(entry))
            {
                if (handedOff.TryRemove(ctx, out _))
                {
                    continue;
                }
                accountStore.TransitionToOffline(entry.Key);
                Console.WriteLine($"{LogTag} {ctx.Remote} account {entry.Key} left the channel without selecting a character -> state 0");
            }
        }
        return Task.CompletedTask;
    },
    OnSessionAccepted = (ctx, sessionCt) =>
    {
        uint? sessionAccountId = null;
        string? sessionUsername = null;
        var heartbeat = new ClientHeartbeat(heartbeatAnswerTimeout);
        _ = RunHeartbeatAsync(ctx, heartbeat, sessionCt);

        ctx.Dispatcher.Register(Opcode.C2S_HEARTBEAT_INIT, async (_, payload, innerCt) =>
        {
            ChannelIntroPacket intro;
            try
            {
                intro = ChannelIntroPacket.DecodePayload(payload.Span);
            }
            catch (InvalidPacketException ex)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} bad ChannelIntro: {ex.Message}");
                return;
            }
            Console.WriteLine($"{LogTag} {ctx.Remote} ChannelIntro user='{intro.Username}' (pw {intro.Password.Length} chars)");

            var loginGate = ctx.Guard.CheckLogin(intro.Username);
            if (loginGate.LockedOut)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} ChannelIntro for '{intro.Username}' refused: too many failed logins");
                await Task.Delay(loginGate.Delay, innerCt).ConfigureAwait(false);
                return;
            }

            var versionRefusal = ChannelLogin.CheckVersion(intro.Version, expectedClientVersion);
            if (versionRefusal != ChannelLoginRefusal.None)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} ChannelIntro user='{intro.Username}' refused: client version {intro.Version}, server {expectedClientVersion} ({versionRefusal}, 0x{(uint)versionRefusal:X})");
                await SendRawFrameAsync(ctx, ChannelLogin.RefusalOpcode, randKey: 0x4D, ChannelLogin.RefusalPayload(versionRefusal), innerCt);
                return;
            }

            var token = ChannelLoginToken.Read(intro.LoginResultToken);
            if (channelSessions.TryGetValue(token.AccountId, out var holder) && !ReferenceEquals(holder, ctx))
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} ChannelIntro account={token.AccountId} already on the channel from {holder.Remote}: refusing the new session and closing the old one");
                await SendRawFrameAsync(ctx, ChannelLogin.RefusalOpcode, randKey: 0x4D, ChannelLogin.RefusalPayload(ChannelLoginRefusal.DuplicateUser), innerCt);
                await holder.Session.DisposeAsync().ConfigureAwait(false);
                return;
            }

            var gameLogin = accountStore.GetGameLogin(token.AccountId);
            var refusal = ChannelLogin.Check(token, gameLogin);
            if (refusal == ChannelLoginRefusal.None && !accountStore.JoinGame(token.AccountId, token.Authcode, ChannelLogin.Island))
            {
                refusal = ChannelLoginRefusal.InvalidAccountState;
            }
            var account = refusal == ChannelLoginRefusal.None ? accountStore.GetAccountById(token.AccountId) : null;
            if (account is null)
            {
                var code = refusal == ChannelLoginRefusal.None ? ChannelLoginRefusal.AccountNotFound : refusal;
                if (gameLogin is not null && gameLogin.AccountId == token.AccountId)
                {
                    accountStore.ResetGameState(token.AccountId);
                }
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} ChannelIntro user='{intro.Username}' account={token.AccountId} refused: {code} ({(uint)code}){(gameLogin is null ? string.Empty : "; account state set to 0")}");
                await Task.Delay(ctx.Guard.LoginFailed(intro.Username), innerCt).ConfigureAwait(false);
                await SendRawFrameAsync(ctx, ChannelLogin.RefusalOpcode, randKey: 0x4D, ChannelLogin.RefusalPayload(code), innerCt);
                return;
            }

            if (!string.Equals(account.Username, intro.Username, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} ChannelIntro typed user '{intro.Username}' differs from account {account.AccountId} '{account.Username}'; the lobby authcode decides");
            }

            channelSessions[account.AccountId] = ctx;
            sessionAccountId = account.AccountId;
            sessionUsername = account.Username;
            ctx.Guard.LoginSucceeded(intro.Username);

            foreach (var info in secondPasswords.InfoAtLogin(account.AccountId))
            {
                await SendRawFrameAsync(ctx, info.Opcode, randKey: SecondPasswordRandKey, info.Payload, innerCt);
            }
            var characters = accountStore.GetCharactersForAccount(account.AccountId);
            var charListPayload = CharacterListBuilder.Build(characters, itemStore).EncodePayload();
            await SendFrameAsync(ctx, Opcode.CHAR_LIST, randKey: 0xB3, charListPayload, innerCt);
            Console.WriteLine($"{LogTag} {ctx.Remote} sent CHAR_LIST account={account.AccountId} ({charListPayload.Length}B, {characters.Count} chars, slot quota=8)");
            heartbeat.Start();
        });

        ctx.Dispatcher.Register(Opcode.HEARTBEAT_ACK, (_, _, _) =>
        {
            heartbeat.Answered();
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register((ushort)Kr2014Opcode.C2S_KR_CHARCREATE_QUERY_07F6, async (_, _, innerCt) =>
        {
            await SendRawFrameAsync(ctx, (ushort)Kr2014Opcode.S2C_KR_CHARSELECT_WAIT_CLEAR,
                randKey: 0x12, new byte[] { 0x00, 0x00 }, innerCt);
            Console.WriteLine($"{LogTag} {ctx.Remote} 0x07F6 received → sent 0x0818 [00 00] (clears CharSelect wait flag, skips 30-60s watchdog)");
        });
        ctx.Dispatcher.Register((ushort)Kr2014Opcode.C2S_KR_CHARCREATE_QUERY_0817, async (_, _, innerCt) =>
        {
            var payload = new byte[] { 0xFF, 0x00, 0x00, 0x00 };
            Console.WriteLine($"{LogTag} {ctx.Remote} 0x0817 answered with the character slot states (0x07F8)");
            await SendRawFrameAsync(ctx, (ushort)Kr2014Opcode.S2C_KR_CHARSLOT_COUPON_COUNT, randKey: 0x4D,
                payload, innerCt);
        });

        ctx.Dispatcher.Register(Opcode.C2S_CHAR_CREATE_REQUEST, async (_, payload, innerCt) =>
        {
            async Task SendCreateFailAsync(uint errorCode, string reason)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} CharCreate failed (0x{errorCode:X3}): {reason}");
                var failPayload = new byte[]
                {
                    (byte)(errorCode & 0xFF),
                    (byte)((errorCode >> 8) & 0xFF),
                    (byte)((errorCode >> 16) & 0xFF),
                    (byte)((errorCode >> 24) & 0xFF),
                };
                await SendFrameAsync(ctx, Opcode.CHAR_CREATE_FAIL, randKey: 0x4D, failPayload, innerCt);
            }

            CharCreateRequestPacket req;
            try
            {
                req = CharCreateRequestPacket.DecodePayload(payload.Span);
            }
            catch (InvalidPacketException ex)
            {
                await SendCreateFailAsync(0x3FB, $"bad CharCreateRequest: {ex.Message}");
                return;
            }

            Console.WriteLine($"{LogTag} {ctx.Remote} CharCreateRequest name='{req.Name}' species={req.SpeciesEnum} ftype={req.FType} stats=[{req.StatA},{req.StatB},{req.StatC},{req.StatD}] hair={req.Hair}");

            const int MaxNameChars = CharCreatedReplyPacket.MaxCharacterNameChars;
            if (string.IsNullOrEmpty(req.Name) || tPoseidon.Core.Common.ClientText.Encoding.GetByteCount(req.Name) > MaxNameChars)
            {
                await SendCreateFailAsync(0x3FB, $"name length {(req.Name is null ? 0 : tPoseidon.Core.Common.ClientText.Encoding.GetByteCount(req.Name))} bytes not in 1..{MaxNameChars}");
                return;
            }

            var validation = req.ValidateRules();
            if (validation is not null)
            {
                await SendCreateFailAsync(0x3FB, $"validator: {validation}");
                return;
            }

            if (sessionAccountId is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} CharCreate before channel-intro auth; closing");
                return;
            }
            var existingChars = accountStore.GetCharactersForAccount(sessionAccountId.Value);

            var usedSlots = existingChars.Select(c => c.Slot).ToHashSet();
            byte freshSlot = 0;
            while (freshSlot < 3 && usedSlots.Contains(freshSlot)) freshSlot++;
            if (freshSlot >= 3)
            {
                await SendCreateFailAsync(0x3FC, "no free slot (account already has 3 characters)");
                return;
            }

            var freshCharId = 0x10000001ul + ((ulong)sessionAccountId.Value << 3) + freshSlot;

            var creationRequest = new tPoseidon.Core.Game.Characters.CharacterCreationRequest
            {
                CharacterId = freshCharId,
                OwnerAccountId = sessionAccountId.Value,
                Slot = freshSlot,
                Name = req.Name,
                Species = req.SpeciesEnum,

                FType = (FType)(req.FType + 1),
                Hair = req.Hair,
                GrowthGraph = (req.StatA, req.StatB, req.StatC, req.StatD),
            };

            CharCreationOutcome creationOutcome;
            try
            {
                creationOutcome = characterCreationManager.CreateCharacter(creationRequest);
            }
            catch (InvalidOperationException ex)
            {
                await SendCreateFailAsync(0x3FE, $"persist failed: {ex.Message}");
                return;
            }
            if (!creationOutcome.IsSuccess || creationOutcome.Snapshot is null)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} char-create rejected: error={creationOutcome.Error}");
                await SendCreateFailAsync((ushort)creationOutcome.Error, $"create failed: {creationOutcome.Error}");
                return;
            }
            var character = creationOutcome.Snapshot.Character;
            Console.WriteLine($"{LogTag} {ctx.Remote} created char {character.Name} (uid=0x{character.CharacterId:X16}, slot={character.Slot})");

            byte[] replyBytes;
            try
            {
                var reply = CharCreatedReplyPacket.FromNewCharacter(character, sessionUsername ?? string.Empty);
                replyBytes = reply.EncodePayload();
            }
            catch (InvalidOperationException ex)
            {
                accountStore.DeleteCharacter(character.CharacterId, character.OwnerAccountId);
                await SendCreateFailAsync(0x3FE, $"reply encode failed: {ex.Message}");
                return;
            }

            await SendFrameAsync(ctx, Opcode.CHAR_CREATED, randKey: 0x4D, replyBytes, innerCt);
            Console.WriteLine($"{LogTag} {ctx.Remote} sent CHAR_CREATED uid=0x{character.CharacterId:X16} slot={character.Slot} (now {existingChars.Count + 1} characters on account)");

            var hex = string.Join(" ", replyBytes[..16].Select(b => $"{b:X2}"));
            Console.WriteLine($"{LogTag}   CHAR_CREATED [0..15]: {hex}");
        });

        ctx.Dispatcher.Register(Opcode.C2S_HEARTBEAT_CHAR_SELECT, async (_, payload, innerCt) =>
        {
            if (payload.Length < 8)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} ChannelSelect payload too short ({payload.Length}B); dropping");
                return;
            }
            ulong selectedCharUid = 0;
            for (var i = 0; i < 8; i++) selectedCharUid |= (ulong)payload.Span[i] << (i * 8);

            var hex = string.Join(" ", payload.Span.ToArray().Select(b => $"{b:X2}"));
            Console.WriteLine($"{LogTag} {ctx.Remote} ChannelSelect raw payload ({payload.Length}B): {hex} (uid=0x{selectedCharUid:X16})");

            if (sessionAccountId is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} CharSelect before channel-intro auth; closing");
                return;
            }

            var character = accountStore.GetCharacterById(selectedCharUid);
            if (character is null || character.OwnerAccountId != sessionAccountId.Value)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} CharSelect uid=0x{selectedCharUid:X16} not owned by account {sessionAccountId.Value}; refusing handoff");
                await SendFrameAsync(ctx, Opcode.CHAR_SELECT_FAIL, randKey: 0x4D, new byte[] { 0x04, 0x04, 0x00, 0x00 }, innerCt);
                return;
            }

            var clientIp = ctx.Remote?.Address ?? System.Net.IPAddress.None;
            accountStore.TransitionToWorldSelected(sessionAccountId.Value, clientIp, world: ChannelLogin.World, island: ChannelLogin.Island, selectedCharUid: selectedCharUid);
            handedOff[ctx] = 0;

            var sessionToken = accountStore.IssueSessionToken(sessionAccountId.Value, TimeSpan.FromMinutes(5));

            var runningBlock = gmBlockStore.LatestRunningBlock(selectedCharUid);
            var handoff = new ChannelHandoffReplyPacket
            {
                BaseBytes = ChannelHandoffReplyPacket.SnapshotBytes,
                SelectedCharUid = selectedCharUid,
                IpAddress = LocalHostIp,
                GamePort = GameServerPort,
                BlockMinutes = GmBlockRules.LoginBlockMinutes(runningBlock),
            };
            if (runningBlock is { } block)
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} uid=0x{selectedCharUid:X16} has a running block: type {block.Type}, {block.MinutesLeft} minute(s) left{(block.Type is >= GmBlockRules.FirstLoginBlockType and <= GmBlockRules.LastLoginBlockType ? string.Empty : " (not a login block type; not sent)")}");
            }
            await SendFrameAsync(ctx, Opcode.CHANNEL_HANDOFF_REPLY, randKey: 0xB3,
                handoff.EncodePayload(), innerCt);

            await SendFrameAsync(ctx, Opcode.CHANNEL_PRE_HANDOFF, randKey: 0x12,
                ChannelPreHandoffPacket.SnapshotDefault.EncodePayload(), innerCt);

            var sessionParam = new SessionParam1Packet
            {
                SelectedCharUid = selectedCharUid,
                IpAddress = LocalHostIp,
                LoginPort = LoginServerPort,
                SessionToken = sessionToken.Value,
            };
            await SendFrameAsync(ctx, Opcode.SESSION_PARAM_1, randKey: 0x4D,
                sessionParam.EncodePayload(), innerCt);

            Console.WriteLine($"{LogTag} {ctx.Remote} sent 3-frame handoff bundle (uid=0x{selectedCharUid:X16}, GameServer={LocalHostIp}:{GameServerPort}, account={sessionAccountId.Value} state=4 token=0x{sessionToken.Value:X8})");
        });

        ctx.Dispatcher.Register(tPoseidon.Core.Net.Social.SecondPasswordFrames.Envelope, async (_, payload, innerCt) =>
        {
            if (sessionAccountId is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} second password request before channel-intro auth; closing");
                return;
            }
            foreach (var reply in secondPasswords.Handle(sessionAccountId.Value, payload.Span))
            {
                await SendRawFrameAsync(ctx, reply.Opcode, randKey: SecondPasswordRandKey, reply.Payload, innerCt);
            }
        });

        ctx.Dispatcher.Register(Opcode.C2S_CHAR_DELETE, async (_, payload, innerCt) =>
        {
            tPoseidon.Kr2014.Wire.CharDeleteRequestPacket request;
            try
            {
                request = tPoseidon.Kr2014.Wire.CharDeleteRequestPacket.DecodePayload(payload.Span);
            }
            catch (InvalidPacketException ex)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} bad CharDelete: {ex.Message} payload={Convert.ToHexString(payload.Span)}; dropping");
                return;
            }
            var charId = request.CharacterUid;

            if (sessionAccountId is null)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} CharDelete before channel-intro auth; closing");
                return;
            }
            Console.WriteLine($"{LogTag} {ctx.Remote} CharDelete request uid=0x{charId:X16} (second password digest {request.SecondPasswordDigest.Length} chars)");
            if (!secondPasswords.PermitsCharacterDelete(sessionAccountId.Value, request.SecondPasswordDigest))
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} CharDelete uid=0x{charId:X16} refused: the second password does not match");
                var refusal = tPoseidon.Core.Net.Social.SecondPasswordFrames.RefusedReply(tPoseidon.Core.Net.Social.SecondPasswordCodes.WrongPassword);
                await SendRawFrameAsync(ctx, refusal.Opcode, randKey: SecondPasswordRandKey, refusal.Payload, innerCt);
                return;
            }
            var snapshot = accountStore.GetCharacterById(charId);
            var deleted = accountStore.DeleteCharacter(charId, sessionAccountId.Value);
            if (deleted && snapshot is not null)
            {
                var clientIp = ctx.Remote is System.Net.IPEndPoint ep ? ep.Address : null;
                deletedCharStore.SoftDelete(charId, sessionAccountId.Value, snapshot.Name, clientIp);
            }
            if (!deleted)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} CharDelete uid=0x{charId:X16} not found or wrong owner — sending CHAR_DELETE_FAIL(0x401)");
                var failPayload = new byte[] { 0x01, 0x04, 0x00, 0x00 };
                await SendFrameAsync(ctx, Opcode.CHAR_DELETE_FAIL, randKey: 0x4D, failPayload, innerCt);
                return;
            }

            Console.WriteLine($"{LogTag} {ctx.Remote} CharDelete uid=0x{charId:X16} OK — sending CHAR_DELETE_OK");
            await SendFrameAsync(ctx, Opcode.CHAR_DELETE_OK, randKey: 0x4D, Array.Empty<byte>(), innerCt);
        });

        ctx.Dispatcher.Register(Opcode.C2S_CHANNEL_CRASH_REPORT, (_, payload, _) =>
        {
            var bytes = payload.ToArray();
            string text;
            try
            {
                text = tPoseidon.Core.Common.ClientText.Encoding.GetString(bytes).TrimEnd('\0');
            }
            catch (System.ArgumentException)
            {
                var sb = new System.Text.StringBuilder(bytes.Length);
                foreach (var b in bytes)
                {
                    if (b == 0) break;
                    if (b is >= 0x20 and < 0x7F) sb.Append((char)b);
                    else if (b is 0x0A or 0x0D) sb.Append((char)b);
                    else sb.Append('?');
                }
                text = sb.ToString();
            }
            Console.WriteLine($"{LogTag} {ctx.Remote} CLIENT CRASH ({payload.Length}B):\n{text}");
            return Task.CompletedTask;
        });

        ctx.Dispatcher.Register(Opcode.C2S_CHANNEL_ANTIDEBUG_TELEMETRY, (_, _, _) => Task.CompletedTask);

        ctx.Dispatcher.Register(Opcode.C2S_SESSION_CONTROL_ACK, (_, _, _) => Task.CompletedTask);

        return Task.CompletedTask;

        static async Task RunHeartbeatAsync(S2SessionContext c, ClientHeartbeat beat, CancellationToken ct)
        {
            try
            {
                await beat.RunAsync(
                    pingCt => SendFrameAsync(c, Opcode.HEARTBEAT_ACK, randKey: 0x12, Array.Empty<byte>(), pingCt),
                    async () =>
                    {
                        Console.Error.WriteLine($"{LogTag} {c.Remote} heartbeat time over: no 0x07EC answer within {beat.AnswerTimeout.TotalMilliseconds:0}ms; closing");
                        await c.Session.DisposeAsync().ConfigureAwait(false);
                    },
                    () => c.IsConnected,
                    ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or System.Net.Sockets.SocketException)
            {
                Console.Error.WriteLine($"{LogTag} {c.Remote} heartbeat stopped: {ex.Message}");
            }
        }

        static Task SendFrameAsync(S2SessionContext c, Opcode op, byte randKey, byte[] payload, CancellationToken ct)
            => SendRawFrameAsync(c, (ushort)op, randKey, payload, ct);

        static async Task SendRawFrameAsync(S2SessionContext c, ushort op, byte randKey, byte[] payload, CancellationToken ct)
        {
            var frame = c.Cipher.Encrypt(
                opcode: op,
                sequence: 0,
                randKey: randKey,
                flags: PackingFlags.PayloadEncrypted,
                plaintextPayload: payload,
                session: c.SessionInfo);
            await c.Session.SendAsync(frame, ct).ConfigureAwait(false);
        }
    },
};

using var stopSignal = S2ServerHost.WireCtrlC();

tPoseidon.Core.InterServer.MasterServerClient? masterClient = null;
try
{
    var meshPassword = SiblingIdParser.Parse(kr2014LoginIni?.GetString("MasterServerID") ?? string.Empty).Password;
    masterClient = await tPoseidon.Core.InterServer.MasterServerRegistration.ConnectAndRegisterAsync(
        ServerEndpointConfig.MasterHost,
        ServerEndpointConfig.MasterPort,
        tPoseidon.Core.InterServer.ServerIdentity.Login1,
        msg => Console.WriteLine(msg),
        stopSignal.Token,
        onBrokeredFrame: null,
        password: meshPassword).ConfigureAwait(false);

    if (Environment.GetEnvironmentVariable("MT_CHAR_VIA_MASTER") == "1")
    {
        var routedCharStore = new tPoseidon.Core.InterServer.MasterRoutedCharStore(
            masterClient,
            new PostgresCharStore(pg.DataSource),
            tPoseidon.Core.InterServer.ServerIdentity.Login1.ServerIndex,
            msg => Console.WriteLine($"{LogTag} {msg}"));
        accountStore = new PostgresAccountStore(pg.DataSource, charStore: routedCharStore);
        Console.WriteLine($"{LogTag} char store: master-routed (Pass 4) with PostgresCharStore fallback; accountStore rewrapped to delegate char ops");
    }
    else
    {
        Console.WriteLine($"{LogTag} char store: direct PostgresCharStore via IAccountStore (set MT_CHAR_VIA_MASTER=1 to route via Master)");
    }
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Console.Error.WriteLine($"{LogTag} MasterServer registration failed: {ex.Message} — continuing without mesh");
}

try
{
    await host.RunAsync(stopSignal.Token);
}
finally
{
    if (masterClient is not null)
    {
        await masterClient.DisposeAsync().ConfigureAwait(false);
    }
}
