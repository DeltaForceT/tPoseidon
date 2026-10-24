using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net;

public sealed class TcpClientSession : IAsyncDisposable
{
    private const int ReceiveBufferBytes = 5888;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly PacketFramer _framer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile bool _disposed;

    public TcpClientSession(TcpClient client, int maxPacketBytes = 65535)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _stream = client.GetStream();
        _framer = new PacketFramer(maxPacketBytes);
    }

    public SessionGuard Guard { get; internal set; } = SessionGuard.None;

    public void Abort()
    {
        try
        {
            _client.Client.LingerState = new LingerOption(true, 0);
            _client.Client.Close();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
    }

    public IPEndPoint? RemoteEndpoint
    {
        get
        {
            try { return (IPEndPoint?)_client.Client?.RemoteEndPoint; }
            catch (ObjectDisposedException) { return null; }
            catch (SocketException) { return null; }
        }
    }

    public IPEndPoint? LocalEndpoint
    {
        get
        {
            try { return (IPEndPoint?)_client.Client?.LocalEndPoint; }
            catch (ObjectDisposedException) { return null; }
            catch (SocketException) { return null; }
        }
    }

    public bool IsConnected
    {
        get
        {
            try
            {
                var s = _client.Client;
                if (s is null || !s.Connected)
                {
                    return false;
                }
                return !(s.Poll(0, SelectMode.SelectRead) && s.Available == 0);
            }
            catch (ObjectDisposedException) { return false; }
            catch (SocketException) { return false; }
        }
    }

        public async IAsyncEnumerable<byte[]> ReadPacketsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[ReceiveBufferBytes];

        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                yield break;
            }
            catch (ObjectDisposedException)
            {
                yield break;
            }

            if (bytesRead == 0)
            {
                yield break;
            }

            var frames = _framer.Feed(buffer.AsSpan(0, bytesRead));
            foreach (var frame in frames)
            {
                yield return frame;
            }
        }
    }

        public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadRawAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[ReceiveBufferBytes];

        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                yield break;
            }
            catch (ObjectDisposedException)
            {
                yield break;
            }

            if (bytesRead == 0) yield break;

            
            yield return new ReadOnlyMemory<byte>(buffer, 0, bytesRead).ToArray();
        }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!_disposed) _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _stream.DisposeAsync().ConfigureAwait(false);
        _client.Dispose();
        _writeLock.Dispose();
    }
}
