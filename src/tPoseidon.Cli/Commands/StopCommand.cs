using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Cli.Commands;

internal static class StopCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown();
        var settings = CommandSupport.LoadSettings(paths);
        foreach (var server in ServerStartup.StopAll(paths, settings))
        {
            Console.WriteLine($"  {server.Title,-16} stopped");
        }

        Console.WriteLine("All servers are stopped. PostgreSQL keeps running; it is managed by your system.");
        return 0;
    }
}
