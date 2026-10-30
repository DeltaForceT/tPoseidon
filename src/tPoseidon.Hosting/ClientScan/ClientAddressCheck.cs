namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientAddressCheck : IClientCheck
{
    public const string CheckId = "address";

    public string Id => CheckId;

    public bool ReadsWorldData => false;

    public IReadOnlyList<ScanFinding> Run(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ExecutablePath is not { } executable)
        {
            return [];
        }

        var bytes = File.ReadAllBytes(executable);
        var offsets = ClientAddressPatch.Slots.Select(lobby => ClientAddressPatch.FindSlot(bytes, lobby)).ToList();
        if (offsets.Any(offset => offset < 0))
        {
            return [];
        }

        var findings = new List<ScanFinding>();
        if (context.LobbyPort != 0 && context.LobbyPort != ClientAddressPatch.ClientLobbyPort)
        {
            findings.Add(ScanFinding.Problem(Id, $"The KR 2014 client always connects to lobby port {ClientAddressPatch.ClientLobbyPort}, but this server has its lobby port on {context.LobbyPort}."));
        }

        var current = offsets.Select(offset => ClientAddressPatch.ReadAddress(bytes, offset)).Distinct(StringComparer.Ordinal).ToList();
        var shown = string.Join(" and ", current);
        if (context.ServerAddress is null)
        {
            findings.Add(ScanFinding.Pass(Id, $"The client connects to {shown}; Play points it at your server."));
            return findings;
        }

        string target;
        try
        {
            target = ClientAddressPatch.ClientAddressFor(context.ServerAddress);
        }
        catch (HostingException ex)
        {
            findings.Add(ScanFinding.Warning(Id, ex.Message));
            return findings;
        }

        findings.Add(current.Count == 1 && string.Equals(current[0], target, StringComparison.Ordinal)
            ? ScanFinding.Pass(Id, $"The client already connects to {target}.")
            : ScanFinding.Warning(Id, $"The client connects to {shown}, but this server is {target}. Play fixes this on its own; Fix does it now and keeps the original.", AddressPatchFix.FixId));
        return findings;
    }
}
