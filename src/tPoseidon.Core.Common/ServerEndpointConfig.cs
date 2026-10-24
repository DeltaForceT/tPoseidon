using System.Net;

namespace tPoseidon.Core.Common;

public static class ServerEndpointConfig
{
    public const string DefaultPublicIp = "216.39.108.160";

    public const string InterServerLoopback = "127.0.0.1";

    public const ushort DefaultLobbyPort   = 9980;
    public const ushort DefaultChannelPort = 10006;
    public const ushort DefaultLoginPort   = 13336;
    public const ushort DefaultGamePort    = 22006;
    public const ushort DefaultGameAltPort = 23006;
    public const ushort DefaultMasterPort  = 16646;
    public const ushort DefaultSystemPort  = 13336;

    private static readonly Lazy<string?> _publicIpOverride =
        new(() =>
        {
            var raw = Environment.GetEnvironmentVariable("MT_PUBLIC_IP");
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        });

    private static readonly Lazy<ushort> _lobbyPort =
        new(() => ReadPort("MT_LOBBY_PORT", DefaultLobbyPort));

    private static readonly Lazy<ushort> _channelPort =
        new(() => ReadPort("MT_CHANNEL_PORT", DefaultChannelPort));

    private static readonly Lazy<ushort> _loginPort =
        new(() => ReadPort("MT_LOGIN_PORT", DefaultLoginPort));

    private static readonly Lazy<ushort> _gamePort =
        new(() => ReadPort("MT_GAME_PORT", DefaultGamePort));

    private static readonly Lazy<ushort> _gameAltPort =
        new(() => ReadPort("MT_GAME_ALT_PORT", DefaultGameAltPort));

    private static readonly Lazy<ushort> _masterPort =
        new(() => ReadPort("MT_MASTER_PORT", DefaultMasterPort));

    private static readonly Lazy<ushort> _systemPort =
        new(() => ReadPort("MT_SYSTEM_PORT", DefaultSystemPort));

    private static readonly Lazy<IPAddress> _clientBindAddress =
        new(() => ReadAddress("MT_BIND_ADDRESS", IPAddress.Any));

    private static readonly Lazy<IPAddress> _interServerBindAddress =
        new(() => ReadAddress("MT_INTERSERVER_BIND_ADDRESS", IPAddress.Loopback));

    private static readonly Lazy<string> _masterHost =
        new(() =>
        {
            var raw = Environment.GetEnvironmentVariable("MT_MASTER_HOST");
            return string.IsNullOrWhiteSpace(raw) ? InterServerLoopback : raw.Trim();
        });

    public static string MasterHost => _masterHost.Value;

    public static IPAddress ClientBindAddress => _clientBindAddress.Value;

    public static IPAddress InterServerBindAddress => _interServerBindAddress.Value;

    public static string PublicIp => _publicIpOverride.Value ?? DefaultPublicIp;

    public static ushort LobbyPort   => _lobbyPort.Value;
    public static ushort ChannelPort => _channelPort.Value;
    public static ushort LoginPort   => _loginPort.Value;
    public static ushort GamePort    => _gamePort.Value;
    public static ushort GameAltPort => _gameAltPort.Value;
    public static ushort MasterPort  => _masterPort.Value;
    public static ushort SystemPort  => _systemPort.Value;

    private static IPAddress ReadAddress(string envVar, IPAddress fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }
        if (!IPAddress.TryParse(raw.Trim(), out var address))
        {
            Console.Error.WriteLine($"[config] {envVar}='{raw}' is not an IP address; using {fallback}");
            return fallback;
        }
        return address;
    }

    private static ushort ReadPort(string envVar, ushort fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }
        if (!ushort.TryParse(raw, out var port) || port == 0)
        {
            Console.Error.WriteLine(
                $"[config] {envVar}='{raw}' is not a valid port (1..65535); using default {fallback}");
            return fallback;
        }
        return port;
    }

        public static string Summary() =>
        $"endpoints: {PublicIp} lobby={LobbyPort} channel={ChannelPort} login={LoginPort} game={GamePort}+{GameAltPort}";
}
