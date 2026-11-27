using System.Globalization;
using tPoseidon.Cli.Terminal;
using tPoseidon.Core.Common;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Updates;

namespace tPoseidon.Cli.Commands;

internal static class UpdateCommand
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(30);

    public static async Task<int> RunAsync(InstallPaths paths, CommandLine commandLine, Prompt prompt, CancellationToken ct)
    {
        var action = commandLine.Positionals.Count > 0 ? commandLine.Positionals[0] : "check";
        return action switch
        {
            "check" => await CheckAsync(paths, commandLine, ct).ConfigureAwait(false),
            "install" => await InstallAsync(paths, commandLine, prompt, ct).ConfigureAwait(false),
            "status" => Status(paths, commandLine),
            "apply" => await ApplyAsync(paths, commandLine, ct).ConfigureAwait(false),
            "recover" => await RecoverAsync(paths, commandLine, ct).ConfigureAwait(false),
            _ => throw new CliException($"Unknown update action '{action}'. Use check, install, status or recover."),
        };
    }

    public static void WriteHelp()
    {
        Console.WriteLine("update options:");
        Console.WriteLine("  update check                     look for a newer release on the chosen channel (stable or beta, set in tposeidon.json)");
        Console.WriteLine("  update install [--channel stable|beta] [--countdown <minutes>] [--no-backup] [--yes]");
        Console.WriteLine("                                   download, check the signature, warn players, stop, back up, replace the program files,");
        Console.WriteLine("                                   start and check; goes back by itself if the new version does not start");
        Console.WriteLine("  update status                    show the result of the last update");
        Console.WriteLine("  update recover                   put the earlier version back after an interrupted update");
    }

    private static async Task<int> CheckAsync(InstallPaths paths, CommandLine commandLine, CancellationToken ct)
    {
        commandLine.RejectUnknown("channel");
        var (service, _) = Open(paths, commandLine);
        Console.WriteLine($"tPoseidon {service.InstalledVersion} is installed. Channel: {service.Settings.Channel.ToString().ToLowerInvariant()}.");
        var offer = await Find(service, ct).ConfigureAwait(false);
        if (offer is null)
        {
            Console.WriteLine("You are up to date.");
            return 0;
        }

        Console.WriteLine($"Version {offer.Version} is available.");
        foreach (var line in offer.Release.Notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(12))
        {
            Console.WriteLine("  " + line);
        }

        Console.WriteLine("Install it with: tposeidon update install");
        return 0;
    }

    private static async Task<int> InstallAsync(InstallPaths paths, CommandLine commandLine, Prompt prompt, CancellationToken ct)
    {
        commandLine.RejectUnknown("channel", "countdown");
        var (service, settings) = Open(paths, commandLine);
        var countdown = settings.Updates.CountdownMinutes;
        if (commandLine.Option("countdown") is { } text && (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out countdown) || countdown > UpdateSettings.MaxCountdownMinutes))
        {
            throw new CliException($"--countdown must be a whole number of minutes from 0 to {UpdateSettings.MaxCountdownMinutes}.");
        }

        var offer = await Find(service, ct).ConfigureAwait(false);
        if (offer is null)
        {
            Console.WriteLine($"tPoseidon {service.InstalledVersion} is installed and up to date.");
            return 0;
        }

        var host = new SystemUpdateHost(paths, settings);
        if (host.ServersRunning())
        {
            Console.WriteLine(settings.Updates.CanSendNotice
                ? $"The servers are running. Players get an in-game notice, then the servers stop in {countdown} minute(s)."
                : "The servers are running and no game master character is set for notices (updates.noticeCharacter and updates.noticePassword in tposeidon.json), so players get no in-game warning.");
        }

        if (!prompt.Confirm($"Install tPoseidon {offer.Version} now?", defaultValue: true))
        {
            Console.WriteLine("Nothing was changed.");
            return 0;
        }

        var options = service.OptionsFor(!commandLine.NoBackup, null, countdown);
        var progress = new Progress<double>(done => Console.Write($"\rDownloading {done:P0}   "));
        await service.InstallAsync(offer, options, host, Environment.ProcessId, progress, Console.WriteLine, null, ct).ConfigureAwait(false);
        Console.WriteLine();
        Console.WriteLine("The update continues in the background. See the result with: tposeidon update status");
        return 0;
    }

    private static int Status(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown();
        var result = UpdateService.LastResult(paths);
        Console.WriteLine(result is null ? "No update has been installed here yet." : $"{result.At.ToLocalTime():yyyy-MM-dd HH:mm}  {result.Outcome}: {result.Message}");
        Console.WriteLine($"Log: {new UpdateWork(paths).LogFile}");
        return result is { Outcome: UpdateOutcome.RolledBack or UpdateOutcome.RollbackFailed } ? 1 : 0;
    }

    private static async Task<int> ApplyAsync(InstallPaths paths, CommandLine commandLine, CancellationToken ct)
    {
        commandLine.RejectUnknown("pid");
        if (!int.TryParse(commandLine.Option("pid"), NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
        {
            throw new CliException("update apply needs --pid <process to wait for>. It is started by the updater itself.");
        }

        var result = await UpdateApplier.RunAsync(paths, processId, new ProcessApplyHost(), null, ct).ConfigureAwait(false);
        Console.WriteLine(result.Message);
        return result.Outcome == UpdateOutcome.Updated ? 0 : 1;
    }

    private static async Task<int> RecoverAsync(InstallPaths paths, CommandLine commandLine, CancellationToken ct)
    {
        commandLine.RejectUnknown();
        var result = await UpdateApplier.RecoverAsync(paths, new ProcessApplyHost(), null, ct).ConfigureAwait(false);
        Console.WriteLine(result.Message);
        return result.Outcome == UpdateOutcome.RolledBack ? 0 : 1;
    }

    private static (UpdateService Service, HostSettings Settings) Open(InstallPaths paths, CommandLine commandLine)
    {
        var settings = CommandSupport.LoadSettings(paths);
        if (commandLine.Option("channel") is { } channel)
        {
            settings.Updates.Channel = channel.ToLowerInvariant() switch
            {
                "stable" => ReleaseChannel.Stable,
                "beta" => ReleaseChannel.Beta,
                _ => throw new CliException("--channel must be stable or beta."),
            };
        }

        var http = new HttpClient { Timeout = RequestTimeout };
        return (UpdateService.Create(paths, settings, http), settings);
    }

    private static async Task<UpdateOffer?> Find(UpdateService service, CancellationToken ct)
    {
        var check = await service.CheckAsync(ct).ConfigureAwait(false);
        return check.Failed ? throw new CliException(check.Problem!) : check.Offer;
    }

    private sealed class InlineProgress : IProgress<double>
    {
        private int _last = -1;

        public void Report(double value)
        {
            var percent = (int)(value * 100);
            if (percent / 10 != _last / 10 || _last < 0)
            {
                Console.Write($"\rDownloading {percent}%   ");
            }

            _last = percent;
        }
    }
}
