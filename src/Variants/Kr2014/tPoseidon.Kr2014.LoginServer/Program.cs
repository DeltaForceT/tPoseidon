using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using SessionOpenPacket = tPoseidon.Kr2014.Wire.SessionOpenPacket;

ConsoleFileLog.EnableIfConfigured();
var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : ServerEndpointConfig.LoginPort;
const string LogTag = "[login]";
Console.WriteLine($"{LogTag} {ServerEndpointConfig.Summary()}");

Console.WriteLine($"{LogTag} reply-bundle encoder ready (4 typed frames)");

var pg = await PostgresStoreBootstrap.InitializeAsync();
var accountStore = pg.Store;
Console.WriteLine($"{LogTag} account store ready (Postgres-backed); SessionInit validator listening on port {port}");

var loginHandler = new LoginSessionHandler(accountStore);

var host = new S2ServerHost
{
    LogTag = LogTag,
    Port = port,
    KeyTablePath = S2ServerHost.DefaultKeyTablePath(),
    OnSessionAccepted = (ctx, _) =>
    {
        ctx.Dispatcher.Register(Opcode.SESSION_INIT, async (_, payload, innerCt) =>
        {
            var result = loginHandler.Handle(payload.Span);
            if (!result.Accepted)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} SessionInit rejected ({result.RejectReason}): {result.Message}");
                return;
            }

            Console.WriteLine($"{LogTag} {ctx.Remote} {result.Message}");

            

            

            

            

            var loginPeerIp = ctx.Remote?.Address ?? System.Net.IPAddress.None;
            accountStore.RefreshLatestIp(result.AccountId, loginPeerIp);

            

            

            await SendFrameAsync(ctx, Opcode.SESSION_OPEN, randKey: 0x5A,
                SessionOpenPacket.EncodePayload(), innerCt);
            await SendFrameAsync(ctx, Opcode.S2C_OP_1773_TBD, randKey: 0x08,
                PartyDataUpdatePacket.NoParty.EncodePayload(), innerCt);
            await SendFrameAsync(ctx, Opcode.CHAR_DATA, randKey: 0xA3,
                CharDataPacket.SnapshotDefault.EncodePayload(), innerCt);
            await SendFrameAsync(ctx, Opcode.S2C_OP_2159_TBD, randKey: 0x99,
                PassiveSkillGainPacket.SnapshotDefault.EncodePayload(), innerCt);
            Console.WriteLine($"{LogTag} {ctx.Remote} sent 4-frame typed reply bundle");

            async Task SendFrameAsync(S2SessionContext c, Opcode op, byte randKey, byte[] payload, CancellationToken ct)
            {
                var frame = c.Cipher.Encrypt(
                    opcode: (ushort)op,
                    sequence: 0,
                    randKey: randKey,
                    flags: PackingFlags.PayloadEncrypted,
                    plaintextPayload: payload,
                    session: c.SessionInfo);
                await c.Session.SendAsync(frame, ct).ConfigureAwait(false);
            }
        });

        

        

        RegisterHandshakeStub(ctx.Dispatcher, Opcode.HANDSHAKE_STEP_1, "STEP_1");
        RegisterHandshakeStub(ctx.Dispatcher, Opcode.HANDSHAKE_STEP_2, "STEP_2");
        RegisterHandshakeStub(ctx.Dispatcher, Opcode.HANDSHAKE_STEP_4, "STEP_4");
        RegisterHandshakeStub(ctx.Dispatcher, Opcode.HANDSHAKE_STEP_5, "STEP_5");
        RegisterHandshakeStub(ctx.Dispatcher, Opcode.HANDSHAKE_FINAL, "FINAL");

        
        RegisterHandshakeStub(ctx.Dispatcher, Opcode.S2C_OP_2712_TBD, "STEP_3 (alias DRILL_START)");

        

        

        
        RegisterHandshakeStub(ctx.Dispatcher, Opcode.SESSION_PARAM_3, "SESSION_PARAM_3 (kr post-warp)");

        ctx.Dispatcher.Register(Opcode.CHAR_SELECT, (_, payload, _) =>
        {
            try
            {
                var pkt = tPoseidon.Core.Net.Packets.CharSelectPacket.DecodePayload(payload.Span);
                Console.WriteLine($"{LogTag} {ctx.Remote} CHAR_SELECT char_id=0x{pkt.CharacterId:X8}");
            }
            catch (InvalidPacketException ex)
            {
                Console.Error.WriteLine($"{LogTag} {ctx.Remote} bad CHAR_SELECT: {ex.Message}");
            }
            return Task.CompletedTask;
        });

        return Task.CompletedTask;

        void RegisterHandshakeStub(OpcodeDispatcher d, Opcode op, string label)
        {
            d.Register(op, (_, _, _) =>
            {
                Console.WriteLine($"{LogTag} {ctx.Remote} handshake {label} (0x{(ushort)op:X4}) — not yet implemented");
                return Task.CompletedTask;
            });
        }
    },
};

using var stopSignal = S2ServerHost.WireCtrlC();
await host.RunAsync(stopSignal.Token);
