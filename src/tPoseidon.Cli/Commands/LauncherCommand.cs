using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class LauncherCommand
{
    private const string Publish = "publish";
    private const string Folder = "folder";

    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown();
        var action = commandLine.Positionals.Count > 0 ? commandLine.Positionals[0] : Publish;
        if (commandLine.Positionals.Count > 1)
        {
            throw new CliException($"Unexpected argument '{commandLine.Positionals[1]}'.");
        }

        return action switch
        {
            Publish => PublishUpdate(paths),
            Folder => PrintFolder(paths),
            _ => throw new CliException($"Unknown launcher action '{action}'. Use publish or folder."),
        };
    }

    public static void WriteHelp()
    {
        Console.WriteLine("launcher options:");
        Console.WriteLine("  launcher publish   build the update manifest from the updates folder; players' launchers pick it up");
        Console.WriteLine("  launcher folder    print the updates folder (client/, news/, packs/, banner.png, channel.json)");
    }

    private static int PublishUpdate(InstallPaths paths)
    {
        var settings = CommandSupport.LoadSettings(paths);
        var outcome = UpdateChannel.Publish(paths, settings);
        var manifest = outcome.Manifest;
        Console.WriteLine($"Published '{manifest.Name}': {manifest.Files.Count} files ({manifest.TotalBytes} bytes), {manifest.News.Count} news items{(manifest.Banner is null ? ", no banner" : $", banner {manifest.Banner}")}.");
        Console.WriteLine($"Changes since the last publish: {outcome.Record.Added} files added, {outcome.Record.Changed} changed, {outcome.Record.Removed} removed{(outcome.Record.BannerChanged ? ", banner changed" : "")}.");
        Console.WriteLine("Only publish your own files: custom content, never the original game files. The web port (item shop) serves them.");
        return 0;
    }

    private static int PrintFolder(InstallPaths paths)
    {
        Console.WriteLine(paths.Updates);
        return 0;
    }
}
