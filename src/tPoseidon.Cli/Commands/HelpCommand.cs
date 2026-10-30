using tPoseidon.Core.Common;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Cli.Commands;

internal static class HelpCommand
{
    public static int Run()
    {
        Console.WriteLine($"tPoseidon {VersionCommand.Current}");
        Console.WriteLine();
        Console.WriteLine("Usage: tposeidon <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  setup     first-run setup: client folder, network, database, game data, account");
        Console.WriteLine("  start     check the database and start every server");
        Console.WriteLine("  stop      stop every server");
        Console.WriteLine("  status    show what is running, the ports and where the logs are");
        Console.WriteLine("  backup    back up the database with pg_dump");
        Console.WriteLine("  gm        game master tools (run 'tposeidon gm --help')");
        Console.WriteLine("  cash      show or change an account's item shop cash (balance mode)");
        Console.WriteLine("  camp      list or add the My Camp items of a character's account");
        Console.WriteLine("  guild-depot  show or set how many depot tabs a guild owns");
        Console.WriteLine("  guild-coins  show a guild's coins or add some");
        Console.WriteLine("  scripts   check or reload the NPC scripts (check, reload, folder)");
        Console.WriteLine("  security  connection guard: status, ban, unban, allow (run 'tposeidon security --help')");
        Console.WriteLine("  play      start the game client pointed at this server, logged in with your play account");
        Console.WriteLine("  server-file  write the file friends open in the launcher to join this server");
        Console.WriteLine("  launcher  publish the update folder for players' launchers (launcher publish, launcher folder)");
        Console.WriteLine("  client    scan the game client folder and fix what the emulator needs (client check, client fix)");
        Console.WriteLine("  version   print the version");
        Console.WriteLine();
        Console.WriteLine("start options:");
        Console.WriteLine("  --no-backup           start without the backup that is offered after an update (use it when pg_dump is not");
        Console.WriteLine("                        installed and PostgreSQL does not run in a Docker container that publishes its port)");
        Console.WriteLine();
        Console.WriteLine("backup:");
        Console.WriteLine("  uses pg_dump from PATH or the usual PostgreSQL folders; without it, runs pg_dump inside the running Docker");
        Console.WriteLine("  container that publishes the database port");
        Console.WriteLine();
        Console.WriteLine("Setup options (all optional; setup asks for anything not given):");
        Console.WriteLine("  --client <folder>              game client folder");
        Console.WriteLine("  --audience local|lan|internet  who connects");
        Console.WriteLine("  --public-ip <address>          address players connect to");
        Console.WriteLine("  --db-host <host> --db-port <port> --db-name <name>");
        Console.WriteLine("  --db-admin-user <login> --db-admin-password <secret>   create the database and its login");
        Console.WriteLine("  --db-user <login> --db-password <secret>               or use a login you already made");
        Console.WriteLine("  --admin-user <name> --admin-password <secret>          your game account");
        Console.WriteLine("  --play-account <name>          the account Play logs in with (default: the one you create)");
        Console.WriteLine("  --cash free|balance|disabled --cash-start <amount>     item shop cash");
        Console.WriteLine("  --guild-coins <amount>                                 guild coins for each guild game win (default 100)");
        Console.WriteLine();
        Console.WriteLine("play options:");
        Console.WriteLine("  --account <name>      log in with another existing account; remembered for next time");
        Console.WriteLine("  --server <address> --account <name> [--client <folder>] [--web-url <url>]");
        Console.WriteLine("                        play on someone else's server: asks for your password, logs in over the");
        Console.WriteLine("                        server's web port, then starts your client pointed at it");
        Console.WriteLine();
        Console.WriteLine("server-file options (all optional):");
        Console.WriteLine("  --name <text> --note <text> --color <#RRGGBB> --web-url <url> --output <file>   default: players.tposeidon-server in the tPoseidon folder");
        Console.WriteLine();
        Console.WriteLine("guild-depot options:");
        Console.WriteLine("  --guild <name> [--tabs <0-10>]   show the tab count, or set it (owner override; members buy tabs in the guild shop)");
        Console.WriteLine();
        Console.WriteLine("guild-coins options:");
        Console.WriteLine("  --guild <name> [--add <amount>]   show the guild coins the guild shop spends, or add (or with a minus sign, take) some");
        Console.WriteLine();
        Console.WriteLine("cash options:");
        Console.WriteLine("  --account <name> [--grant <amount>]   add (or with a minus sign, take) cash");
        Console.WriteLine();
        Console.WriteLine("camp options:");
        Console.WriteLine("  --character <name> [--give <camp item id> [--count <n>]]   add camp items to the account's new items");
        Console.WriteLine();
        Console.WriteLine("gm options:");
        Console.WriteLine("  register --character <name> [--permit-ip <address>]   and the GM commands; see 'tposeidon gm --help'");
        Console.WriteLine();
        ScriptsCommand.WriteHelp();
        Console.WriteLine();
        LauncherCommand.WriteHelp();
        Console.WriteLine();
        ClientCommand.WriteHelp();
        Console.WriteLine();
        Console.WriteLine("Common options:");
        Console.WriteLine("  --home <folder>            tPoseidon folder (default: the one this program is in)");
        Console.WriteLine("  -y, --yes                  accept the defaults, do not ask");
        return 0;
    }

    public static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        Console.Error.WriteLine("Run 'tposeidon help' for the list of commands.");
        return 2;
    }
}

internal static class VersionCommand
{
    public static string Current => ServerStartup.CurrentVersion;

    public static int Run()
    {
        var build = BuildIdentity.Current;
        Console.WriteLine(build.ToString());
        if (build.IsStamped && !build.IsConsistent)
        {
            Console.WriteLine("The build tag does not match this version and commit.");
        }

        return 0;
    }
}
