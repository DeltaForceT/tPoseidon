using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresMembershipStore : IMembershipStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresMembershipStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public bool SetMember(string accountName, bool member)
    {
        ArgumentException.ThrowIfNullOrEmpty(accountName);
        return Update("LOWER(username) = LOWER(@name)", accountName, member);
    }

    public bool SetMemberOfCharacter(string characterName, bool member)
    {
        ArgumentException.ThrowIfNullOrEmpty(characterName);
        return Update("account_id = (SELECT account_id FROM characters WHERE LOWER(name) = LOWER(@name) LIMIT 1)", characterName, member);
    }

    public IReadOnlyList<string> Members()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT username FROM accounts WHERE is_member ORDER BY LOWER(username)", conn);
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read()) names.Add(reader.GetString(0));
        return names;
    }

    private bool Update(string filter, string name, bool member)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand($"UPDATE accounts SET is_member = @member WHERE {filter}", conn);
        cmd.Parameters.AddWithValue("member", member);
        cmd.Parameters.AddWithValue("name", name);
        return cmd.ExecuteNonQuery() > 0;
    }
}
