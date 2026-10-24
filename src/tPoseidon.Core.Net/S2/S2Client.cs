using System.Net.Sockets;
using tPoseidon.Core.Crypto.S2;

namespace tPoseidon.Core.Net.S2;

public static class S2Client
{
    public static async Task RunAsync(
        string host,
        int port,
        string keyTablePath,
        string logTag,
        Func<S2SessionContext, CancellationToken, Task> onConnected,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(onConnected);
        var (keyTable, derivation) = S2ServerHost.LoadCipher(keyTablePath, logTag);
        var cipher = new S2PacketCipher(keyTable, derivation);
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        await using var session = new TcpClientSession(client);
        var dispatcher = new OpcodeDispatcher();
        dispatcher.SetUnknownHandler((opcode, payload, _) =>
        {
            Console.WriteLine($"{logTag} {host}:{port} UNHANDLED opcode 0x{(ushort)opcode:X4} — {payload.Length} bytes");
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
            LogTag = logTag,
            LocalPort = (ushort)(session.LocalEndpoint?.Port ?? 0),
        };
        await onConnected(ctx, cancellationToken).ConfigureAwait(false);
        var adapter = new S2SessionAdapter(session, cipher, keyTable, derivation, dispatcher, sessionInfo);
        await adapter.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
