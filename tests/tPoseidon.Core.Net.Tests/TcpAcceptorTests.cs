using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Net;
using Xunit;

namespace tPoseidon.Core.Net.Tests;

public sealed class TcpAcceptorTests
{
    [Fact]
    public async Task Acceptor_ReceivesWholePacketFromClient()
    {
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                await foreach (var frame in session.ReadPacketsAsync(ct))
                {
                    received.TrySetResult(frame);
                    return;
                }
            });

        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var packet = MakePacket([0xDE, 0xAD, 0xBE, 0xEF]);
        await client.GetStream().WriteAsync(packet);

        var frame = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(packet, frame);
    }

    [Fact]
    public async Task Acceptor_ReassemblesPacketDeliveredInChunks()
    {
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                await foreach (var frame in session.ReadPacketsAsync(ct))
                {
                    received.TrySetResult(frame);
                    return;
                }
            });

        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();

        var packet = MakePacket([0x01, 0x02, 0x03, 0x04, 0x05]);

        await stream.WriteAsync(packet.AsMemory(0, 2));
        await stream.FlushAsync();
        await Task.Delay(25);
        await stream.WriteAsync(packet.AsMemory(2, 3));
        await stream.FlushAsync();
        await Task.Delay(25);
        await stream.WriteAsync(packet.AsMemory(5));

        var frame = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(packet, frame);
    }

    [Fact]
    public async Task Acceptor_HandlesMultipleConcurrentClients()
    {
        const int clientCount = 10;
        var countdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var remaining = clientCount;

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                await foreach (var _ in session.ReadPacketsAsync(ct))
                {
                    if (Interlocked.Decrement(ref remaining) == 0)
                    {
                        countdown.TrySetResult(true);
                    }

                    return;
                }
            });

        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        var tasks = Enumerable.Range(0, clientCount).Select(async i =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            var packet = MakePacket([(byte)i]);
            await client.GetStream().WriteAsync(packet);
            await client.GetStream().FlushAsync();
            await Task.Delay(100);
        });

        await Task.WhenAll(tasks);

        await countdown.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Acceptor_SessionHandlerExits_WhenPeerClosesConnection()
    {
        var sessionExited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                await foreach (var _ in session.ReadPacketsAsync(ct))
                {
                }
                sessionExited.TrySetResult(true);
            });

        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        using (var client = new TcpClient())
        {
            await client.ConnectAsync(IPAddress.Loopback, port);
        }

        await sessionExited.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Acceptor_StartTwice_Throws()
    {
        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            (_, _) => Task.CompletedTask);

        acceptor.Start();

        Assert.Throws<InvalidOperationException>(() => acceptor.Start());
    }

    private static byte[] MakePacket(ReadOnlySpan<byte> payload)
    {
        var total = checked((ushort)(payload.Length + 2));
        var buffer = new byte[total];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, total);
        payload.CopyTo(buffer.AsSpan(2));
        return buffer;
    }
}
