using tPoseidon.Core.Game.Scripting.Library;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class ScriptsCommand
{
    private const string Check = "check";
    private const string Reload = "reload";
    private const string Folder = "folder";

    public static int Run(InstallPaths paths, Terminal.CommandLine commandLine)
    {
        commandLine.RejectUnknown("dir");
        var action = commandLine.Positionals.Count > 0 ? commandLine.Positionals[0] : Check;
        if (commandLine.Positionals.Count > 1)
        {
            throw new CliException($"Unexpected argument '{commandLine.Positionals[1]}'.");
        }

        var directory = commandLine.Option("dir") ?? FolderOf(paths);
        return action switch
        {
            Check => CheckScripts(directory),
            Reload => RequestReload(directory),
            Folder => PrintFolder(directory),
            _ => throw new CliException($"Unknown scripts action '{action}'. Use check, reload or folder."),
        };
    }

    public static string FolderOf(InstallPaths paths) => Path.Combine(paths.ServerData, "scripts", "npc");

    public static void WriteHelp()
    {
        Console.WriteLine("scripts options:");
        Console.WriteLine("  scripts check [--dir <folder>]    compile every .tscript file and list the errors as file:line");
        Console.WriteLine("  scripts reload [--dir <folder>]   check, then ask the running game server to reload the scripts");
        Console.WriteLine("  scripts folder                    print the folder the game server reads scripts from");
    }

    private static int CheckScripts(string directory)
    {
        Console.WriteLine($"Scripts folder: {directory}");
        if (!Directory.Exists(directory))
        {
            Console.WriteLine("The folder does not exist, so no scripts are active.");
            return 0;
        }

        var result = ScriptLoader.LoadDirectory(directory);
        foreach (var error in result.Errors) Console.Error.WriteLine(error);
        var library = result.Library;
        Console.WriteLine($"{result.FilesLoaded} file(s) compiled, {result.FilesFailed} with errors: {library.Npcs.Count} npc(s), {library.OverrideCount} override(s), {library.EventCount} event(s), {library.Timers.Count} timer(s), {library.CommandCount} command(s), {library.FunctionCount} function(s).");
        return result.FilesFailed == 0 ? 0 : 1;
    }

    private static int RequestReload(string directory)
    {
        var status = CheckScripts(directory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ScriptEngine.ReloadRequestFile), string.Empty);
        Console.WriteLine("Reload requested. A running game server picks it up within a second; files with errors stay out until they are fixed.");
        return status;
    }

    private static int PrintFolder(string directory)
    {
        Console.WriteLine(directory);
        return 0;
    }
}
