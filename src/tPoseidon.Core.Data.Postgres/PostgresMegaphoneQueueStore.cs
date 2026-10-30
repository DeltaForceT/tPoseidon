using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresMegaphoneQueueStore : IMegaphoneQueueStore
{
    private const string RowColumns = "broadcast_code, char_uid, message, broadcast_type, message_flag, refund_item_index, refund_item_class";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresMegaphoneQueueStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public int Enqueue(ulong characterId, string message, MegaphoneRefund refund) =>
        Insert(characterId, message, MegaphoneKinds.Normal, MegaphoneFlags.Queued, refund);

    public int EnqueueNotice(string message) =>
        Insert(MegaphoneKinds.NoticeSender, message, MegaphoneKinds.Notice, MegaphoneFlags.Waiting, new MegaphoneRefund(0, 0));

    private int Insert(ulong characterId, string message, byte kind, byte flag, MegaphoneRefund refund)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        int waiting;
        using (var count = new NpgsqlCommand("SELECT COUNT(*) FROM broadcast WHERE message_flag <= @queued", conn, tx))
        {
            count.Parameters.AddWithValue("queued", (short)MegaphoneFlags.Queued);
            waiting = checked((int)(long)count.ExecuteScalar()!);
        }
        using (var insert = new NpgsqlCommand("""
            INSERT INTO broadcast (char_uid, island_code, message, message_flag, broadcast_type, refund_item_index, refund_item_class)
            VALUES (@c, 0, @m, @f, @t, @ri, @rc)
            """, conn, tx))
        {
            insert.Parameters.AddWithValue("c", unchecked((long)characterId));
            insert.Parameters.AddWithValue("m", message);
            insert.Parameters.AddWithValue("f", (short)flag);
            insert.Parameters.AddWithValue("t", (short)kind);
            insert.Parameters.AddWithValue("ri", refund.ItemIndex);
            insert.Parameters.AddWithValue("rc", (short)refund.ItemClass);
            insert.ExecuteNonQuery();
        }
        tx.Commit();
        return waiting;
    }

    public QueuedMegaphone? NextPending()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            $"SELECT {RowColumns} FROM broadcast WHERE message_flag <= @queued ORDER BY broadcast_code LIMIT 1", conn);
        cmd.Parameters.AddWithValue("queued", (short)MegaphoneFlags.Queued);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public bool MarkDelivered(long code)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var update = new NpgsqlCommand(
            "UPDATE broadcast SET message_flag = @done WHERE broadcast_code = @code AND message_flag <= @queued", conn, tx))
        {
            update.Parameters.AddWithValue("done", (short)MegaphoneFlags.Delivered);
            update.Parameters.AddWithValue("code", code);
            update.Parameters.AddWithValue("queued", (short)MegaphoneFlags.Queued);
            if (update.ExecuteNonQuery() != 1) return false;
        }
        Log(conn, tx, code, MegaphoneFlags.Delivered);
        tx.Commit();
        return true;
    }

    public IReadOnlyList<QueuedMegaphone> Interrupted()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand($"SELECT {RowColumns} FROM broadcast WHERE message_flag = @queued ORDER BY broadcast_code", conn);
        cmd.Parameters.AddWithValue("queued", (short)MegaphoneFlags.Queued);
        using var reader = cmd.ExecuteReader();
        var rows = new List<QueuedMegaphone>();
        while (reader.Read()) rows.Add(Read(reader));
        return rows;
    }

    public void Settle(long code, byte flag)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var update = new NpgsqlCommand("UPDATE broadcast SET message_flag = @f WHERE broadcast_code = @code", conn, tx))
        {
            update.Parameters.AddWithValue("f", (short)flag);
            update.Parameters.AddWithValue("code", code);
            update.ExecuteNonQuery();
        }
        Log(conn, tx, code, flag);
        tx.Commit();
    }

    private static void Log(NpgsqlConnection conn, NpgsqlTransaction tx, long code, byte flag)
    {
        using var insert = new NpgsqlCommand("INSERT INTO broadcast_state_log (broadcast_code, message_flag) VALUES (@code, @f)", conn, tx);
        insert.Parameters.AddWithValue("code", code);
        insert.Parameters.AddWithValue("f", (short)flag);
        insert.ExecuteNonQuery();
    }

    private static QueuedMegaphone Read(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        unchecked((ulong)reader.GetInt64(1)),
        reader.GetString(2),
        (byte)reader.GetInt16(3),
        (byte)reader.GetInt16(4),
        new MegaphoneRefund(reader.GetInt32(5), (byte)reader.GetInt16(6)));
}
