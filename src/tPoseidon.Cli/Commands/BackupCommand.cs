using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Database;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Cli.Commands;

internal static class BackupCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown();
        var settings = CommandSupport.LoadSettings(paths);
        if (new ServerSupervisor(paths, settings).AnyRunning())
        {
            throw new CliException("The servers are running. Run 'tposeidon stop' first so the backup is consistent.");
        }

        Backup(paths, settings);
        return 0;
    }

    public static void Backup(InstallPaths paths, HostSettings settings)
    {
        var tool = DatabaseBackup.Locate(settings);
        Console.WriteLine($"Backing up {settings.Database.Describe()} with {tool.Describe()}...");
        var target = DatabaseBackup.Run(paths, settings, tool);
        Console.WriteLine($"Backup written to {target}.");
        Console.WriteLine(DatabaseBackup.RestoreHint(target));
    }
}
