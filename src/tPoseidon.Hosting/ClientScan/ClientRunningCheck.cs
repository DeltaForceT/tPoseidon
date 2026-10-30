namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientRunningCheck : IClientCheck
{
    public const string CheckId = "running";

    public string Id => CheckId;

    public bool ReadsWorldData => false;

    public IReadOnlyList<ScanFinding> Run(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ExecutablePath is not { } executable)
        {
            return [];
        }

        return context.IsRunning(executable)
            ? [ScanFinding.Warning(Id, "The game client is running from this folder. Close it before fixing anything.")]
            : [ScanFinding.Pass(Id, "The game client is not running from this folder.")];
    }
}
