using tPoseidon.Core.Common;

namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientDataFilesCheck : IClientCheck
{
    public const string CheckId = "data-files";
    public const string LevelExpChartFile = "gulim14.fnt2";
    public const string StatusChartFile = "gulim13.fnt2";

    private const string MissingPrefix = "no ";
    private const string MapFolderProblem = "no data/map folder";

    public string Id => CheckId;

    public bool ReadsWorldData => false;

    public IReadOnlyList<ScanFinding> Run(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Directory.Exists(context.ClientFolder))
        {
            return [];
        }

        if (!Directory.Exists(context.DataFolder))
        {
            return [ScanFinding.Problem(Id, $"There is no data folder in {context.ClientFolder}.")];
        }

        var findings = new List<ScanFinding>();
        var plan = context.Plan;
        foreach (var copy in plan.Copies)
        {
            findings.Add(ScanFinding.Warning(
                Id,
                $"data/{Path.GetFileName(copy.Target)} is missing; it can be made from {Path.GetFileName(copy.Source)}.",
                LibConfigFilesFix.FixId));
        }

        foreach (var problem in ClientFolder.Check(context.ClientFolder).Problems.Where(problem => problem.StartsWith("no data/", StringComparison.Ordinal) && problem != MapFolderProblem))
        {
            findings.Add(ScanFinding.Problem(Id, $"Missing {problem[MissingPrefix.Length..]}."));
        }

        if (findings.Count == 0)
        {
            findings.Add(ScanFinding.Pass(Id, $"{plan.Explanation} The libconfig, index and font files are all present."));
        }

        foreach (var chart in new[] { LevelExpChartFile, StatusChartFile })
        {
            findings.Add(ClientFiles.Exists(Path.Combine(context.DataFolder, chart))
                ? ScanFinding.Pass(Id, $"data/{chart} is present.")
                : ScanFinding.Problem(Id, $"data/{chart} is missing; the game server reads its level and stat charts from it."));
        }

        findings.Add(Directory.Exists(context.MapFolder)
            ? ScanFinding.Pass(Id, $"The map folder holds {Directory.EnumerateFiles(context.MapFolder).Count()} files.")
            : ScanFinding.Problem(Id, "There is no data/map folder; the server builds its world from it."));
        return findings;
    }
}
