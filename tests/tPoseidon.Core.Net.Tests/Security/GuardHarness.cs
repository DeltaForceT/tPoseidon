using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Common;
using tPoseidon.Core.Common.Security;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Tests.Security;

internal sealed class GuardHarness : IAsyncDisposable
{
    public const ushort EchoOpcode = 0x0018;
    public const ushort ReplyOpcode = 0x0019;
    public const ushort LoginOpcode = 0x2CED;
    public const ushort LoginReplyOpcode = 0x2CEE;
    public const ushort PlainOpcode = 0x0075;
    public const ushort PasswordOpcode = 0x2CF0;
    public const string RightPassword = "right";
    private static readonly string KeyTablePath = S2ServerHost.DefaultKeyTablePath();
    private static readonly Lazy<(KeyTable Table, IS2CipherDerivation Derivation)> SharedCipher =
        new(() => S2ServerHost.LoadCipher(KeyTablePath, "[guard-test]"));

    private readonly CancellationTokenSource _stop;
    private readonly Task _run;

    private GuardHarness(S2ServerHost host, CancellationTokenSource stop, Task run, IPEndPoint endpoint, ConnectionGuard guard)
    {
        Host = host;
        _stop = stop;
        _run = run;
        Port = endpoint.Port;
        Guard = guard;
    }

    public S2ServerHost Host { get; }

    public ConnectionGuard Guard { get; }

    public int Port { get; }

    public static GuardSettings Settings(Action<GuardSettings>? change = null)
    {
        var settings = new GuardSettings();
        change?.Invoke(settings);
        return settings;
    }

    public static ConnectionGuard NewGuard(GuardSettings settings, List<string>? log = null, string? stateDirectory = null) =>
        new(settings, "guard-test", line => { lock (log ?? []) { log?.Add(line); } }, TimeProvider.System, trustLoopback: false, stateDirectory)
        {
            SweepInterval = TimeSpan.FromMilliseconds(100),
        };

    public static async Task<GuardHarness> StartAsync(
        ConnectionGuard guard,
        Action<S2SessionContext>? register = null,
        bool sessionsLogIn = false,
        bool clientsKeepAlive = false)
    {
        var host = new S2ServerHost
        {
            LogTag = "[guard-test]",
            Port = 0,
            BindAddress = IPAddress.Loopback,
            KeyTablePath = KeyTablePath,
            Guard = guard,
            SessionsLogIn = sessionsLogIn,
            ClientsKeepAlive = clientsKeepAlive,
            OnSessionAccepted = (ctx, _) =>
            {
                ctx.Dispatcher.Register((Opcode)EchoOpcode, async (_, payload, ct) =>
                {
                    await ctx.SendFrameAsync((Opcode)ReplyOpcode, payload.ToArray(), 0x21, ct).ConfigureAwait(false);
                });
                ctx.Dispatcher.Register((Opcode)PlainOpcode, async (_, payload, ct) =>
                {
                    await ctx.SendFrameAsync((Opcode)ReplyOpcode, payload.ToArray(), 0x23, ct).ConfigureAwait(false);
                });
                ctx.Dispatcher.Register((Opcode)PasswordOpcode, async (_, payload, ct) =>
                {
                    var text = System.Text.Encoding.ASCII.GetString(payload.Span).Split('\0');
                    var gate = ctx.Guard.CheckLogin(text[0]);
                    if (gate.LockedOut)
                    {
                        await Task.Delay(gate.Delay, ct).ConfigureAwait(false);
                        await ctx.Session.DisposeAsync().ConfigureAwait(false);
                        return;
                    }

                    if (text.Length < 2 || text[1] != RightPassword)
                    {
                        await Task.Delay(ctx.Guard.LoginFailed(text[0]), ct).ConfigureAwait(false);
                        await ctx.Session.DisposeAsync().ConfigureAwait(false);
                        return;
                    }

                    ctx.Guard.LoginSucceeded(text[0]);
                    await ctx.SendFrameAsync((Opcode)LoginReplyOpcode, [0x01], 0x22, ct).ConfigureAwait(false);
                });
                ctx.Dispatcher.Register((Opcode)LoginOpcode, async (_, _, ct) =>
                {
                    ctx.Guard.Authenticated();
                    await ctx.SendFrameAsync((Opcode)LoginReplyOpcode, [0x01], 0x22, ct).ConfigureAwait(false);
                });
                register?.Invoke(ctx);
                return Task.CompletedTask;
            },
        };
        var stop = new CancellationTokenSource();
        var run = host.RunAsync(stop.Token);
        var endpoint = await host.Listening.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        guard.Start();
        return new GuardHarness(host, stop, run, endpoint, guard);
    }

    public Task<ScriptedClient> ConnectAsync() => ScriptedClient.ConnectAsync(Port);

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        await _run.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        await Guard.DisposeAsync().ConfigureAwait(false);
        _stop.Dispose();
    }

    public static (KeyTable Table, IS2CipherDerivation Derivation) Cipher() => SharedCipher.Value;
}

internal sealed class ScriptedClient : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly S2PacketCipher _cipher;
    private readonly SessionInfo _session = new() { IsStrict = false };
    private readonly S2StreamFramer _framer;
    private readonly List<byte> _received = [];

    private ScriptedClient(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        var (table, derivation) = GuardHarness.Cipher();
        _cipher = new S2PacketCipher(table, derivation);
        _framer = new S2StreamFramer(table, derivation, _session.RecvKey);
    }

    public IReadOnlyList<byte> Received => _received;

    public static async Task<ScriptedClient> ConnectAsync(int port)
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
        return new ScriptedClient(client);
    }

    public byte[] Frame(ushort opcode, byte[] payload, byte randKey = 0x37) =>
        _cipher.Encrypt(opcode, 0, randKey, PackingFlags.PayloadEncrypted, payload, _session);

    public async Task SendAsync(ushort opcode, byte[] payload, byte randKey = 0x37) =>
        await SendRawAsync(Frame(opcode, payload, randKey)).ConfigureAwait(false);

    public async Task SendRawAsync(byte[] bytes)
    {
        await _stream.WriteAsync(bytes).ConfigureAwait(false);
        await _stream.FlushAsync().ConfigureAwait(false);
    }

    public async Task<S2DecryptedPacket?> ReceiveAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var buffer = new byte[4096];
        while (true)
        {
            if (_framer.TryDrainOne(out var frame))
            {
                var packet = _cipher.Decrypt(frame, _session);
                _framer.OnSessionKeyAdvanced(_session.RecvKey);
                return packet;
            }

            int read;
            try
            {
                read = await _stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }

            if (read == 0)
            {
                return null;
            }

            _received.AddRange(buffer.AsSpan(0, read).ToArray());
            _framer.Feed(buffer.AsSpan(0, read));
        }
    }

    public async Task<bool> WaitClosedAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var buffer = new byte[4096];
        while (true)
        {
            int read;
            try
            {
                read = await _stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (IOException)
            {
                return true;
            }

            if (read == 0)
            {
                return true;
            }

            _received.AddRange(buffer.AsSpan(0, read).ToArray());
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}
