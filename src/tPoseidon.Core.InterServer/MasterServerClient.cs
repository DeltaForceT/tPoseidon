using System.Collections.Concurrent;
using System.Net.Sockets;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class MasterServerClient : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly ServerIdentity _self;
    private readonly Action<string> _log;
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<InterServerFrame>> _pending = new();
    private TcpClientSession? _session;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveLoop;
    private int _nextRequestId;

    public MasterServerClient(string host, int port, ServerIdentity self, Action<string> log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(log);
        _host = host;
        _port = port;
        _self = self;
        _log = log;
    }

    public Func<InterServerFrame, CancellationToken, Task>? OnFrame { get; set; }

    public bool IsConnected => _session is not null;

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (_session is not null)
        {
            throw new InvalidOperationException("MasterServerClient already connected.");
        }

        var tcp = new TcpClient();
        await tcp.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
        _session = new TcpClientSession(tcp);
        _log($"[{_self.Name}] connected to MasterServer {_host}:{_port}");

        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), CancellationToken.None);
    }

    public Task SendAsync(InterServerFrame frame, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (_session is null)
        {
            throw new InvalidOperationException("MasterServerClient is not connected.");
        }
        return _session.SendAsync(frame.Encode(), ct);
    }

    public ushort AllocateRequestId()
    {
        var next = Interlocked.Increment(ref _nextRequestId);
        var truncated = (ushort)(next & 0xFFFF);
        if (truncated == 0)
        {
            truncated = (ushort)(Interlocked.Increment(ref _nextRequestId) & 0xFFFF);
        }
        return truncated;
    }

    public async Task<InterServerFrame> SendAndAwaitReplyAsync(
        InterServerFrame request,
        HashSet<InterServerOpcode> ackOpcodes,
        TimeSpan timeout,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ackOpcodes);
        if (request.Header.RequestId == 0)
        {
            throw new ArgumentException("request must have non-zero RequestId; use AllocateRequestId()", nameof(request));
        }

        var tcs = new TaskCompletionSource<InterServerFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(request.Header.RequestId, tcs))
        {
            throw new InvalidOperationException($"RequestId 0x{request.Header.RequestId:X4} already pending");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await SendAsync(request, ct).ConfigureAwait(false);
            await using var _ = timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token)).ConfigureAwait(false);
            var reply = await tcs.Task.ConfigureAwait(false);
            if (!ackOpcodes.Contains(reply.Opcode))
            {
                throw new InvalidOperationException(
                    $"reply opcode 0x{(ushort)reply.Opcode:X4} not in expected set for request 0x{(ushort)request.Opcode:X4}");
            }
            return reply;
        }
        finally
        {
            _pending.TryRemove(request.Header.RequestId, out _);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            await foreach (var raw in _session.ReadPacketsAsync(ct).ConfigureAwait(false))
            {
                InterServerFrame frame;
                try
                {
                    frame = InterServerFrame.Decode(raw);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log($"[{_self.Name}] bad inter-server frame from master: {ex.Message}");
                    continue;
                }

                if (frame.Header.RequestId != 0 &&
                    _pending.TryRemove(frame.Header.RequestId, out var tcs))
                {
                    tcs.TrySetResult(frame);
                    continue;
                }

                var handler = OnFrame;
                if (handler is not null)
                {
                    await handler(frame, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log($"[{_self.Name}] MasterServer receive loop ended: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (_, tcs) in _pending)
        {
            tcs.TrySetException(new ObjectDisposedException(nameof(MasterServerClient), "client disposed before reply"));
        }
        _pending.Clear();

        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync().ConfigureAwait(false);
        }

        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_session is not null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
        }

        _receiveCts?.Dispose();
    }
}
