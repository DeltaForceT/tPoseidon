using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using tPoseidon.DrillEgg.Config;

namespace tPoseidon.DrillEgg.Connectors;

internal static class SqlProviders
{
    public const string Postgres = "postgres";
    public const string SqlServer = "sqlserver";
    public const string MySql = "mysql";
    public const string Sqlite = "sqlite";

    public static readonly IReadOnlyList<string> Names = [Postgres, SqlServer, MySql, Sqlite];

    public static bool IsKnown(string provider) => Names.Contains(provider, StringComparer.Ordinal);

    public static DbProviderFactory Factory(string provider) => provider switch
    {
        Postgres => NpgsqlFactory.Instance,
        SqlServer => SqlClientFactory.Instance,
        MySql => MySqlConnectorFactory.Instance,
        Sqlite => SqliteFactory.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown database provider."),
    };
}

internal static class DrawValues
{
    public const string Account = "account";
    public const string AccountId = "accountId";
    public const string Character = "character";
    public const string Password = "password";
    public const string Token = "token";
    public const string Cost = "cost";
    public const string DrawId = "drawId";
    public const string ItemId = "itemId";
    public const string ItemName = "itemName";
    public const string Count = "count";
    public const string ItemClass = "itemClass";
    public const string UseHours = "useHours";
    public const string ItemFlag = "itemFlag";
    public const string Area = "area";
    public const string Deep = "deep";

    public static readonly IReadOnlyList<string> SignIn = [Account, Password, Token];
    public static readonly IReadOnlyList<string> CouponCount = [Account, AccountId, Character];
    public static readonly IReadOnlyList<string> CouponSpend = [Account, AccountId, Character, Cost, DrawId];
    public static readonly IReadOnlyList<string> Deliver = [Account, AccountId, Character, ItemId, ItemName, Count, ItemClass, UseHours, ItemFlag, DrawId, Area, Deep];

    public static Dictionary<string, object?> Of(GachaPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [Account] = player.Account,
            [AccountId] = player.AccountId,
            [Character] = player.Character,
        };
    }

    public static Dictionary<string, object?> Of(Delivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var values = Of(delivery.Player);
        values[ItemId] = delivery.Prize.ItemIndex;
        values[ItemName] = delivery.ItemName;
        values[Count] = (int)delivery.Prize.Count;
        values[ItemClass] = (int)delivery.Prize.ItemClass;
        values[UseHours] = (int)delivery.Prize.UseTimeHours;
        values[ItemFlag] = (int)delivery.Prize.ItemFlag;
        values[DrawId] = delivery.DrawId;
        values[Area] = (int)delivery.Area;
        values[Deep] = delivery.Deep;
        return values;
    }
}

internal sealed partial class SqlDatabase
{
    private readonly DbProviderFactory _factory;
    private readonly string _connection;

    public SqlDatabase(string name, DbProviderFactory factory, string connection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(connection);
        Name = name;
        _factory = factory;
        _connection = connection;
    }

    public string Name { get; }

    public static SqlDatabase From(string name, DatabaseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new SqlDatabase(name, SqlProviders.Factory(settings.Provider), settings.Connection);
    }

    public static IReadOnlyList<string> ParameterNames(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return ParameterPattern().Matches(sql).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<string> UnknownParameters(string sql, IReadOnlyList<string> known) =>
        ParameterNames(sql).Where(name => !known.Contains(name, StringComparer.Ordinal)).ToList();

    public async Task<DbConnection> OpenAsync(CancellationToken ct)
    {
        var connection = _factory.CreateConnection() ?? throw new InvalidOperationException($"The {Name} database provider made no connection.");
        connection.ConnectionString = _connection;
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public static DbCommand Command(DbConnection connection, DbTransaction? transaction, string sql, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(values);
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var name in ParameterNames(sql))
        {
            if (!values.TryGetValue(name, out var value)) continue;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    [GeneratedRegex(@"(?<![@\w])@([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterPattern();
}
