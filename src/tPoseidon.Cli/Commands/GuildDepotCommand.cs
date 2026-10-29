using Npgsql;
using tPoseidon.Cli.Terminal;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class GuildDepotCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown("guild", "tabs");
        var settings = CommandSupport.LoadSettings(paths);
        var name = commandLine.Option("guild")?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new CliException("Say which guild: tposeidon guild-depot --guild <name> [--tabs <0-10>]");
        }

        using var dataSource = NpgsqlDataSource.Create(settings.Database.Connection);
        ActiveSchemaProvider.Set(settings.Database.Schema);
        var guild = new PostgresGuildStore(dataSource).FindGuildByName(name)
            ?? throw new CliException($"There is no guild named '{name}'.");
        var store = new PostgresGuildDepotStore(dataSource);
        var tabs = commandLine.Option("tabs");
        if (tabs is null)
        {
            Console.WriteLine($"{guild.GuildName}: {store.TabsOf(guild.GuildCode)} depot tabs");
            return 0;
        }

        if (!int.TryParse(tabs, out var wanted) || wanted < 0 || wanted > GuildDepotRules.MaxTabs)
        {
            throw new CliException($"--tabs must be a number from 0 to {GuildDepotRules.MaxTabs} (got '{tabs}').");
        }

        if (!store.SetTabs(guild.GuildCode, wanted))
        {
            throw new CliException($"{guild.GuildName} keeps items in tabs above {wanted}; empty them first.");
        }

        Console.WriteLine($"{guild.GuildName}: now {wanted} depot tabs (members see the change the next time they log in)");
        return 0;
    }
}
