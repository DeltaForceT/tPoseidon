using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Database;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Cli.Commands;

internal static class StatusCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown();
        Console.WriteLine($"tPoseidon {VersionCommand.Current}");
        Console.WriteLine($"Folder:   {paths.Root}");
        var settings = HostSettings.Load(paths);
        if (settings is null)
        {
            Console.WriteLine("Not set up yet. Run 'tposeidon setup'.");
            return 0;
        }

        Console.WriteLine($"Client:   {settings.ClientFolder}");
        Console.WriteLine($"Players:  {settings.Network.Audience.ToString().ToLowerInvariant()}, connecting to {settings.Network.PublicAddress}");
        Console.WriteLine($"Database: {settings.Database.Describe()}, {DatabaseState(settings)}");

        Console.WriteLine();
        var supervisor = new ServerSupervisor(paths, settings);
        foreach (var server in ServerCatalog.Enabled(settings))
        {
            var state = supervisor.State(server);
            var port = server.Port(settings.Ports);
            var text = state.ProcessId switch
            {
                null => "stopped",
                { } pid when state.Listening => $"running, pid {pid}",
                { } pid => $"starting or not answering, pid {pid}",
            };
            Console.WriteLine($"  {server.Title,-16} port {port,-6} {text}");
        }

        Console.WriteLine();
        Console.WriteLine($"Logs:     {paths.Logs}");
        Console.WriteLine($"Crashes:  {paths.CrashDumps}");
        return 0;
    }

    private static string DatabaseState(HostSettings settings)
    {
        try
        {
            var version = DatabaseAdmin.CheckServerAsync(settings.Database.Connection, CancellationToken.None).GetAwaiter().GetResult();
            return $"reachable (PostgreSQL {version.Major}.{version.Minor})";
        }
        catch (HostingException ex)
        {
            return "not reachable: " + ex.Message;
        }
    }
}
