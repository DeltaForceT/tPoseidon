using tPoseidon.Hosting;

namespace tPoseidon.Launcher;

public interface ITicketService
{
    Task RequestAsync(Uri endpoint, string account, string password, CancellationToken ct);
}

public interface IClientHost
{
    bool IsRunning(string executablePath);

    string Start(HostSettings settings);
}

public sealed class HttpTicketService : ITicketService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task RequestAsync(Uri endpoint, string account, string password, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = Timeout };
        await PlayTicketClient.RequestAsync(http, endpoint, account, password, ct).ConfigureAwait(false);
    }
}

public sealed class LocalClientHost : IClientHost
{
    public bool IsRunning(string executablePath) => RunningClient.IsRunning(executablePath);

    public string Start(HostSettings settings) => GameClientLauncher.LaunchWithTicket(settings);
}
