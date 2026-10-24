using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class PlayCommand
{
    public static async Task<int> RunAsync(InstallPaths paths, CommandLine commandLine, Prompt prompt, CancellationToken ct)
    {
        commandLine.RejectUnknown("account", "server", "client", "web-url");
        if (commandLine.Option("server") is { } server)
        {
            return await RunRemoteAsync(paths, commandLine, prompt, server.Trim(), ct).ConfigureAwait(false);
        }

        if (commandLine.Option("client") is not null || commandLine.Option("web-url") is not null)
        {
            throw new CliException("--client and --web-url only go with --server <address>.");
        }

        var settings = CommandSupport.LoadSettings(paths);
        if (commandLine.Option("account") is { } requested)
        {
            var chosen = PlayAccount.Choose(settings, new DatabaseAccountDirectory(settings.Database), requested);
            settings.Save(paths);
            Console.WriteLine($"Play now logs in with '{chosen}'.");
        }
        else if (settings.Launch.Account.Length > 0)
        {
            Console.WriteLine($"Play logs in with '{settings.Launch.Account}'. Switch with: tposeidon play --account <name>");
        }

        var target = GameClientLauncher.Launch(settings);
        Console.WriteLine($"Started the game client, connecting to {target}.");
        return 0;
    }

    private static async Task<int> RunRemoteAsync(InstallPaths paths, CommandLine commandLine, Prompt prompt, string server, CancellationToken ct)
    {
        var account = commandLine.Option("account")?.Trim();
        if (string.IsNullOrEmpty(account))
        {
            throw new CliException("Say which account to log in with: tposeidon play --server <address> --account <name>");
        }

        var settings = HostSettings.Load(paths) ?? new HostSettings();
        if (commandLine.Option("client") is { } folder)
        {
            settings.ClientFolder = Path.GetFullPath(folder.Trim().Trim('"'));
        }

        if (string.IsNullOrWhiteSpace(settings.ClientFolder))
        {
            throw new CliException("Say where your game client is: tposeidon play --server <address> --account <name> --client <folder>");
        }

        var password = prompt.AskSecret($"Password for {account} on {server}");
        var endpoint = PlayTicketClient.Endpoint(server, settings.Ports.Shop, commandLine.Option("web-url"));
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        await PlayTicketClient.RequestAsync(http, endpoint, account, password, ct).ConfigureAwait(false);

        settings.Network.PublicAddress = server;
        settings.Launch.Account = account;
        var target = GameClientLauncher.LaunchWithTicket(settings);
        Console.WriteLine($"Logged in. Started the game client, connecting to {target}.");
        return 0;
    }
}
