using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class ServerFileCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown("name", "web-url", "note", "color", "output");
        var settings = CommandSupport.LoadSettings(paths);
        var name = commandLine.Option("name") ?? settings.Launch.WorldName;
        var file = ServerFile.ForServer(settings, name, commandLine.Option("web-url"), commandLine.Option("note"), commandLine.Option("color"));
        var output = Path.GetFullPath(commandLine.Option("output")?.Trim().Trim('"') ?? Path.Combine(paths.Root, "players" + ServerFile.Extension));
        file.Write(output);
        Console.WriteLine($"Server file written to {output}.");
        Console.WriteLine($"Players open it in the tPoseidon launcher to join {file.Address}. Their login goes through {file.WebUrl}, so that web port must be reachable.");
        return 0;
    }
}
