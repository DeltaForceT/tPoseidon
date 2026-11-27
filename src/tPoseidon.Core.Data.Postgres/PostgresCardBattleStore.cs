using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresCardBattleStore : ICardBattleStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCardBattleStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public CardBattleStats Load(ulong characterId, ulong otherId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using (var insert = new NpgsqlCommand("INSERT INTO cardbattle (char_uid) VALUES (@c) ON CONFLICT DO NOTHING", conn))
        {
            insert.Parameters.AddWithValue("c", (long)characterId);
            insert.ExecuteNonQuery();
        }
        using var cmd = new NpgsqlCommand("""
            SELECT winpoint, wincount, battlecount, dis, specialdis,
                (SELECT COUNT(DISTINCT other_uid) FROM cardbattle_count WHERE char_uid = @c AND created_at::date = CURRENT_DATE),
                (SELECT COUNT(*) FROM cardbattle_count WHERE char_uid = @c AND other_uid = @o AND created_at::date = CURRENT_DATE)
            FROM cardbattle WHERE char_uid = @c
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)characterId);
        cmd.Parameters.AddWithValue("o", (long)otherId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return CardBattleStats.Empty;
        return new CardBattleStats(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4),
            (int)reader.GetInt64(5), (int)reader.GetInt64(6));
    }

    public void Record(ulong characterId, ulong otherId, bool won, int winPointChange)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var update = new NpgsqlCommand("""
            INSERT INTO cardbattle (char_uid, winpoint, wincount, battlecount) VALUES (@c, GREATEST(0, @p), @w, 1)
            ON CONFLICT (char_uid) DO UPDATE SET winpoint = GREATEST(0, cardbattle.winpoint + @p), wincount = cardbattle.wincount + @w, battlecount = cardbattle.battlecount + 1
            """, conn, tx))
        {
            update.Parameters.AddWithValue("c", (long)characterId);
            update.Parameters.AddWithValue("w", won ? 1 : 0);
            update.Parameters.AddWithValue("p", winPointChange);
            update.ExecuteNonQuery();
        }
        using (var history = new NpgsqlCommand("INSERT INTO cardbattle_count (char_uid, other_uid) VALUES (@c, @o)", conn, tx))
        {
            history.Parameters.AddWithValue("c", (long)characterId);
            history.Parameters.AddWithValue("o", (long)otherId);
            history.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void RecordClosed(ulong characterId, ulong otherId, int disIncrease, int specialDisIncrease)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var update = new NpgsqlCommand("UPDATE cardbattle SET dis = dis + @d, specialdis = specialdis + @s WHERE char_uid = @c", conn, tx))
        {
            update.Parameters.AddWithValue("c", (long)characterId);
            update.Parameters.AddWithValue("d", disIncrease);
            update.Parameters.AddWithValue("s", specialDisIncrease);
            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException($"No card battle record for character 0x{characterId:X}.");
        }
        using (var history = new NpgsqlCommand("INSERT INTO cardbattle_count (char_uid, other_uid) VALUES (@c, @o)", conn, tx))
        {
            history.Parameters.AddWithValue("c", (long)characterId);
            history.Parameters.AddWithValue("o", (long)otherId);
            history.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
