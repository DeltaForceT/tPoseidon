using Npgsql;
using tPoseidon.Cli.Terminal;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class GuildCoinsCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown("guild", "add");
        var settings = CommandSupport.LoadSettings(paths);
        var name = commandLine.Option("guild")?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new CliException("Say which guild: tposeidon guild-coins --guild <name> [--add <amount>]");
        }

        using var dataSource = NpgsqlDataSource.Create(settings.Database.Connection);
        ActiveSchemaProvider.Set(settings.Database.Schema);
        var guild = new PostgresGuildStore(dataSource).FindGuildByName(name)
            ?? throw new CliException($"There is no guild named '{name}'.");
        var store = new PostgresGuildShopStore(dataSource);
        var add = commandLine.Option("add");
        if (add is null)
        {
            var state = store.StateOf(guild.GuildCode) ?? throw new CliException($"There is no guild named '{name}'.");
            Console.WriteLine($"{guild.GuildName}: {state.Coins} guild coins");
            return 0;
        }

        if (!int.TryParse(add, out var amount))
        {
            throw new CliException($"--add must be a whole number (got '{add}').");
        }

        var total = store.AddCoins(guild.GuildCode, amount)
            ?? throw new CliException($"{guild.GuildName} cannot go below 0 or above {int.MaxValue} guild coins with {amount}.");
        Console.WriteLine($"{guild.GuildName}: now {total} guild coins (members see the change the next time they log in)");
        return 0;
    }
}
