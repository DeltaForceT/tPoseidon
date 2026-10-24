using System.Net;
using Npgsql;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresShopAccountStore : IShopAccountStore
{
    private const string Columns = "account_id, username, acc_game_state, acc_game_latest_ip";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresShopAccountStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public ShopAccount? Find(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM accounts WHERE username = @u LIMIT 1", conn);
        cmd.Parameters.AddWithValue("u", username);
        return Read(cmd);
    }

    public ShopAccount? Find(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM accounts WHERE account_id = @a", conn);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        return Read(cmd);
    }

    public IReadOnlyList<ShopAccount> InGameAt(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM accounts WHERE acc_game_state = @s AND acc_game_latest_ip = @ip", conn);
        cmd.Parameters.AddWithValue("s", (short)ShopAccountRules.InGameState);
        cmd.Parameters.AddWithValue("ip", address);
        using var reader = cmd.ExecuteReader();
        var accounts = new List<ShopAccount>();
        while (reader.Read()) accounts.Add(Map(reader));
        return accounts;
    }

    private static ShopAccount? Read(NpgsqlCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    private static ShopAccount Map(NpgsqlDataReader reader)
    {
        return new ShopAccount(
            unchecked((uint)reader.GetInt64(0)),
            reader.GetString(1),
            reader.IsDBNull(2) ? 0 : reader.GetInt16(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<IPAddress>(3));
    }
}
