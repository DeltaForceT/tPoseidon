using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using Xunit;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class LoginHandshakeReplayTests
{
    [Fact]
    public async Task ServerReplaysSnapshotAfterReceivingSessionInit()
    {
        var replySnapshot = new byte[]
        {
            0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE,
            0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF,
            0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE,
            0xFE, 0xDC, 0xBA, 0x98, 0x76, 0x54, 0x32, 0x10,
        };

        var keyTable = KeyTable.Identity();
        IS2CipherDerivation derivation = new IdentityDerivation();
        var cipher = new S2PacketCipher(keyTable, derivation);

        var sessionInitFired = new TaskCompletionSource<SessionInitPacket>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                var dispatcher = new OpcodeDispatcher();
                dispatcher.Register(Opcode.SESSION_INIT, async (_, payload, innerCt) =>
                {
                    var pkt = SessionInitPacket.DecodePayload(payload.Span);
                    sessionInitFired.TrySetResult(pkt);
                    await session.SendAsync(replySnapshot, innerCt);
                });

                var sessionInfo = new SessionInfo { IsStrict = false };
                var adapter = new S2SessionAdapter(
                    session, cipher, keyTable, derivation, dispatcher, sessionInfo);
                await adapter.RunAsync(ct);
            });

        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        var sessionInitPayload = new SessionInitPacket
        {
            CharacterId = 0xDEADBEEF,
            Reserved4 = 0,
            SessionToken = 0x12345678,
        }.EncodePayload();

        var clientSession = new SessionInfo { IsStrict = false };
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

        var received = await sessionInitFired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0xDEADBEEFu, received.CharacterId);
        Assert.Equal(0x12345678u, received.SessionToken);

        var replyBuffer = new byte[replySnapshot.Length];
        var totalRead = 0;
        while (totalRead < replyBuffer.Length)
        {
            var n = await stream.ReadAsync(
                replyBuffer.AsMemory(totalRead, replyBuffer.Length - totalRead))
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (n == 0) break;
            totalRead += n;
        }

        Assert.Equal(replySnapshot.Length, totalRead);
        Assert.Equal(replySnapshot, replyBuffer);
    }

    [Fact]
    public async Task ServerHandlesMultipleSequentialClients()
    {
        var replySnapshot = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

        var keyTable = KeyTable.Identity();
        IS2CipherDerivation derivation = new IdentityDerivation();
        var cipher = new S2PacketCipher(keyTable, derivation);

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                var dispatcher = new OpcodeDispatcher();
                dispatcher.Register(Opcode.SESSION_INIT, async (_, _, innerCt) =>
                {
                    await session.SendAsync(replySnapshot, innerCt);
                });

                var sessionInfo = new SessionInfo { IsStrict = false };
                var adapter = new S2SessionAdapter(
                    session, cipher, keyTable, derivation, dispatcher, sessionInfo);
                await adapter.RunAsync(ct);
            });

        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        for (var i = 0; i < 3; i++)
        {
            var clientSession = new SessionInfo { IsStrict = false };
            var frame = cipher.Encrypt(
                opcode: (ushort)Opcode.SESSION_INIT,
                sequence: (ushort)i,
                randKey: (byte)(0x40 + i),
                flags: PackingFlags.PayloadEncrypted,
                plaintextPayload: new SessionInitPacket
                {
                    CharacterId = (uint)(0x100 + i),
                    SessionToken = (uint)(0x200 + i),
                }.EncodePayload(),
                session: clientSession);

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            await client.GetStream().WriteAsync(frame);

            var buf = new byte[replySnapshot.Length];
            var n = await client.GetStream()
                .ReadAsync(buf.AsMemory())
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(replySnapshot.Length, n);
            Assert.Equal(replySnapshot, buf);
        }
    }
}
