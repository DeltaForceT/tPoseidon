using System.Collections.Concurrent;
using System.Net;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class MasterServerHost : IAsyncDisposable
{
    private readonly IPEndPoint _endpoint;
    private readonly Action<string> _log;
    private readonly ConcurrentDictionary<ushort, MasterServerSession> _siblings = new();
    private TcpAcceptor? _acceptor;

    public MasterServerHost(IPEndPoint endpoint, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(log);
        _endpoint = endpoint;
        _log = log;
    }

    public Func<MasterServerSession, InterServerFrame, CancellationToken, Task>? OnFrame { get; set; }

    public IReadOnlyCollection<MasterServerSession> Siblings => (IReadOnlyCollection<MasterServerSession>)_siblings.Values;

    public void Start()
    {
        if (_acceptor is not null)
        {
            throw new InvalidOperationException("MasterServerHost already started.");
        }

        _acceptor = new TcpAcceptor(_endpoint, HandleSessionAsync);
        _acceptor.Start();
        _log($"[master] listening on {_endpoint}");
    }

    public bool TryGetSibling(ushort serverIndex, out MasterServerSession session) =>
        _siblings.TryGetValue(serverIndex, out session!);

    private async Task HandleSessionAsync(TcpClientSession tcp, CancellationToken ct)
    {
        var session = new MasterServerSession(tcp);
        try
        {
            await foreach (var raw in tcp.ReadPacketsAsync(ct).ConfigureAwait(false))
            {
                InterServerFrame frame;
                try
                {
                    frame = InterServerFrame.Decode(raw);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log($"[master] {tcp.RemoteEndpoint} bad inter-server frame: {ex.Message}");
                    continue;
                }

                session.RegisteredServerIndex ??= frame.Header.SourceServerIndex;
                if (session.RegisteredServerIndex is { } idx)
                {
                    _siblings[idx] = session;
                }

                var handler = OnFrame;
                if (handler is not null)
                {
                    await handler(session, frame, ct).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (session.RegisteredServerIndex is { } idx)
            {
                _siblings.TryRemove(idx, out _);
                _log($"[master] sibling {idx} disconnected");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_acceptor is not null)
        {
            await _acceptor.DisposeAsync().ConfigureAwait(false);
        }
    }
}

public sealed class MasterServerSession
{
    private readonly TcpClientSession _tcp;

    public MasterServerSession(TcpClientSession tcp)
    {
        ArgumentNullException.ThrowIfNull(tcp);
        _tcp = tcp;
    }

    public ushort? RegisteredServerIndex { get; set; }

    public IPEndPoint? RemoteEndpoint => _tcp.RemoteEndpoint;

    public Task SendAsync(InterServerFrame frame, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return _tcp.SendAsync(frame.Encode(), ct);
    }
}
