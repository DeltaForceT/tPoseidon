using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;
using tPoseidon.LauncherKit;

namespace tPoseidon.Cli.Commands;

internal static class LauncherCommand
{
    private const string Publish = "publish";
    private const string Folder = "folder";
    private const string Key = "key";
    private const string DropLauncher = "drop-launcher";
    private const string LauncherOption = "launcher";
    private const string RotateKey = "rotate-key";

    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown(LauncherOption);
        var action = commandLine.Positionals.Count > 0 ? commandLine.Positionals[0] : Publish;
        if (commandLine.Positionals.Count > 1)
        {
            throw new CliException($"Unexpected argument '{commandLine.Positionals[1]}'.");
        }

        return action switch
        {
            Publish => PublishUpdate(paths, commandLine.Option(LauncherOption)),
            Folder => PrintFolder(paths),
            DropLauncher => RemoveLauncher(paths),
            Key => PrintKey(paths),
            RotateKey => ChangeKey(paths, commandLine),
            _ => throw new CliException($"Unknown launcher action '{action}'. Use publish, folder, key, rotate-key or drop-launcher."),
        };
    }

    public static void WriteHelp()
    {
        Console.WriteLine("launcher options:");
        Console.WriteLine("  launcher publish   build the signed update manifest from the updates folder; players' launchers pick it up");
        Console.WriteLine("  launcher publish --launcher <zip>   also hand out the launcher zip the panel built, so branded launchers update themselves");
        Console.WriteLine("  launcher drop-launcher   stop handing out a launcher update (publish again afterwards)");
        Console.WriteLine("  launcher folder    print the updates folder (client/, news/, packs/, banner.png, channel.json)");
        Console.WriteLine("  launcher key       print the public key your launchers trust (created on first use)");
        Console.WriteLine("  launcher rotate-key --yes   make a new signing key; launchers you handed out follow it");
    }

    private static int RemoveLauncher(InstallPaths paths)
    {
        LauncherUpdatePackage.Remove(paths);
        Console.WriteLine("The launcher update was removed. Publish again so players stop being offered it.");
        return 0;
    }

    private static int PublishUpdate(InstallPaths paths, string? launcherZip)
    {
        var settings = CommandSupport.LoadSettings(paths);
        if (launcherZip is not null)
        {
            var import = LauncherUpdatePackage.Import(paths, launcherZip);
            Console.WriteLine($"Launcher update {import.Info.Version}: {import.Files} files ({import.Bytes} bytes) ready to hand out.");
        }

        var outcome = UpdateChannel.Publish(paths, settings);
        if (UpdateChannel.ReadLauncher(paths) is { TestOnThisComputer: true })
        {
            Console.WriteLine("Warning: " + LauncherUpdatePackage.TestLauncherWarning);
        }

        var manifest = outcome.Manifest;
        Console.WriteLine($"Published '{manifest.Name}': {manifest.Files.Count} files ({manifest.TotalBytes} bytes), {manifest.News.Count} news items{(manifest.Banner is null ? ", no banner" : $", banner {manifest.Banner}")}.");
        Console.WriteLine($"Changes since the last publish: {outcome.Record.Added} files added, {outcome.Record.Changed} changed, {outcome.Record.Removed} removed{(outcome.Record.BannerChanged ? ", banner changed" : "")}.");
        Console.WriteLine("Only publish your own files: custom content, never the original game files. The web port (item shop) serves them.");
        return 0;
    }

    private static int PrintKey(InstallPaths paths)
    {
        var key = SigningKeyStore.EnsureCreated(paths);
        Console.WriteLine(key.PublicKey);
        Console.WriteLine($"Key file: {paths.SigningKey} (it is part of 'tposeidon backup'; keep it private).");
        return 0;
    }

    private static int ChangeKey(InstallPaths paths, CommandLine commandLine)
    {
        if (!commandLine.AssumeDefaults)
        {
            throw new CliException("Changing the signing key makes every later publish use the new key. Launchers you already handed out follow the change; run again with --yes to do it.");
        }

        var key = SigningKeyStore.Rotate(paths);
        Console.WriteLine($"New public key: {key.PublicKey}");
        Console.WriteLine("Publish again so the new key signs your updates. Launchers you build from now on carry the new key.");
        return 0;
    }

    private static int PrintFolder(InstallPaths paths)
    {
        Console.WriteLine(paths.Updates);
        return 0;
    }
}
