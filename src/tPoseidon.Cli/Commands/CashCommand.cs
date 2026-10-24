using System.Globalization;
using Npgsql;
using tPoseidon.Cli.Terminal;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class CashCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown("account", "grant");
        var settings = CommandSupport.LoadSettings(paths);
        var name = commandLine.Option("account")?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new CliException("Say which account: tposeidon cash --account <name> [--grant <amount>]");
        }

        if (settings.ShopCash != "balance")
        {
            Console.WriteLine($"The item shop is set to '{settings.ShopCash}', so cash balances are not used. Run 'tposeidon setup --cash balance' to switch.");
        }

        using var dataSource = NpgsqlDataSource.Create(settings.Database.Connection);
        ActiveSchemaProvider.Set(settings.Database.Schema);
        var account = new PostgresAccountStore(dataSource).GetAccountByUsername(name)
            ?? throw new CliException($"There is no account named '{name}'.");
        var store = new PostgresCashShopStore(dataSource);
        var grant = commandLine.Option("grant");
        if (grant is null)
        {
            Console.WriteLine($"{account.Username}: {store.Balance(account.AccountId, settings.ShopStartingCash).ToString("N0", CultureInfo.InvariantCulture)} cash");
            return 0;
        }

        var amount = long.TryParse(grant, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new CliException($"'{grant}' is not an amount.");
        var balance = store.Grant(account.AccountId, amount, settings.ShopStartingCash, "console");
        Console.WriteLine($"{account.Username}: now {balance.ToString("N0", CultureInfo.InvariantCulture)} cash");
        return 0;
    }
}
