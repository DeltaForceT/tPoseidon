using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Cli.Commands;

internal sealed class StartCommand
{
    private readonly InstallPaths _paths;
    private readonly CommandLine _commandLine;
    private readonly Prompt _prompt;

    public StartCommand(InstallPaths paths, CommandLine commandLine, Prompt prompt)
    {
        _paths = paths;
        _commandLine = commandLine;
        _prompt = prompt;
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        _commandLine.RejectUnknown();
        var settings = CommandSupport.LoadSettings(_paths);
        ClientFolder.EnsureUsable(settings);

        var supervisor = new ServerSupervisor(_paths, settings);
        if (!ServerCatalog.Enabled(settings).Any(server => supervisor.State(server).Running))
        {
            OfferBackupAfterUpdate(settings);
        }

        var observer = new ConsoleStartupObserver();
        try
        {
            await ServerStartup.StartAllAsync(_paths, settings, observer, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HostingException or OperationCanceledException)
        {
            observer.EndPendingLine();
            throw;
        }

        Console.WriteLine();
        Console.WriteLine($"tPoseidon is running. Players connect to {settings.Network.PublicAddress}.");
        Console.WriteLine($"Logs: {_paths.Logs}");
        Console.WriteLine("Start the game with 'tposeidon play'. Stop everything with 'tposeidon stop'.");
        return 0;
    }

    private void OfferBackupAfterUpdate(HostSettings settings)
    {
        if (!ServerStartup.NeedsBackupBeforeStart(settings))
        {
            return;
        }

        if (_commandLine.NoBackup)
        {
            Console.WriteLine($"This database was last used by tPoseidon {settings.LastStartedVersion}; this is {VersionCommand.Current}. Starting without a backup (--no-backup).");
            return;
        }

        Console.WriteLine($"This database was last used by tPoseidon {settings.LastStartedVersion}; this is {VersionCommand.Current}.");
        Console.WriteLine("A new version can change how data is stored, and the change can't be undone without a backup.");
        if (!_prompt.Confirm("Back up the database first?", defaultValue: true))
        {
            return;
        }

        try
        {
            BackupCommand.Backup(_paths, settings);
        }
        catch (HostingException ex)
        {
            Console.WriteLine("The database could not be backed up:");
            Console.WriteLine(ex.Message);
            if (!_prompt.Interactive)
            {
                throw new CliException("The servers were not started because the backup failed. Run 'tposeidon start --no-backup' to start without a backup.", ex);
            }

            if (!_prompt.Confirm("Start without a backup?", defaultValue: false))
            {
                throw new CliException("The servers were not started. Fix the backup, or run 'tposeidon start --no-backup' to start without one.", ex);
            }
        }
    }

    private sealed class ConsoleStartupObserver : IStartupObserver
    {
        private bool _pendingLine;

        public void CheckingDatabase(string database) => Console.WriteLine($"Checking the database ({database})...");

        public void UpdatingTables() => Console.WriteLine("Updating the database tables...");

        public void RebuildingServerData() => Console.WriteLine("Rebuilding the server data from your client after the update (once, takes a minute)...");

        public void StartingServers() => Console.WriteLine("Starting the servers:");

        public void RollingBack()
        {
            EndPendingLine();
            Console.WriteLine("Stopping the servers that did start...");
        }

        public void Report(ServerStartReport value)
        {
            switch (value.Stage)
            {
                case ServerStartStage.AlreadyRunning:
                    Console.WriteLine($"  {value.Server.Title,-16} already running (pid {value.ProcessId})");
                    break;
                case ServerStartStage.Starting:
                    Console.Write($"  {value.Server.Title,-16} starting on port {value.Port}...");
                    _pendingLine = true;
                    break;
                case ServerStartStage.Up:
                    Console.WriteLine($" up (pid {value.ProcessId})");
                    _pendingLine = false;
                    break;
            }
        }

        public void EndPendingLine()
        {
            if (_pendingLine)
            {
                Console.WriteLine(" failed");
                _pendingLine = false;
            }
        }
    }
}
