using tPoseidon.Cli.Terminal;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Cli.Commands;

internal static class GmCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine, Prompt prompt)
    {
        var subcommand = commandLine.Positionals.Count > 0 ? commandLine.Positionals[0] : null;
        if (subcommand is null)
        {
            if (commandLine.Option("character") is null)
            {
                return Help();
            }

            subcommand = GmAccounts.RegisterName;
        }

        switch (subcommand)
        {
            case GmAccounts.RegisterName:
                return Register(paths, commandLine);
            case GmAccounts.UnregisterName:
                return Unregister(paths, commandLine);
            case GmAccounts.ListName:
                commandLine.RejectUnknown();
                return Print(GmAccounts.List(new DatabaseGmAccountBook(CommandSupport.LoadSettings(paths).Database)));
        }

        if (GmPenalties.Handles(subcommand))
        {
            commandLine.RejectUnknown();
            var settings = CommandSupport.LoadSettings(paths);
            return Print(GmPenalties.Run(new DatabasePenaltyBook(settings.Database), subcommand, commandLine.Positionals.Skip(1).ToList()));
        }

        var command = GameMasterToolCommands.Find(subcommand)
            ?? throw new CliException($"Unknown gm command '{subcommand}'. Run 'tposeidon gm --help' for the list.");
        return RunTool(paths, commandLine, prompt, command);
    }

    public static int Help()
    {
        Console.WriteLine("tposeidon gm <command> [arguments] [options]");
        Console.WriteLine();
        Console.WriteLine("Game master tools. 'register' prepares a character; every other command logs in as that");
        Console.WriteLine("character through the system server and runs.");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        foreach (var line in GmAccounts.Help.Concat(GmPenalties.Help))
        {
            Console.WriteLine(line);
        }

        foreach (var command in GameMasterToolCommands.All)
        {
            Console.WriteLine($"  {command.Name,-20} {command.Arguments}");
            Console.WriteLine($"                       {command.Description}");
            Console.WriteLine($"                       {command.Example}");
        }

        Console.WriteLine();
        Console.WriteLine("Options for every command except register, unregister, list and the penalty commands:");
        Console.WriteLine("  --gm <character>      the registered GM character to log in as (required)");
        Console.WriteLine("  --password <secret>   the account's password (asked for when not given)");
        Console.WriteLine($"  --host <address>      system server address (default {GmToolEndpoint.LocalHost}; the tool connection is local-only by default)");
        Console.WriteLine("  --port <number>       system server port (default: the one in your settings)");
        Console.WriteLine("  --home <folder>       tPoseidon folder (default: the one this program is in)");
        Console.WriteLine();
        Console.WriteLine("'tposeidon gm --character <name>' still works and means 'register'.");
        Console.WriteLine("The control panel's GM button opens the same commands in a window.");
        return 0;
    }

    private static int Register(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown("character", "permit-ip");
        var settings = CommandSupport.LoadSettings(paths);
        var character = commandLine.Option("character")?.Trim();
        if (string.IsNullOrEmpty(character))
        {
            throw new CliException("Say which character: tposeidon gm register --character <name> [--permit-ip <address>]");
        }

        var permitIp = commandLine.Option("permit-ip")?.Trim() ?? GmAccounts.LocalPermitIp;
        if (!GmAccounts.IsPermitAddress(permitIp))
        {
            throw new CliException($"--permit-ip must be an IP address (got '{permitIp}').");
        }

        return Print(GmAccounts.Register(new DatabaseGmAccountBook(settings.Database), character, permitIp));
    }

    private static int Unregister(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown("character");
        var settings = CommandSupport.LoadSettings(paths);
        var character = commandLine.Option("character")?.Trim();
        if (string.IsNullOrEmpty(character))
        {
            throw new CliException("Say which character: tposeidon gm unregister --character <name>");
        }

        return Print(GmAccounts.Unregister(new DatabaseGmAccountBook(settings.Database), character));
    }

    private static int RunTool(InstallPaths paths, CommandLine commandLine, Prompt prompt, GameMasterToolCommand command)
    {
        commandLine.RejectUnknown("gm", "password", "host", "port");
        var settings = CommandSupport.LoadSettings(paths);
        var gm = commandLine.Option("gm")?.Trim();
        if (string.IsNullOrEmpty(gm))
        {
            throw new CliException("Say which GM character to log in as: --gm <character>");
        }

        var host = commandLine.Option("host")?.Trim() ?? GmToolEndpoint.LocalHost;
        var port = settings.Ports.System;
        if (commandLine.Option("port") is { } portText && !int.TryParse(portText, out port))
        {
            throw new CliException($"--port must be a number (got '{portText}').");
        }

        var call = GmTool.Prepare(command, commandLine.Positionals.Skip(1).ToList());
        var password = commandLine.Option("password") ?? prompt.AskSecret("Account password");
        if (string.IsNullOrEmpty(password))
        {
            throw new CliException("The account password is required.");
        }

        return Print(GmTool.RunAsync(GmToolTransport.Shared, new GmToolEndpoint(host, port, paths.KeyTable), new GmLogin(gm, password), call, CancellationToken.None)
            .GetAwaiter().GetResult());
    }

    private static int Print(GmOutcome outcome)
    {
        foreach (var line in outcome.Lines)
        {
            Console.WriteLine(line);
        }

        return outcome.ExitCode;
    }
}
