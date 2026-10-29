using System.Globalization;
using Npgsql;
using tPoseidon.Cli.Terminal;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class CampCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown("character", "give", "count");
        var settings = CommandSupport.LoadSettings(paths);
        var name = commandLine.Option("character")?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new CliException("Say which character: tposeidon camp --character <name> [--give <camp item id> [--count <n>]]");
        }

        using var dataSource = NpgsqlDataSource.Create(settings.Database.Connection);
        ActiveSchemaProvider.Set(settings.Database.Schema);
        var characterId = new PostgresCharacterDirectory(dataSource).FindCharacterId(name)
            ?? throw new CliException($"There is no character named '{name}'.");
        var character = new PostgresCharStore(dataSource).GetCharacterById(characterId)
            ?? throw new CliException($"There is no character named '{name}'.");
        var store = new PostgresMyCampStore(dataSource);
        var give = commandLine.Option("give");
        if (give is null)
        {
            var items = store.GetItemsForAccount(character.OwnerAccountId).Where(item => item.ItemWhere != MyCampItemWhere.Deleted).ToArray();
            Console.WriteLine($"{character.Name}: {items.Count(item => item.ItemWhere == MyCampItemWhere.New)} new and {items.Count(item => item.ItemWhere == MyCampItemWhere.Inventory)} stored My Camp items on the account");
            return 0;
        }

        if (!int.TryParse(give, NumberStyles.None, CultureInfo.InvariantCulture, out var itemIndex) || itemIndex is < 1 or > ushort.MaxValue)
        {
            throw new CliException($"'{give}' is not a My Camp item id (1 to {ushort.MaxValue}).");
        }

        var count = 1;
        if (commandLine.Option("count") is { } countText && (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count is < 1 or > 100))
        {
            throw new CliException($"'{countText}' is not a count (1 to 100).");
        }

        for (var granted = 0; granted < count; granted++)
        {
            store.AddItemForAccount(character.OwnerAccountId, itemIndex);
        }

        Console.WriteLine($"{character.Name}: added {count} of My Camp item {itemIndex} to the new items of the account; the character sees them in My Camp after the next camp is opened");
        return 0;
    }
}
