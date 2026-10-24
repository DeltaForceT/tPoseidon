using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Hosting.GameMaster;

public sealed record GmToolEndpoint(string Host, int Port, string KeyTablePath)
{
    public const string LocalHost = "127.0.0.1";

    public static GmToolEndpoint For(HostSettings settings, InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(paths);
        return new GmToolEndpoint(LocalHost, settings.Ports.System, paths.KeyTable);
    }

    public bool IsLocal =>
        string.Equals(Host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(Host, out var address) && IPAddress.IsLoopback(address));

    public override string ToString() => $"{Host}:{Port}";
}

public sealed record GmLogin(string Character, string Password);

public sealed record GmOutcome(int ExitCode, IReadOnlyList<string> Lines, bool Refused = false, IReadOnlyList<string>? Answers = null)
{
    public const int Done = 0;
    public const int NotDone = 4;

    public bool Ok => ExitCode == Done;

    public IReadOnlyList<string> Results => Answers ?? Lines;
}

public sealed record GmToolCall(GameMasterToolCommand Command, GameMasterToolRequest Request);

public interface IGmToolTransport
{
    Task<IReadOnlyList<ToolReply>> SendAsync(GmToolEndpoint endpoint, GmLogin login, GameMasterToolRequest request, bool expectsReply, CancellationToken ct);

    Task<bool> LoginAsync(GmToolEndpoint endpoint, GmLogin login, CancellationToken ct);

    bool? IsListening(GmToolEndpoint endpoint);
}

public sealed class GmToolTransport : IGmToolTransport
{
    public static GmToolTransport Shared { get; } = new();

    public async Task<IReadOnlyList<ToolReply>> SendAsync(GmToolEndpoint endpoint, GmLogin login, GameMasterToolRequest request, bool expectsReply, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return await GameMasterToolConnection
                .RunAsync(endpoint.Host, endpoint.Port, endpoint.KeyTablePath, login.Character, login.Password, request.Opcode, request.Body, expectsReply, GameMasterToolCommands.ClientText(), ct)
                .ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return Array.Empty<ToolReply>();
        }
    }

    public async Task<bool> LoginAsync(GmToolEndpoint endpoint, GmLogin login, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(login);
        try
        {
            return await GameMasterToolConnection.LoginAsync(endpoint.Host, endpoint.Port, endpoint.KeyTablePath, login.Character, login.Password, GameMasterToolCommands.ClientText(), ct).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public bool? IsListening(GmToolEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return endpoint.IsLocal ? PortProbe.IsListening(endpoint.Port, endpoint.Host) : null;
    }
}

public static class GmTool
{
    public static GmToolCall Prepare(GameMasterToolCommand command, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(arguments);
        var request = GameMasterToolCommands.Build(command.Name, arguments, GameMasterToolCommands.ClientText())
            ?? throw new HostingException($"Wrong arguments for '{command.Name}'. Usage: tposeidon gm {command.Name} {command.Arguments}");
        return new GmToolCall(command, request);
    }

    public static async Task<GmOutcome> RunAsync(IGmToolTransport transport, GmToolEndpoint endpoint, GmLogin login, GmToolCall call, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(call);
        var replies = await transport.SendAsync(endpoint, login, call.Request, call.Command.ExpectsReply, ct).ConfigureAwait(false);
        if (replies.Count == 0)
        {
            throw new HostingException($"No answer: GM login was refused or the system server is not reachable at {endpoint.Host}:{endpoint.Port}.");
        }

        return new GmOutcome(
            replies.Any(reply => reply.Opcode == GameMasterFrames.LoginOk) ? GmOutcome.Done : GmOutcome.NotDone,
            replies.Select(GameMasterToolConnection.Describe).ToList(),
            replies.Any(GameMasterToolConnection.IsRefusal),
            replies.Where(reply => reply.Opcode != GameMasterFrames.LoginOk).Select(GameMasterToolConnection.Describe).ToList());
    }
}
