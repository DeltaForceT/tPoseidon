using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public static class NpgsqlDataSourceExtensions
{
        public static async Task<NpgsqlConnection> OpenConnectionForActiveSchemaAsync(
        this NpgsqlDataSource dataSource,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        var conn = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            await ApplySearchPathAsync(conn, ct).ConfigureAwait(false);
            return conn;
        }
        catch
        {
            await conn.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

        public static NpgsqlConnection OpenConnectionForActiveSchema(
        this NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        var conn = dataSource.OpenConnection();
        try
        {
            ApplySearchPath(conn);
            return conn;
        }
        catch
        {
            conn.Dispose();
            throw;
        }
    }

    private static async Task ApplySearchPathAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var schema = ActiveSchemaProvider.Current;

        

        await using var cmd = new NpgsqlCommand($"SET search_path TO \"{schema}\", public", conn);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void ApplySearchPath(NpgsqlConnection conn)
    {
        var schema = ActiveSchemaProvider.Current;
        using var cmd = new NpgsqlCommand($"SET search_path TO \"{schema}\", public", conn);
        cmd.ExecuteNonQuery();
    }
}
