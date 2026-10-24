using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using Xunit;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class FullLoginFlowTests
{
    private const ulong TestCharId = 0x10000001ul;

    private static (Account Account, Character Character, uint TokenValue) MakeFreshAccountWithCharacter(
        InMemoryAccountStore store,
        ulong charId,
        uint tokenValue)
    {
        var account = store.RegisterAccount("flowtest", PasswordHasher.Hash("flowtestpw"));
        var character = new Character
        {
            CharacterId = charId,
            OwnerAccountId = account.AccountId,
            Slot = 0,
            Name = "TestChar",
            Species = Species.Buffalo,
            FType = FType.Power,
            Level = 1,
        };
        store.AddCharacter(character);
        store.SeedSessionToken(tokenValue, account.AccountId, TimeSpan.FromMinutes(5));
        return (account, character, tokenValue);
    }

    [Fact]
    public async Task ValidTokenAndCharacter_TriggerSnapshotReply()
    {
        var store = new InMemoryAccountStore();
        var fixture = MakeFreshAccountWithCharacter(store, TestCharId, tokenValue: 0x12345678u);
        var loginHandler = new LoginSessionHandler(store);

        var snapshot = new byte[]
        {
            0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80,
            0x90, 0xA0, 0xB0, 0xC0, 0xD0, 0xE0, 0xF0, 0x00,
            0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
            0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x01,
        };

        var keyTable = KeyTable.Identity();
        IS2CipherDerivation derivation = new IdentityDerivation();
        var cipher = new S2PacketCipher(keyTable, derivation);

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                var dispatcher = new OpcodeDispatcher();
                dispatcher.Register(Opcode.SESSION_INIT, async (_, payload, innerCt) =>
                {
                    var result = loginHandler.Handle(payload.Span);
                    if (!result.Accepted) return;
                    await session.SendAsync(snapshot, innerCt);
                });

                var sessionInfo = new SessionInfo { IsStrict = false };
                var adapter = new S2SessionAdapter(
                    session, cipher, keyTable, derivation, dispatcher, sessionInfo);
                await adapter.RunAsync(ct);
            });
        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        var clientSession = new SessionInfo { IsStrict = false };
        var sessionInitPayload = new SessionInitPacket
        {
            CharacterId = (uint)TestCharId,
            Reserved4 = 0,
            SessionToken = fixture.TokenValue,
        }.EncodePayload();
        var encryptedFrame = cipher.Encrypt(
            opcode: (ushort)Opcode.SESSION_INIT,
            sequence: 1,
            randKey: 0x4F,
            flags: PackingFlags.PayloadEncrypted,
            plaintextPayload: sessionInitPayload,
            session: clientSession);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();
        await stream.WriteAsync(encryptedFrame);

        var buf = new byte[snapshot.Length];
        var totalRead = 0;
        while (totalRead < buf.Length)
        {
            var n = await stream.ReadAsync(buf.AsMemory(totalRead, buf.Length - totalRead))
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (n == 0) break;
            totalRead += n;
        }

        Assert.Equal(snapshot.Length, totalRead);
        Assert.Equal(snapshot, buf);
    }

    [Fact]
    public async Task BadToken_DoesNotTriggerSnapshotReply()
    {
        var store = new InMemoryAccountStore();
        var account = store.RegisterAccount("flowtest", PasswordHasher.Hash("flowtestpw"));
        var loginHandler = new LoginSessionHandler(store);

        var snapshot = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

        var keyTable = KeyTable.Identity();
        IS2CipherDerivation derivation = new IdentityDerivation();
        var cipher = new S2PacketCipher(keyTable, derivation);

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                var dispatcher = new OpcodeDispatcher();
                dispatcher.Register(Opcode.SESSION_INIT, async (_, payload, innerCt) =>
                {
                    var result = loginHandler.Handle(payload.Span);
                    if (!result.Accepted) return;
                    await session.SendAsync(snapshot, innerCt);
                });

                var sessionInfo = new SessionInfo { IsStrict = false };
                var adapter = new S2SessionAdapter(
                    session, cipher, keyTable, derivation, dispatcher, sessionInfo);
                await adapter.RunAsync(ct);
            });
        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        var clientSession = new SessionInfo { IsStrict = false };
        var sessionInitPayload = new SessionInitPacket
        {
            CharacterId = (uint)TestCharId,
            Reserved4 = 0,
            SessionToken = 0xBADBADBAu,
        }.EncodePayload();
        var encryptedFrame = cipher.Encrypt(
            opcode: (ushort)Opcode.SESSION_INIT,
            sequence: 1,
            randKey: 0x4F,
            flags: PackingFlags.PayloadEncrypted,
            plaintextPayload: sessionInitPayload,
            session: clientSession);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();
        await stream.WriteAsync(encryptedFrame);

        var buf = new byte[snapshot.Length];
        var readTask = stream.ReadAsync(buf.AsMemory()).AsTask();
        var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(500));
        var winner = await Task.WhenAny(readTask, timeoutTask);

        Assert.Same(timeoutTask, winner);
    }
}
