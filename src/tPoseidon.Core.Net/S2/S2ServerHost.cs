using System.Net;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.S2;

public sealed class S2ServerHost
{
        public required string LogTag { get; init; }

        public required int Port { get; init; }

        public required string KeyTablePath { get; init; }

        public required Func<S2SessionContext, CancellationToken, Task> OnSessionAccepted { get; init; }

    public Func<S2SessionContext, Task>? OnSessionClosed { get; init; }

    public bool PauseAcceptedSessions { get; set; }

    public ConnectionGuard? Guard { get; init; }

    public IPAddress? BindAddress { get; init; }

    public bool SessionsLogIn { get; init; }

    public bool ClientsKeepAlive { get; init; }

    public Task<IPEndPoint> Listening => _listening.Task;

    private readonly TaskCompletionSource<IPEndPoint> _listening = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"{LogTag} tPoseidon {BuildIdentity.Current}");
        var (keyTable, derivation) = LoadCipher(KeyTablePath, LogTag);
        var cipher = new S2PacketCipher(keyTable, derivation);

        var endpoint = new IPEndPoint(BindAddress ?? ServerEndpointConfig.ClientBindAddress, Port);
        var ownedGuard = Guard is null ? ConnectionGuard.FromEnvironment(LogTag, Port) : null;
        var guard = Guard ?? ownedGuard!;
        guard.UseListener(SessionsLogIn, ClientsKeepAlive);
        Console.WriteLine($"{LogTag} {guard.Describe()}");
        ownedGuard?.Start();
        await using var guardLifetime = ownedGuard;
        await using var acceptor = new TcpAcceptor(endpoint, async (session, ct) =>
        {
            var remote = session.RemoteEndpoint;
            if (PauseAcceptedSessions)
            {
                Console.WriteLine($"{LogTag} rejected {remote}: server still warming up (PauseAcceptedSessions=true)");
                await session.DisposeAsync().ConfigureAwait(false);
                return;
            }
            Console.WriteLine($"{LogTag} accepted {remote}");

            var dispatcher = new OpcodeDispatcher();
            dispatcher.SetUnknownHandler((opcode, payload, _) =>
            {
                var hex = payload.Length == 0
                    ? "<empty>"
                    : Convert.ToHexString(payload.Span);
                Console.WriteLine($"{LogTag} {remote} UNHANDLED opcode 0x{(ushort)opcode:X4} ({opcode}) — {payload.Length} bytes : {hex}");
                return Task.CompletedTask;
            });

            var sessionInfo = new SessionInfo { IsStrict = false };
            var ctx = new S2SessionContext
            {
                Session = session,
                SessionInfo = sessionInfo,
                Dispatcher = dispatcher,
                Cipher = cipher,
                KeyTable = keyTable,
                Derivation = derivation,
                LogTag = LogTag,
                LocalPort = (ushort)Port,
            };

            try
            {
                await OnSessionAccepted(ctx, ct);

                var adapter = new S2SessionAdapter(
                    session, cipher, keyTable, derivation, dispatcher, sessionInfo);
                await adapter.RunAsync(ct);
            }
            finally
            {
                Console.WriteLine($"{LogTag} closed {remote}");
                if (OnSessionClosed is not null)
                {
                    await OnSessionClosed(ctx).ConfigureAwait(false);
                }
            }
        }, guard: guard);

        acceptor.Start();
        _listening.TrySetResult(acceptor.BoundEndpoint!);
        Console.WriteLine($"{LogTag} listening on {acceptor.BoundEndpoint}");
        Console.WriteLine($"{LogTag} press Ctrl+C to stop");

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        Console.WriteLine($"{LogTag} shutting down");
    }

    internal static (KeyTable, IS2CipherDerivation) LoadCipher(string keyTablePath, string logTag)
    {
        if (!File.Exists(keyTablePath))
        {
            Console.WriteLine($"{logTag} WARN: KeyTable not found at {LogPaths.Scrub(keyTablePath)} — using identity. Real clients will fail to handshake.");
            return (KeyTable.Identity(), new IdentityDerivation());
        }

        var bytes = File.ReadAllBytes(keyTablePath);
        if (bytes.Length != KeyTable.RequiredLength)
        {
            Console.Error.WriteLine($"{logTag} KeyTable wrong size: {bytes.Length}, want {KeyTable.RequiredLength}. Falling back to identity.");
            return (KeyTable.Identity(), new IdentityDerivation());
        }

        Console.WriteLine($"{logTag} cipher: S2 v2010 (KeyTable from {LogPaths.Scrub(keyTablePath)})");
        return (new KeyTable(bytes), new S2v2010Derivation());
    }

        public static string DefaultKeyTablePath() =>
        RepoRoot.Data("keytable-s2v2010.bin");

        public static CancellationTokenSource WireCtrlC()
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        return cts;
    }
}

public sealed class S2SessionContext
{
    public required TcpClientSession Session { get; init; }
    public required SessionInfo SessionInfo { get; init; }
    public required OpcodeDispatcher Dispatcher { get; init; }
    public required S2PacketCipher Cipher { get; init; }
    public required KeyTable KeyTable { get; init; }
    public required IS2CipherDerivation Derivation { get; init; }
    public required string LogTag { get; init; }

        public required ushort LocalPort { get; init; }

    public IPEndPoint? Remote => Session.RemoteEndpoint;

    public SessionGuard Guard => Session.Guard;

    public bool IsConnected => Session.IsConnected;
}
