using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net;

public sealed class TcpAcceptor : IAsyncDisposable
{
    public const int DefaultBacklog = 139;

    private readonly IPEndPoint _endpoint;
    private readonly Func<TcpClientSession, CancellationToken, Task> _sessionHandler;
    private readonly int _backlog;
    private readonly ConnectionGuard? _guard;
    private TcpListener? _listener;
    private CancellationTokenSource? _acceptCts;
    private Task? _acceptLoop;

    public TcpAcceptor(
        IPEndPoint endpoint,
        Func<TcpClientSession, CancellationToken, Task> sessionHandler,
        int backlog = DefaultBacklog,
        ConnectionGuard? guard = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(sessionHandler);

        ArgumentOutOfRangeException.ThrowIfLessThan(backlog, 1);

        _endpoint = endpoint;
        _sessionHandler = sessionHandler;
        _backlog = backlog;
        _guard = guard;
    }

    public IPEndPoint? BoundEndpoint => (IPEndPoint?)_listener?.LocalEndpoint;

    public void Start()
    {
        if (_listener is not null)
        {
            throw new InvalidOperationException("Acceptor already started.");
        }

        _listener = new TcpListener(_endpoint);
        _listener.Start(_backlog);

        _acceptCts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_acceptCts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                continue;
            }

            var guard = SessionGuard.None;
            if (_guard is not null)
            {
                var admission = _guard.Admit(RemoteAddress(client));
                if (admission.Session is null)
                {
                    Refuse(client);
                    continue;
                }

                guard = admission.Session;
            }

            _ = Task.Run(() => RunSessionAsync(client, guard, cancellationToken), cancellationToken);
        }
    }

    private static IPAddress RemoteAddress(TcpClient client)
    {
        try
        {
            return (client.Client.RemoteEndPoint as IPEndPoint)?.Address ?? IPAddress.None;
        }
        catch (ObjectDisposedException)
        {
            return IPAddress.None;
        }
        catch (SocketException)
        {
            return IPAddress.None;
        }
    }

    private static void Refuse(TcpClient client)
    {
        try
        {
            client.Client.LingerState = new LingerOption(true, 0);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }

        client.Dispose();
    }

    private async Task RunSessionAsync(TcpClient client, SessionGuard guard, CancellationToken cancellationToken)
    {
        try
        {
            await using var session = new TcpClientSession(client) { Guard = guard };
            guard.Bind(session.Abort);

            try
            {
                await _sessionHandler(session, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception)
            {

            
            }
        }
        finally
        {
            _guard?.Release(guard);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_acceptCts is not null)
        {
            await _acceptCts.CancelAsync().ConfigureAwait(false);
        }

        _listener?.Stop();

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch
            {

            }
        }

        _acceptCts?.Dispose();
        _listener = null;
        _acceptCts = null;
        _acceptLoop = null;
    }
}
