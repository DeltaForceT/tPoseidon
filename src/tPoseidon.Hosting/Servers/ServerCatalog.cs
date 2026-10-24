namespace tPoseidon.Hosting.Servers;

public sealed record ServerDefinition(
    string Name,
    string ShortName,
    string Title,
    string Assembly,
    Func<PortSettings, int> Port,
    Action<PortSettings, int> SetPort,
    TimeSpan ReadyTimeout,
    bool IsShop = false);

public static class ServerCatalog
{
    private static readonly TimeSpan ShortStart = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan GameStart = TimeSpan.FromMinutes(5);

    public static IReadOnlyList<ServerDefinition> All { get; } = new[]
    {
        new ServerDefinition("Kr-Lobby", "Lobby", "Lobby server", "tPoseidon.Kr2014.LobbyServer", ports => ports.Lobby, (ports, port) => ports.Lobby = port, ShortStart),
        new ServerDefinition("Kr-Master", "Master", "Master server", "tPoseidon.Kr2014.MasterServer", ports => ports.Master, (ports, port) => ports.Master = port, ShortStart),
        new ServerDefinition("Kr-Channel", "Channel", "Channel server", "tPoseidon.Kr2014.ChannelServer", ports => ports.Channel, (ports, port) => ports.Channel = port, ShortStart),
        new ServerDefinition("Kr-System", "System", "System server", "tPoseidon.Kr2014.SystemServer", ports => ports.System, (ports, port) => ports.System = port, ShortStart),
        new ServerDefinition("Kr-Game1", "Game 1", "Game server 1", "tPoseidon.Kr2014.GameServer", ports => ports.Game1, (ports, port) => ports.Game1 = port, GameStart),
        new ServerDefinition("Kr-Game2", "Game 2", "Game server 2", "tPoseidon.Kr2014.GameServer", ports => ports.Game2, (ports, port) => ports.Game2 = port, GameStart),
        new ServerDefinition("Kr-Shop", "Shop", "Item shop", "tPoseidon.WebShop", ports => ports.Shop, (ports, port) => ports.Shop = port, ShortStart, IsShop: true),
    };

    public static IEnumerable<ServerDefinition> Enabled(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return All.Where(server => !server.IsShop || settings.ShopEnabled);
    }

    public static IReadOnlyList<int> PlayerFacingPorts(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var ports = new List<int>
        {
            settings.Ports.Lobby,
            settings.Ports.Channel,
            settings.Ports.System,
            settings.Ports.Game1,
            settings.Ports.Game2,
        };
        if (settings.ShopEnabled)
        {
            ports.Add(settings.Ports.Shop);
        }

        return ports;
    }
}
