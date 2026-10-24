using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresScriptVariableStore : IScriptVariableStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresScriptVariableStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<StoredScriptVariable> Load(byte scope, ulong ownerId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT name, number_value, text_value
            FROM script_variable
            WHERE scope = @scope AND owner_id = @owner_id
            """,
            conn);
        cmd.Parameters.AddWithValue("scope", (short)scope);
        cmd.Parameters.AddWithValue("owner_id", unchecked((long)ownerId));
        using var reader = cmd.ExecuteReader();
        var variables = new List<StoredScriptVariable>();
        while (reader.Read())
        {
            variables.Add(new StoredScriptVariable(reader.GetString(0), reader.GetInt64(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return variables;
    }

    public void Save(byte scope, ulong ownerId, StoredScriptVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO script_variable (scope, owner_id, name, number_value, text_value, updated_at)
            VALUES (@scope, @owner_id, @name, @number_value, @text_value, NOW())
            ON CONFLICT (scope, owner_id, name) DO UPDATE
                SET number_value = EXCLUDED.number_value,
                    text_value   = EXCLUDED.text_value,
                    updated_at   = NOW()
            """,
            conn);
        cmd.Parameters.AddWithValue("scope", (short)scope);
        cmd.Parameters.AddWithValue("owner_id", unchecked((long)ownerId));
        cmd.Parameters.AddWithValue("name", variable.Name);
        cmd.Parameters.AddWithValue("number_value", variable.Number);
        cmd.Parameters.AddWithValue("text_value", (object?)variable.Text ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}
