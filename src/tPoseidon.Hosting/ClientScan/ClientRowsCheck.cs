namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientRowsCheck : IClientCheck
{
    public const string CheckId = "libconfig-rows";

    private readonly IReadOnlyList<LibConfigRowFix> _fixes;

    public ClientRowsCheck(IReadOnlyList<LibConfigRowFix> fixes)
    {
        ArgumentNullException.ThrowIfNull(fixes);
        _fixes = fixes;
    }

    public string Id => CheckId;

    public bool ReadsWorldData => true;

    public IReadOnlyList<ScanFinding> Run(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!File.Exists(context.Plan.LibConfig))
        {
            return [];
        }

        var findings = new List<ScanFinding>();
        foreach (var fix in _fixes)
        {
            var check = fix.Check(context);
            findings.Add(check.Needed ? ScanFinding.Warning(Id, check.Reason, fix.Id) : ScanFinding.Pass(Id, check.Reason));
        }

        return findings;
    }
}
