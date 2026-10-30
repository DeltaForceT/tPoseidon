using System.Xml;

namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientScanner
{
    private readonly IReadOnlyList<IClientCheck> _checks;
    private readonly IReadOnlyList<IClientFix> _fixes;

    public ClientScanner(IEnumerable<IClientCheck> checks, IEnumerable<IClientFix> fixes)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(fixes);
        _checks = checks.ToList();
        _fixes = fixes.ToList();
    }

    public static ClientScanner Default { get; } = Create();

    public static ClientScanner Create(ClientPatchCatalog? catalog = null, IReadOnlyList<LibConfigRowFix>? rowFixes = null)
    {
        var patches = catalog ?? ClientPatchCatalog.Default;
        var rows = rowFixes ?? [LibConfigRowFix.ShadowSanctuary, LibConfigRowFix.ReferencedMaps];
        return new ClientScanner(
            [new ClientProgramCheck(), new ClientDataFilesCheck(), new ClientAddressCheck(), new ClientRegistryCheck(), new ClientRowsCheck(rows), new ClientWorldCheck(patches, rows), new ClientRunningCheck()],
            new IClientFix[] { new LibConfigFilesFix(), new AddressPatchFix() }.Concat(rows).Append(new Md3PointsFix(patches)));
    }

    public IReadOnlyList<IClientFix> Fixes => _fixes;

    public ScanReport Scan(ClientScanContext context, bool includeWorld = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        var findings = _checks
            .Where(check => includeWorld || !check.ReadsWorldData)
            .SelectMany(check => RunCheck(check, context))
            .DistinctBy(finding => (finding.Status, finding.Message, finding.Group))
            .ToList();
        return new ScanReport(findings, _fixes.ToDictionary(fix => fix.Id, fix => fix.Title, StringComparer.Ordinal));
    }

    private static IReadOnlyList<ScanFinding> RunCheck(IClientCheck check, ClientScanContext context)
    {
        try
        {
            return check.Run(context);
        }
        catch (HostingException ex)
        {
            return [ScanFinding.Problem(check.Id, ex.Summary)];
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or XmlException)
        {
            return [ScanFinding.Problem(check.Id, $"The {check.Id} check could not run because a client file is damaged: {ex.Message}")];
        }
    }

    public bool CanRestore(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return _fixes.Any(fix => fix.CanRestore(context));
        }
        catch (HostingException)
        {
            return false;
        }
    }

    public IReadOnlyList<FixResult> Fix(ClientScanContext context, IReadOnlyCollection<string>? fixIds = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireClientClosed(context);
        var wanted = fixIds ?? Scan(context).FixableIds;
        var unknown = wanted.FirstOrDefault(id => _fixes.All(fix => fix.Id != id));
        if (unknown is not null)
        {
            throw new HostingException($"There is no client fix called '{unknown}'.");
        }

        return _fixes.Where(fix => wanted.Contains(fix.Id, StringComparer.Ordinal))
            .Select(fix => new FixResult(fix.Id, fix.Title, fix.Apply(context)))
            .ToList();
    }

    public IReadOnlyList<FixResult> Restore(ClientScanContext context, IReadOnlyCollection<string>? fixIds = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireClientClosed(context);
        return _fixes.Where(fix => fixIds is null || fixIds.Contains(fix.Id, StringComparer.Ordinal))
            .Where(fix => fix.CanRestore(context))
            .Select(fix => new FixResult(fix.Id, fix.Title, fix.Restore(context)))
            .ToList();
    }

    private static void RequireClientClosed(ClientScanContext context)
    {
        if (context.ExecutablePath is { } executable && context.IsRunning(executable))
        {
            throw new PlayFailure(PlayFailureReason.ClientRunning, "The game client is running from this folder.", "Close the game client, then try again.");
        }
    }
}
