using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed record CharacterSummary(ulong CharacterId, string Name, string AccountName, int Level, int MapId);

public sealed class PostgresCharacterSearch
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCharacterSearch(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<CharacterSummary> Find(string text, int limit)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT c.character_id, c.name, a.username, c.level, c.map_id
              FROM characters AS c
              JOIN accounts AS a ON a.account_id = c.account_id
             WHERE c.name ILIKE @pattern ESCAPE '\' OR a.username ILIKE @pattern ESCAPE '\'
             ORDER BY POSITION(LOWER(@text) IN LOWER(c.name)) = 1 DESC, LOWER(c.name)
             LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("pattern", "%" + Escape(text.Trim()) + "%");
        cmd.Parameters.AddWithValue("text", text.Trim());
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var found = new List<CharacterSummary>();
        while (reader.Read())
        {
            found.Add(new CharacterSummary(unchecked((ulong)reader.GetInt64(0)), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4)));
        }
        return found;
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
