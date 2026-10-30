using System.Xml;

namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientScanContext
{
    private readonly Lazy<string?> _executable;

    public ClientScanContext(string clientFolder, string service, string? serverAddress, int lobbyPort, Func<string, bool>? isRunning = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFolder);
        ClientFolder = clientFolder;
        Service = string.IsNullOrWhiteSpace(service) ? Hosting.ClientFolder.AutomaticService : service;
        ServerAddress = string.IsNullOrWhiteSpace(serverAddress) ? null : serverAddress.Trim();
        LobbyPort = lobbyPort;
        IsRunning = isRunning ?? RunningClient.IsRunning;
        _executable = new Lazy<string?>(FindExecutable);
    }

    public string ClientFolder { get; }

    public string Service { get; }

    public string? ServerAddress { get; }

    public int LobbyPort { get; }

    public Func<string, bool> IsRunning { get; }

    public string? ExecutablePath => _executable.Value;

    public string DataFolder => Hosting.ClientFolder.DataFolder(ClientFolder);

    public string MapFolder => Hosting.ClientFolder.MapFolder(ClientFolder);

    public ClientLaunchPlan Plan => MakePlan();

    public static ClientScanContext For(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var address = settings.Network.PublicAddress;
        return new ClientScanContext(settings.ClientFolder, settings.Launch.ServiceName, address, settings.Ports.Lobby);
    }

    public static ClientScanContext ForFolder(string clientFolder) =>
        new(clientFolder, Hosting.ClientFolder.AutomaticService, null, 0);

    private ClientLaunchPlan MakePlan()
    {
        try
        {
            return Hosting.ClientFolder.Plan(ClientFolder, Service);
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
