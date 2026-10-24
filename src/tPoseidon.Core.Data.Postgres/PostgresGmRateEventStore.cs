using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGmRateEventStore : IGmRateEventStore
{
    private const short DirectRow = 1;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresGmRateEventStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public GmRateEventState Load()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        var rows = new List<GmRateEventRow>();
        using (var cmd = new NpgsqlCommand("""
            SELECT event_kind, event_list, position, stat, first_percent, second_percent, start_time, end_time
            FROM gm_rate_event
            ORDER BY event_kind, event_list, position
            """, conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add(new GmRateEventRow(
                    (GmRateEventKind)reader.GetInt16(0), (GmRateEventList)reader.GetInt16(1), reader.GetInt32(2), (byte)reader.GetInt16(3),
                    (ushort)reader.GetInt32(4), (ushort)reader.GetInt32(5), reader.GetInt64(6), reader.GetInt64(7)));
            }
        }
        using var direct = new NpgsqlCommand("SELECT exp_percent, tm_percent FROM gm_rate_direct WHERE id = @id", conn);
        direct.Parameters.AddWithValue("id", DirectRow);
        using var directReader = direct.ExecuteReader();
        return directReader.Read()
            ? new GmRateEventState(rows, (ushort)directReader.GetInt32(0), (ushort)directReader.GetInt32(1))
            : new GmRateEventState(rows, 0, 0);
    }

    public void Save(GmRateEventState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var clear = new NpgsqlCommand("DELETE FROM gm_rate_event", conn, tx))
        {
            clear.ExecuteNonQuery();
        }
        foreach (var row in state.Rows)
        {
            using var insert = new NpgsqlCommand("""
                INSERT INTO gm_rate_event (event_kind, event_list, position, stat, first_percent, second_percent, start_time, end_time)
                VALUES (@kind, @list, @position, @stat, @first, @second, @start, @end)
                """, conn, tx);
            insert.Parameters.AddWithValue("kind", (short)row.Kind);
            insert.Parameters.AddWithValue("list", (short)row.List);
            insert.Parameters.AddWithValue("position", row.Position);
            insert.Parameters.AddWithValue("stat", (short)row.Stat);
            insert.Parameters.AddWithValue("first", (int)row.FirstPercent);
            insert.Parameters.AddWithValue("second", (int)row.SecondPercent);
            insert.Parameters.AddWithValue("start", row.StartTime);
            insert.Parameters.AddWithValue("end", row.EndTime);
            insert.ExecuteNonQuery();
        }
        using (var direct = new NpgsqlCommand("""
            INSERT INTO gm_rate_direct (id, exp_percent, tm_percent) VALUES (@id, @exp, @tm)
            ON CONFLICT (id) DO UPDATE SET exp_percent = EXCLUDED.exp_percent, tm_percent = EXCLUDED.tm_percent
            """, conn, tx))
        {
            direct.Parameters.AddWithValue("id", DirectRow);
            direct.Parameters.AddWithValue("exp", (int)state.DirectExpPercent);
            direct.Parameters.AddWithValue("tm", (int)state.DirectTmPercent);
            direct.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
