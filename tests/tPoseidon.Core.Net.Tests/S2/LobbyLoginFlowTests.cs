using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class LobbyLoginFlowTests
{
    private const string TestUsername = "lobbytest";
    private const string TestPassword = "lobbytestpw";

    [Fact]
    public async Task ValidCredentials_AuthenticateAndReceiveServerList()
    {
        var keyTable = KeyTable.Identity();
        IS2CipherDerivation derivation = new IdentityDerivation();
        var cipher = new S2PacketCipher(keyTable, derivation);

        var accountStore = new InMemoryAccountStore();
        var account = accountStore.RegisterAccount(TestUsername, PasswordHasher.Hash(TestPassword));

        var serverListSnapshot = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x11, 0x22 };

        SessionToken? issuedToken = null;

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                var sessionInfo = new SessionInfo { IsStrict = false };
                var dispatcher = new OpcodeDispatcher();

                dispatcher.Register(Opcode.LOGIN_REQUEST, async (_, payload, innerCt) =>
                {
                    var pkt = LoginRequestPacket.DecodePayload(payload.Span);
                    if (!accountStore.VerifyPassword(pkt.Username, pkt.Password)) return;
                    var resolved = accountStore.GetAccountByUsername(pkt.Username);
                    if (resolved is null) return;
                    issuedToken = accountStore.IssueSessionToken(resolved.AccountId, TimeSpan.FromMinutes(5));
                    await session.SendAsync(serverListSnapshot, innerCt);
                });

                var adapter = new S2SessionAdapter(
                    session, cipher, keyTable, derivation, dispatcher, sessionInfo);
                await adapter.RunAsync(ct);
            });
        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();

        var clientSession = new SessionInfo { IsStrict = false };
        var loginFrame = cipher.Encrypt(
            (ushort)Opcode.LOGIN_REQUEST, 1, 0x4F,
            PackingFlags.PayloadEncrypted,
            new LoginRequestPacket
            {
                Username = TestUsername,
                Password = TestPassword,
            }.EncodePayload(),
            clientSession);
        await stream.WriteAsync(loginFrame);

        var buf = new byte[serverListSnapshot.Length];
        var read = 0;
        while (read < buf.Length)
        {
            var n = await stream.ReadAsync(buf.AsMemory(read, buf.Length - read))
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (n == 0) break;
            read += n;
        }

        Assert.Equal(serverListSnapshot.Length, read);
        Assert.Equal(serverListSnapshot, buf);
        Assert.NotNull(issuedToken);
        Assert.Equal(account.AccountId, issuedToken.AccountId);
    }

    [Fact]
    public async Task WrongPassword_DoesNotIssueTokenOrReply()
    {
        var keyTable = KeyTable.Identity();
        IS2CipherDerivation derivation = new IdentityDerivation();
        var cipher = new S2PacketCipher(keyTable, derivation);

        var accountStore = new InMemoryAccountStore();
        accountStore.RegisterAccount(TestUsername, PasswordHasher.Hash(TestPassword));

        var replyBytes = new byte[] { 0x11, 0x22, 0x33, 0x44 };
        var tokenIssued = false;

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                var sessionInfo = new SessionInfo { IsStrict = false };
                var dispatcher = new OpcodeDispatcher();
                dispatcher.Register(Opcode.LOGIN_REQUEST, async (_, payload, innerCt) =>
                {
                    var pkt = LoginRequestPacket.DecodePayload(payload.Span);
                    if (!accountStore.VerifyPassword(pkt.Username, pkt.Password)) return;
                    var resolved = accountStore.GetAccountByUsername(pkt.Username);
                    if (resolved is null) return;
                    tokenIssued = true;
                    accountStore.IssueSessionToken(resolved.AccountId, TimeSpan.FromMinutes(5));
                    await session.SendAsync(replyBytes, innerCt);
                });

                var adapter = new S2SessionAdapter(
                    session, cipher, keyTable, derivation, dispatcher, sessionInfo);
                await adapter.RunAsync(ct);
            });
        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();

        var clientSession = new SessionInfo { IsStrict = false };
        var loginFrame = cipher.Encrypt(
            (ushort)Opcode.LOGIN_REQUEST, 1, 0x4F,
            PackingFlags.PayloadEncrypted,
            new LoginRequestPacket
            {
                Username = TestUsername,
                Password = "wrong",
            }.EncodePayload(),
            clientSession);
        await stream.WriteAsync(loginFrame);

        var buf = new byte[replyBytes.Length];
        var readTask = stream.ReadAsync(buf.AsMemory()).AsTask();
        var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(500));
        var winner = await Task.WhenAny(readTask, timeoutTask);

        Assert.Same(timeoutTask, winner);
        Assert.False(tokenIssued);
    }
}
