using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public static class PostgresStoreBootstrap
{
        public sealed record Bootstrap(NpgsqlDataSource DataSource, PostgresAccountStore Store, SchemaSwitchListener SchemaListener, PostgresServerRegistryStore Registry, PostgresGmAuthorityStore GmAuthority);

    public static async Task<Bootstrap> InitializeAsync(CancellationToken ct = default)
    {
        var connStr = PostgresAccountStore.ResolveConnectionString();
        var schema = PostgresAccountStore.ResolveSchema();
        var dataSource = NpgsqlDataSource.Create(connStr);

        ActiveSchemaProvider.Set(schema);

        var migrationsDir = PostgresMigrations.DefaultMigrationsDirectory();

        await PostgresMigrations.ApplyAsync(dataSource, migrationsDir, schema, ct).ConfigureAwait(false);

        await ApplyOperatorRegistryConfigAsync(dataSource, ct).ConfigureAwait(false);

        var store = new PostgresAccountStore(dataSource);
        var registry = new PostgresServerRegistryStore(dataSource);
        var gmAuthority = new PostgresGmAuthorityStore(dataSource);

        Console.WriteLine($"[postgres] active schema: {ActiveSchemaProvider.Current} (per-client isolation; hot-switchable via NOTIFY mt_schema_switch)");

        var listener = new SchemaSwitchListener(connStr);
        listener.Start();

        return new Bootstrap(dataSource, store, listener, registry, gmAuthority);
    }

    private static async Task ApplyOperatorRegistryConfigAsync(
        NpgsqlDataSource dataSource, CancellationToken ct)
    {
        string? Env(string n)
        {
            var v = Environment.GetEnvironmentVariable(n);
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        var ip = Env("MT_PUBLIC_IP");
        var worldName = Env("MT_WORLD_NAME");
        var channelName = Env("MT_CHANNEL_NAME");

        if (ip is null && worldName is null && channelName is null)
        {
            return;
        }

        try
        {
            await using var conn = await dataSource.OpenConnectionForActiveSchemaAsync(ct).ConfigureAwait(false);

            if (worldName is not null || ip is not null)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    UPDATE tbl_world
                       SET world_name   = COALESCE(@wn, world_name),
                           world_ls_ip  = COALESCE(@ip, world_ls_ip)
                     WHERE game_code = 1 AND world_code = 1
                    """, conn);
                cmd.Parameters.AddWithValue("wn", (object?)worldName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ip", (object?)ip ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            if (channelName is not null || ip is not null)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    UPDATE tbl_island
                       SET island_name  = COALESCE(@cn, island_name),
                           island_ls_ip = COALESCE(@ip, island_ls_ip)
                     WHERE game_code = 1 AND world_code = 1 AND island_code = 1
                    """, conn);
                cmd.Parameters.AddWithValue("cn", (object?)channelName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ip", (object?)ip ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            if (ip is not null)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    UPDATE tbl_parm_server
                       SET major_ip = @ip
                     WHERE svr_no IN (1, 2, 3, 4, 5)
                    """, conn);
                cmd.Parameters.AddWithValue("ip", ip);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            Console.WriteLine(
                $"[postgres] applied ControlPanel server-identity config to registry " +
                $"(ip={ip ?? "(unchanged)"}, world={worldName ?? "(unchanged)"}, " +
                $"channel={channelName ?? "(unchanged)"})");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine(
                $"[postgres] WARN: could not apply ControlPanel registry config " +
                $"({ex.GetType().Name}: {ex.Message}); registry keeps prior rows, " +
                $"Tier-A1 fallback still active -- server start NOT blocked");
        }
    }
}
