using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using Xunit;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class S2SessionAdapterTests
{
    private static byte[] BuildIdentityFrameWithLengthPrefix(
        ushort opcode,
        ushort sequence,
        byte randKey,
        ReadOnlySpan<byte> payload)
    {
        var inner = new byte[9 + payload.Length + 2];
        var declared = (ushort)inner.Length;

        BinaryPrimitives.WriteUInt16LittleEndian(inner.AsSpan(0, 2), declared);
        BinaryPrimitives.WriteUInt16LittleEndian(inner.AsSpan(2, 2), opcode);
        BinaryPrimitives.WriteUInt16LittleEndian(inner.AsSpan(4, 2), sequence);
        inner[6] = randKey;
        inner[7] = 0;
        inner[8] = 0;
        payload.CopyTo(inner.AsSpan(9, payload.Length));

        ushort sum = 0;
        foreach (var b in payload) sum += b;
        BinaryPrimitives.WriteUInt16LittleEndian(inner.AsSpan(9 + payload.Length, 2), sum);
        return inner;
    }

    [Fact]
    public async Task Adapter_DecryptsAndDispatchesIncomingFrame()
    {
        var dispatcher = new OpcodeDispatcher();
        var received = new TaskCompletionSource<(Opcode, byte[])>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        dispatcher.Register(Opcode.LOGIN_RESPONSE, (op, pl, _) =>
        {
            received.TrySetResult((op, pl.ToArray()));
            return Task.CompletedTask;
        });

        var keyTable = KeyTable.Identity();
        IS2CipherDerivation derivation = new IdentityDerivation();
        var cipher = new S2PacketCipher(keyTable, derivation);

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                var sessionInfo = new SessionInfo { IsStrict = false };
                var adapter = new S2SessionAdapter(session, cipher, keyTable, derivation, dispatcher, sessionInfo);
                await adapter.RunAsync(ct);
            });

        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        var payload = new byte[] { 0x10, 0x20, 0x30 };
        var frame = BuildIdentityFrameWithLengthPrefix(
            opcode: (ushort)Opcode.LOGIN_RESPONSE,
            sequence: 1,
            randKey: 1,
            payload: payload);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await client.GetStream().WriteAsync(frame);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Opcode.LOGIN_RESPONSE, result.Item1);
        Assert.Equal(payload, result.Item2);
    }

    [Fact]
    public async Task Adapter_MalformedFrame_ClosesSessionWithoutException()
    {
        var dispatcher = new OpcodeDispatcher();
        var keyTable = KeyTable.Identity();
        IS2CipherDerivation derivation = new IdentityDerivation();
        var cipher = new S2PacketCipher(keyTable, derivation);

        var sessionClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                var sessionInfo = new SessionInfo { IsStrict = false };
                var adapter = new S2SessionAdapter(session, cipher, keyTable, derivation, dispatcher, sessionInfo);
                await adapter.RunAsync(ct);
                sessionClosed.TrySetResult(true);
            });

        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        var frame = new byte[14];
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, 2), (ushort)frame.Length);
        for (var i = 2; i < frame.Length; i++) frame[i] = 0xFF;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await client.GetStream().WriteAsync(frame);

        await sessionClosed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
