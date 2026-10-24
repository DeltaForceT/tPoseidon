using Npgsql;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGmAuthorityStore : IGmAuthorityStore
{
    public const string GmAccountAuthority = "B";
    public const string NormalAccountAuthority = "A";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresGmAuthorityStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public bool IsGmClientLoginPermitted(string characterName, string password, string clientIp)
    {
        ArgumentNullException.ThrowIfNull(characterName);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(clientIp);
        if (characterName.Length == 0 || password.Length == 0) return false;

        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT a.password_hash
              FROM tbl_gm_char AS g
              JOIN accounts AS a ON a.account_id = g.account_code
              JOIN tbl_gm_char_permit_ip AS p ON p.account_code = g.account_code
             WHERE LOWER(g.char_name) = LOWER(@name)
               AND TRIM(p.permit_ip) = @ip
             LIMIT 1
            """,
            conn);
        cmd.Parameters.AddWithValue("name", characterName);
        cmd.Parameters.AddWithValue("ip", clientIp);
        return cmd.ExecuteScalar() is string hash
            && hash.Length > 0
            && PasswordHasher.Verify(password, hash);
    }

    public ulong? GmCharacterId(string characterName)
    {
        ArgumentNullException.ThrowIfNull(characterName);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT c.character_id
              FROM tbl_gm_char AS g
              JOIN characters AS c ON c.account_id = g.account_code AND LOWER(c.name) = LOWER(g.char_name)
             WHERE LOWER(g.char_name) = LOWER(@name)
             LIMIT 1
            """,
            conn);
        cmd.Parameters.AddWithValue("name", characterName);
        return cmd.ExecuteScalar() is long id ? unchecked((ulong)id) : null;
    }

    public GmCharacterRegistration? RegisterGmCharacter(string characterName, string permittedIp)
    {
        ArgumentException.ThrowIfNullOrEmpty(characterName);
        ArgumentException.ThrowIfNullOrEmpty(permittedIp);

        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        string storedName;
        uint accountId;
        using (var owner = new NpgsqlCommand(
            "SELECT account_id, name FROM characters WHERE LOWER(name) = LOWER(@name) LIMIT 1",
            conn, tx))
        {
            owner.Parameters.AddWithValue("name", characterName);
            using var reader = owner.ExecuteReader();
            if (!reader.Read()) return null;
            accountId = checked((uint)reader.GetInt64(0));
            storedName = reader.GetString(1);
        }

        using (var authority = new NpgsqlCommand(
            "UPDATE accounts SET acc_game_authority = @gm WHERE account_id = @acc AND acc_game_authority = @normal",
            conn, tx))
        {
            authority.Parameters.AddWithValue("gm", GmAccountAuthority);
            authority.Parameters.AddWithValue("normal", NormalAccountAuthority);
            authority.Parameters.AddWithValue("acc", (long)accountId);
            authority.ExecuteNonQuery();
        }

        using (var gmChar = new NpgsqlCommand(
            """
            INSERT INTO tbl_gm_char (account_code, char_name, permitted_ip, created_date)
            VALUES (@acc, @name, @ip, NOW())
            ON CONFLICT (account_code) DO UPDATE
                SET char_name = EXCLUDED.char_name,
                    permitted_ip = EXCLUDED.permitted_ip
            """,
            conn, tx))
        {
            gmChar.Parameters.AddWithValue("acc", (long)accountId);
            gmChar.Parameters.AddWithValue("name", storedName);
            gmChar.Parameters.AddWithValue("ip", permittedIp);
            gmChar.ExecuteNonQuery();
        }

        using (var permit = new NpgsqlCommand(
            """
            INSERT INTO tbl_gm_char_permit_ip (account_code, permit_ip)
            SELECT @acc, @ip
             WHERE NOT EXISTS (
                SELECT 1 FROM tbl_gm_char_permit_ip WHERE account_code = @acc AND TRIM(permit_ip) = @ip)
            """,
            conn, tx))
        {
            permit.Parameters.AddWithValue("acc", (long)accountId);
            permit.Parameters.AddWithValue("ip", permittedIp);
            permit.ExecuteNonQuery();
        }

        tx.Commit();
        return new GmCharacterRegistration(accountId, storedName, permittedIp);
    }

    public GmCharacterEntry? UnregisterGmCharacter(string characterName)
    {
        ArgumentException.ThrowIfNullOrEmpty(characterName);

        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var entry = ListGmCharacters(conn, tx).FirstOrDefault(gm => string.Equals(gm.CharacterName, characterName, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;

        foreach (var sql in new[]
        {
            "DELETE FROM tbl_gm_char_permit_ip WHERE account_code = @acc",
            "DELETE FROM tbl_gm_char WHERE account_code = @acc",
            "UPDATE accounts SET acc_game_authority = @normal WHERE account_id = @acc AND acc_game_authority = @gm",
        })
        {
            using var command = new NpgsqlCommand(sql, conn, tx);
            command.Parameters.AddWithValue("acc", (long)entry.AccountId);
            command.Parameters.AddWithValue("gm", GmAccountAuthority);
            command.Parameters.AddWithValue("normal", NormalAccountAuthority);
            command.ExecuteNonQuery();
        }

        tx.Commit();
        return entry;
    }

    public IReadOnlyList<GmCharacterEntry> ListGmCharacters()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        return ListGmCharacters(conn, null);
    }

    private static List<GmCharacterEntry> ListGmCharacters(NpgsqlConnection conn, NpgsqlTransaction? tx)
    {
        using var cmd = new NpgsqlCommand(
            """
            SELECT g.account_code, a.username, g.char_name, g.created_date,
                   COALESCE(string_agg(TRIM(p.permit_ip), ',' ORDER BY TRIM(p.permit_ip)) FILTER (WHERE p.permit_ip IS NOT NULL), '')
              FROM tbl_gm_char AS g
              JOIN accounts AS a ON a.account_id = g.account_code
              LEFT JOIN tbl_gm_char_permit_ip AS p ON p.account_code = g.account_code
             GROUP BY g.account_code, a.username, g.char_name, g.created_date
             ORDER BY LOWER(g.char_name)
            """,
            conn, tx);
        using var reader = cmd.ExecuteReader();
        var entries = new List<GmCharacterEntry>();
        while (reader.Read())
        {
            var addresses = reader.GetString(4);
            entries.Add(new GmCharacterEntry(
                checked((uint)reader.GetInt64(0)),
                reader.GetString(1),
                reader.GetString(2),
                addresses.Length == 0 ? Array.Empty<string>() : addresses.Split(','),
                reader.GetFieldValue<DateTimeOffset>(3)));
        }
        return entries;
    }
}
