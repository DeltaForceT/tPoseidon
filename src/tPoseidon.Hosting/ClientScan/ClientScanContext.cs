using System.Xml;

namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientScanContext
{
    private readonly Lazy<string?> _executable;

    public ClientScanContext(string clientFolder, string service, string? serverAddress, int lobbyPort, Func<string, bool>? isRunning = null, Func<string>? libConfigCopy = null, int shopPort = 0)
    {
        _libConfigCopy = libConfigCopy;
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFolder);
        ClientFolder = clientFolder;
        Service = string.IsNullOrWhiteSpace(service) ? Hosting.ClientFolder.AutomaticService : service;
        ServerAddress = string.IsNullOrWhiteSpace(serverAddress) ? null : serverAddress.Trim();
        LobbyPort = lobbyPort;
        ShopPort = shopPort;
        IsRunning = isRunning ?? RunningClient.IsRunning;
        _executable = new Lazy<string?>(FindExecutable);
    }

    public string ClientFolder { get; }

    private readonly Func<string>? _libConfigCopy;

    public string Service { get; }

    public string? ServerAddress { get; }

    public int LobbyPort { get; }

    public int ShopPort { get; }

    public Func<string, bool> IsRunning { get; }

    public string? ExecutablePath => _executable.Value;

    public string DataFolder => Hosting.ClientFolder.DataFolder(ClientFolder);

    public string MapFolder => Hosting.ClientFolder.MapFolder(ClientFolder);

    public ClientLaunchPlan Plan => MakePlan();

    public static ClientScanContext For(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var address = settings.Network.PublicAddress;
        return new ClientScanContext(settings.ClientFolder, settings.Launch.ServiceName, address, settings.Ports.Lobby, shopPort: ShopPortOf(settings));
    }

    public static ClientScanContext For(HostSettings settings, InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new ClientScanContext(settings.ClientFolder, settings.Launch.ServiceName, settings.Network.PublicAddress, settings.Ports.Lobby, libConfigCopy: () => ServerClientData.Ensure(paths, settings), shopPort: ShopPortOf(settings));
    }

    private static int ShopPortOf(HostSettings settings) => settings.ShopEnabled ? settings.Ports.Shop : 0;

    public static ClientScanContext ForFolder(string clientFolder) =>
        new(clientFolder, Hosting.ClientFolder.AutomaticService, null, 0);

    private ClientLaunchPlan MakePlan()
    {
        try
        {
            var plan = Hosting.ClientFolder.Plan(ClientFolder, Service);
            return _libConfigCopy is null ? plan : plan with { LibConfig = _libConfigCopy() };
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or XmlException)
        {
            throw new HostingException($"A libconfig file in {ClientFolder} is damaged: {ex.Message}", ex);
        }
    }

    private string? FindExecutable()
    {
        var check = Hosting.ClientFolder.Check(ClientFolder);
        return check.Executable.Length == 0 ? null : Path.Combine(ClientFolder, check.Executable);
    }
}
