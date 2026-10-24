namespace tPoseidon.Hosting.GameMaster;

public enum GmLinkStatus
{
    NotConnected,
    Connected,
    NotSetUp,
    ServerDown,
    NoGameMaster,
    NotRegistered,
    Refused,
}

public sealed record GmLinkState(GmLinkStatus Status, string Message)
{
    public bool Connected => Status == GmLinkStatus.Connected;
}

public static class GmLink
{
    public static GmLinkState NotConnected { get; } = new(GmLinkStatus.NotConnected, "Not connected. Enter your GM character and the account password, then connect.");

    public static async Task<GmLinkState> CheckAsync(IGmToolTransport transport, GmToolEndpoint endpoint, GmLogin login, IGmAccountBook? accounts, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(login);
        if (login.Character.Trim().Length == 0)
        {
            return new GmLinkState(GmLinkStatus.NotConnected, "Enter the GM character to log in as.");
        }

        if (login.Password.Length == 0)
        {
            return new GmLinkState(GmLinkStatus.NotConnected, "Enter the password of the GM character's account.");
        }

        if (transport.IsListening(endpoint) == false)
        {
            return new GmLinkState(GmLinkStatus.ServerDown, $"The system server is not running: nothing listens on {endpoint}. Start the servers from the main window, then connect again.");
        }

        var (registered, listProblem) = accounts is null ? (null, null) : RegisteredNames(accounts);
        if (registered is { Count: 0 })
        {
            return new GmLinkState(GmLinkStatus.NoGameMaster, "No GM character is registered yet. Register one on the GM accounts tab (or with tposeidon gm register --character <name>), then connect.");
        }

        if (registered is not null && !registered.Contains(login.Character.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return new GmLinkState(GmLinkStatus.NotRegistered, $"'{login.Character.Trim()}' is not a registered GM character. Registered: {string.Join(", ", registered)}.");
        }

        if (await transport.LoginAsync(endpoint, login with { Character = login.Character.Trim() }, ct).ConfigureAwait(false))
        {
            return new GmLinkState(GmLinkStatus.Connected, $"Connected as {login.Character.Trim()} to the system server on {endpoint}.");
        }

        var refused = $"The system server on {endpoint} refused the GM login. Check the account password, and that the GM character may log in from {GmAccounts.LocalPermitIp} (see the GM accounts tab).";
        return new GmLinkState(GmLinkStatus.Refused, listProblem is null ? refused : $"{refused} The GM list could not be read either: {listProblem}");
    }

    private static (List<string>? Names, string? Problem) RegisteredNames(IGmAccountBook accounts)
    {
        try
        {
            return (accounts.List().Select(entry => entry.CharacterName).ToList(), null);
        }
        catch (HostingException ex)
        {
            return (null, ex.Message);
        }
    }
}
