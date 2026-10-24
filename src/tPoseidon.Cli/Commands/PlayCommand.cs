using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class PlayCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown();
        var settings = CommandSupport.LoadSettings(paths);
        var target = GameClientLauncher.Launch(settings);
        Console.WriteLine($"Started the game client, connecting to {target}.");
        return 0;
    }
}
