using System.Globalization;
using Npgsql;
using tPoseidon.Cli.Terminal;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class ShopCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown("add", "remove", "name", "items", "price", "hours", "category", "desc");
        var settings = CommandSupport.LoadSettings(paths);
        var add = commandLine.Option("add");
        var remove = commandLine.Option("remove");
        if (add is not null && remove is not null)
        {
            throw new CliException("Use either --add or --remove, not both.");
        }

        using var dataSource = NpgsqlDataSource.Create(settings.Database.Connection);
        ActiveSchemaProvider.Set(settings.Database.Schema);
        var store = new PostgresCashShopStore(dataSource);
        if (remove is not null)
        {
            var code = Number(remove, "a goods code");
            Console.WriteLine(store.RemoveGoods(code)
                ? $"Goods {code} removed from the shop (goods that were already sold are only taken off sale)."
                : $"There are no goods with code {code}.");
            return 0;
        }

        if (add is not null)
        {
            var goods = ShopGoodsAdmin.Add(store, new ShopGoodsRequest(
                Number(add, "a goods code"),
                commandLine.Option("name") ?? string.Empty,
                commandLine.Option("items") ?? string.Empty,
                commandLine.Option("price") is { } price ? Number(price, "a price") : 0,
                commandLine.Option("hours") is { } hours ? Number(hours, "a number of hours") : 0,
                commandLine.Option("category") is { } category ? (byte)Number(category, "a category") : (byte)0,
                commandLine.Option("desc") ?? string.Empty));
            Console.WriteLine($"Goods {goods.GoodsCode} ({goods.Name}) saved with {goods.Items.Count} item(s).");
            return 0;
        }

        foreach (var line in ShopGoodsAdmin.Describe(store.ListGoods()))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    private static int Number(string text, string what) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new CliException($"'{text}' is not {what}.");
}
