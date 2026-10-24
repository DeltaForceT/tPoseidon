using tPoseidon.Cli;
using tPoseidon.Cli.Commands;
using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

try
{
    var commandLine = CommandLine.Parse(args);
    var paths = InstallPaths.Resolve(commandLine.Option("home"));
    var prompt = new Prompt(commandLine.AssumeDefaults);
    if (commandLine.Command is not ("gm" or "scripts" or "security") && commandLine.Positionals.Count > 0)
    {
        throw new CliException($"Unexpected argument '{commandLine.Positionals[0]}'.");
    }

    if (commandLine.Help)
    {
        return commandLine.Command switch
        {
            "gm" => GmCommand.Help(),
            "security" => SecurityCommand.Help(),
            _ => HelpCommand.Run(),
        };
    }

    return commandLine.Command switch
    {
        "setup" => await new SetupCommand(paths, commandLine, prompt).RunAsync(cancellation.Token).ConfigureAwait(false),
        "start" => await new StartCommand(paths, commandLine, prompt).RunAsync(cancellation.Token).ConfigureAwait(false),
        "stop" => StopCommand.Run(paths, commandLine),
        "status" => StatusCommand.Run(paths, commandLine),
        "backup" => BackupCommand.Run(paths, commandLine),
        "play" => PlayCommand.Run(paths, commandLine),
        "gm" => GmCommand.Run(paths, commandLine, prompt),
        "cash" => CashCommand.Run(paths, commandLine),
        "scripts" => ScriptsCommand.Run(paths, commandLine),
        "security" => SecurityCommand.Run(paths, commandLine),
        "version" or "--version" => VersionCommand.Run(),
        "help" => HelpCommand.Run(),
        _ => HelpCommand.Unknown(commandLine.Command),
    };
}
catch (HostingException ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("error: " + ex.Message);
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Cancelled.");
    return 130;
}
