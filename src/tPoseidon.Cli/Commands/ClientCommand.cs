using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;
using tPoseidon.Hosting.ClientScan;

namespace tPoseidon.Cli.Commands;

internal static class ClientCommand
{
    private const string Check = "check";
    private const string Fix = "fix";
    private const string FolderOption = "client";
    private const string OnlyOption = "only";

    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown(FolderOption, OnlyOption);
        var action = commandLine.Positionals.Count > 0 ? commandLine.Positionals[0] : Check;
        if (commandLine.Positionals.Count > 1)
        {
            throw new CliException($"Unexpected argument '{commandLine.Positionals[1]}'.");
        }

        var context = ContextFor(paths, commandLine.Option(FolderOption));
        return action switch
        {
            Check => RunCheck(context),
            Fix => RunFix(context, commandLine),
            _ => throw new CliException($"Unknown client action '{action}'. Use check or fix."),
        };
    }

    public static void WriteHelp()
    {
        Console.WriteLine("client options:");
        Console.WriteLine("  client check [--client <folder>]                   scan the game client folder for what the server and Play need");
        Console.WriteLine("  client fix [--client <folder>] [--only <fix>]      apply the fixes the scan offers; each keeps a backup");
        Console.WriteLine("  client fix --restore [--client <folder>] [--only <fix>]   put the backed-up originals back");
    }

    public static ClientScanContext ContextFor(InstallPaths paths, string? folderOption)
    {
        var settings = HostSettings.Load(paths);
        if (folderOption is not null)
        {
            var folder = Path.GetFullPath(folderOption.Trim().Trim('"'));
            return settings is null
                ? ClientScanContext.ForFolder(folder)
                : new ClientScanContext(folder, settings.Launch.ServiceName, settings.Network.PublicAddress, settings.Ports.Lobby);
        }

        if (settings is null || string.IsNullOrWhiteSpace(settings.ClientFolder))
        {
            throw new CliException("No client folder is set up yet. Pass --client <folder>, or run 'tposeidon setup'.");
        }

        return ClientScanContext.For(settings);
    }

    public static void Print(ScanReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        foreach (var finding in report.General)
        {
            Console.WriteLine($"  {Mark(finding.Status)} {finding.Message}{FixTag(report, finding)}");
        }

        foreach (var map in report.ByGroup)
        {
            Console.WriteLine();
            Console.WriteLine($"  {map.Key}");
            foreach (var finding in map)
            {
                Console.WriteLine($"    {Mark(finding.Status)} {finding.Message}{FixTag(report, finding)}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  " + report.Summary);
        foreach (var id in report.FixableIds)
        {
            Console.WriteLine($"  Fix available: {id} - {report.FixTitles[id]}");
        }
    }

    public static void Print(IReadOnlyList<FixResult> results)
    {
        foreach (var result in results)
        {
            Console.WriteLine($"  {result.Title}: {result.Outcome.Message}");
        }
    }

    private static int RunCheck(ClientScanContext context)
    {
        Console.WriteLine($"Client folder: {context.ClientFolder}");
        var report = ClientScanner.Default.Scan(context);
        Print(report);
        return report.Blocking ? 1 : 0;
    }

    private static int RunFix(ClientScanContext context, CommandLine commandLine)
    {
        var only = commandLine.Option(OnlyOption) is { } id ? new[] { id } : null;
        Console.WriteLine($"Client folder: {context.ClientFolder}");
        var results = commandLine.Restore
            ? ClientScanner.Default.Restore(context, only)
            : ClientScanner.Default.Fix(context, only);
        if (results.Count == 0)
        {
            Console.WriteLine(commandLine.Restore ? "  Nothing to restore." : "  Nothing to fix.");
            return 0;
        }

        Print(results);
        return 0;
    }

    private static string Mark(ScanStatus status) => status switch
    {
        ScanStatus.Pass => "[ok]     ",
        ScanStatus.Info => "[note]   ",
        ScanStatus.Warning => "[warning]",
        _ => "[problem]",
    };

    private static string FixTag(ScanReport report, ScanFinding finding) =>
        finding.FixId is { } id && report.FixTitles.ContainsKey(id) ? $" (fix: {id})" : "";
}
