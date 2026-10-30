namespace tPoseidon.Hosting.ClientScan;

public enum ScanStatus
{
    Pass,
    Info,
    Warning,
    Problem,
}

public sealed record ScanFinding(string CheckId, ScanStatus Status, string Message, string? FixId = null, string? Group = null)
{
    public static ScanFinding Pass(string checkId, string message, string? group = null) => new(checkId, ScanStatus.Pass, message, null, group);

    public static ScanFinding Info(string checkId, string message, string? group = null) => new(checkId, ScanStatus.Info, message, null, group);

    public static ScanFinding Warning(string checkId, string message, string? fixId = null, string? group = null) => new(checkId, ScanStatus.Warning, message, fixId, group);

    public static ScanFinding Problem(string checkId, string message, string? fixId = null, string? group = null) => new(checkId, ScanStatus.Problem, message, fixId, group);
}

public sealed record FixOutcome(bool Changed, string Message);

public sealed record FixResult(string FixId, string Title, FixOutcome Outcome);

public interface IClientCheck
{
    string Id { get; }

    bool ReadsWorldData { get; }

    IReadOnlyList<ScanFinding> Run(ClientScanContext context);
}

public interface IClientFix
{
    string Id { get; }

    string Title { get; }

    bool CanRestore(ClientScanContext context);

    FixOutcome Apply(ClientScanContext context);

    FixOutcome Restore(ClientScanContext context);
}

public sealed class ScanReport
{
    public ScanReport(IReadOnlyList<ScanFinding> findings, IReadOnlyDictionary<string, string> fixTitles)
    {
        Findings = findings;
        FixTitles = fixTitles;
    }

    public IReadOnlyList<ScanFinding> Findings { get; }

    public IReadOnlyDictionary<string, string> FixTitles { get; }

    public int Passed => Count(ScanStatus.Pass);

    public int Notes => Count(ScanStatus.Info);

    public int Warnings => Count(ScanStatus.Warning);

    public int Problems => Count(ScanStatus.Problem);

    public bool Blocking => Problems > 0;

    public IReadOnlyList<string> FixableIds =>
        Findings.Where(finding => finding.FixId is not null && finding.Status is ScanStatus.Warning or ScanStatus.Problem)
            .Select(finding => finding.FixId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    public IReadOnlyList<IGrouping<string, ScanFinding>> ByGroup =>
        Findings.Where(finding => finding.Group is not null && finding.Status != ScanStatus.Pass)
            .GroupBy(finding => finding.Group!, StringComparer.Ordinal)
            .ToList();

    public IReadOnlyList<ScanFinding> General => Findings.Where(finding => finding.Group is null).ToList();

    public string Summary
    {
        get
        {
            var attention = Findings.Where(finding => finding.Group is not null && finding.Status is ScanStatus.Warning or ScanStatus.Problem).ToList();
            var maps = attention.Select(finding => finding.Group).Distinct(StringComparer.Ordinal).Count();
            var world = maps == 0 ? "" : $", {attention.Count} world findings on {maps} maps";
            var notes = Notes == 0 ? "" : $", {Notes} notes";
            return $"{Passed} passed, {Warnings} warnings, {Problems} problems{notes}{world}.";
        }
    }

    private int Count(ScanStatus status) => Findings.Count(finding => finding.Status == status);
}
