using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresCharacterDirectory : ICharacterDirectory
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCharacterDirectory(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public ulong? FindCharacterId(string characterName)
    {
        ArgumentNullException.ThrowIfNull(characterName);
        if (characterName.Length == 0) return null;
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT character_id FROM characters WHERE LOWER(name) = LOWER(@name) LIMIT 1", conn);
        cmd.Parameters.AddWithValue("name", characterName);
        return cmd.ExecuteScalar() is long id ? unchecked((ulong)id) : null;
    }
}
