using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresUniqueItemStore : IUniqueItemStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresUniqueItemStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<UniqueItemInfo> MasterRestart()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        Execute(conn, tx, "UPDATE unique_item_info SET server_count = user_count WHERE server_count > user_count");
        Execute(conn, tx, "DELETE FROM unique_item_info WHERE item_index > 0 AND update_flag = 'R'");
        Execute(conn, tx, """
            UPDATE unique_item_info ui SET update_flag = 'T'
            WHERE ui.update_flag IN ('A', 'M')
              AND EXISTS (SELECT 1 FROM unique_item_target ut WHERE ut.item_index = ui.item_index AND ut.target_flag = 1)
            """);
        var rows = new List<UniqueItemInfo>();
        using (var cmd = new NpgsqlCommand("""
            SELECT item_index, unique_type, limit_count, total_count, server_count, user_count, base_count, current_count, update_flag, day_limit_count
            FROM unique_item_info ORDER BY item_index
            """, conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add(new UniqueItemInfo(reader.GetInt32(0), (byte)reader.GetInt16(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4),
                    reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetString(8)[0], reader.GetInt32(9)));
            }
        }
        tx.Commit();
        return rows;
    }

    public bool BossKilled(ulong charUid, int monsterId, DateTimeOffset now)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        Execute(conn, tx, """
            INSERT INTO bossmon (char_uid, mon_id, kill_count, latest_update) VALUES (@ch, @m, 1, @now)
            ON CONFLICT (char_uid, mon_id) DO UPDATE SET kill_count = bossmon.kill_count + 1, latest_update = EXCLUDED.latest_update
            """, ("ch", unchecked((long)charUid)), ("m", monsterId), ("now", now));
        Execute(conn, tx, "INSERT INTO bossmon_history (char_uid, mon_id, kill_date) VALUES (@ch, @m, @now)",
            ("ch", unchecked((long)charUid)), ("m", monsterId), ("now", now));
        var credited = false;
        if (UniqueItemRules.EarnsSeal(monsterId))
        {
            using var cmd = new NpgsqlCommand("""
                UPDATE unique_char_duty_info SET bossmonmedal_count = bossmonmedal_count + 1
                WHERE char_uid = @ch AND duty_end_date >= @now
                """, conn, tx);
            cmd.Parameters.AddWithValue("ch", unchecked((long)charUid));
            cmd.Parameters.AddWithValue("now", now);
            credited = cmd.ExecuteNonQuery() > 0;
        }
        tx.Commit();
        return credited;
    }

    public IReadOnlyList<BossKillRecord> LatestBossKills(ulong charUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT mon_id, latest_update FROM bossmon WHERE char_uid = @ch", conn);
        cmd.Parameters.AddWithValue("ch", unchecked((long)charUid));
        using var reader = cmd.ExecuteReader();
        var kills = new List<BossKillRecord>();
        while (reader.Read())
        {
            kills.Add(new BossKillRecord(reader.GetInt32(0), new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc))));
        }
        return kills;
    }

    public bool AddCount(int itemIndex, int units, DateTimeOffset now)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        int current, baseCount;
        using (var count = new NpgsqlCommand("""
            UPDATE unique_item_info SET current_count = current_count + @n
            WHERE item_index = @i AND update_flag <> 'R'
            RETURNING current_count, base_count
            """, conn, tx))
        {
            count.Parameters.AddWithValue("n", units);
            count.Parameters.AddWithValue("i", itemIndex);
            using var reader = count.ExecuteReader();
            if (!reader.Read()) return false;
            current = reader.GetInt32(0);
            baseCount = reader.GetInt32(1);
        }
        (short Order, int Count)? target = null;
        using (var active = new NpgsqlCommand("""
            SELECT target_order, target_count FROM unique_item_target WHERE item_index = @i AND target_flag = @active ORDER BY target_order LIMIT 1
            """, conn, tx))
        {
            active.Parameters.AddWithValue("i", itemIndex);
            active.Parameters.AddWithValue("active", UniqueItemRules.ActiveTarget);
            using var reader = active.ExecuteReader();
            if (reader.Read()) target = (reader.GetInt16(0), reader.GetInt32(1));
        }
        if (target is null)
        {
            NewTarget(conn, tx, itemIndex, 1, baseCount);
            tx.Commit();
            return false;
        }
        if (target.Value.Count > current)
        {
            tx.Commit();
            return false;
        }
        Execute(conn, tx, "UPDATE unique_item_info SET total_count = total_count + 1, server_count = server_count + 1, update_flag = 'M' WHERE item_index = @i", ("i", itemIndex));
        Execute(conn, tx, "UPDATE unique_item_target SET target_flag = @used WHERE item_index = @i AND target_order = @o",
            ("used", UniqueItemRules.UsedTarget), ("i", itemIndex), ("o", target.Value.Order));
        var activated = Execute(conn, tx, """
            UPDATE unique_item_target SET target_flag = @active
            WHERE item_index = @i AND target_order = (SELECT MIN(target_order) FROM unique_item_target WHERE item_index = @i AND target_order > @o AND target_flag = 0)
            """, ("active", UniqueItemRules.ActiveTarget), ("i", itemIndex), ("o", target.Value.Order));
        if (activated == 0)
        {
            NewTarget(conn, tx, itemIndex, (short)(target.Value.Order + 1), baseCount);
        }
        Execute(conn, tx, "INSERT INTO unique_item_log (log_type, item_index, char_uid, current_count, target_order, log_date) VALUES (@t, @i, 0, @c, @o, @at)",
            ("t", UniqueItemRules.IssueLog), ("i", itemIndex), ("c", current), ("o", target.Value.Order), ("at", now.UtcDateTime));
        Execute(conn, tx, "UPDATE unique_item_info SET current_count = 0 WHERE item_index = @i AND limit_count > server_count", ("i", itemIndex));
        tx.Commit();
        return true;
    }

    private static void NewTarget(NpgsqlConnection conn, NpgsqlTransaction tx, int itemIndex, short order, int baseCount)
    {
        var count = UniqueItemRules.RandomTarget(baseCount, Random.Shared.Next);
        Execute(conn, tx, """
            INSERT INTO unique_item_target (item_index, target_order, target_count, target_flag) VALUES (@i, @o, @c, @active)
            ON CONFLICT (item_index, target_order) DO UPDATE SET target_count = EXCLUDED.target_count, target_flag = EXCLUDED.target_flag
            """, ("i", itemIndex), ("o", order), ("c", count), ("active", UniqueItemRules.ActiveTarget));
    }

    public UniquePickup? PickedUp(ulong charUid, ulong itemUid, int itemIndex, DateTimeOffset now)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        byte uniqueType;
        int currentCount;
        using (var cmd = new NpgsqlCommand("""
            UPDATE unique_item_info SET user_count = user_count + 1 WHERE item_index = @i
            RETURNING unique_type, current_count
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("i", itemIndex);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            uniqueType = (byte)reader.GetInt16(0);
            currentCount = reader.GetInt32(1);
        }
        DateTimeOffset? end = null;
        if (UniqueItemRules.CarriesDuty(uniqueType))
        {
            end = now + UniqueItemRules.DutyPeriod;
            Execute(conn, tx, """
                INSERT INTO unique_char_duty_info (char_uid, bossmonmedal_count, unique_item_count, duty_end_date)
                VALUES (@ch, 0, 1, @end)
                ON CONFLICT (char_uid) DO UPDATE SET unique_item_count = unique_char_duty_info.unique_item_count + 1, duty_end_date = EXCLUDED.duty_end_date
                """, ("ch", unchecked((long)charUid)), ("end", end.Value));
            Execute(conn, tx, """
                INSERT INTO unique_item_char (item_uid, item_index, char_uid, pickup_date) VALUES (@u, @i, @ch, @now)
                ON CONFLICT (item_uid) DO NOTHING
                """, ("u", unchecked((long)itemUid)), ("i", itemIndex), ("ch", unchecked((long)charUid)), ("now", now));
        }
        Execute(conn, tx, """
            INSERT INTO unique_item_log (log_type, item_index, char_uid, current_count, target_order)
            VALUES (@t, @i, @ch, @c, (SELECT MAX(target_order) FROM unique_item_target WHERE item_index = @i AND target_flag = 2))
            """, ("t", (short)UniqueItemRules.PickupLog), ("i", itemIndex), ("ch", unchecked((long)charUid)), ("c", currentCount));
        tx.Commit();
        return new UniquePickup(uniqueType, end);
    }

    public UniqueDutyOutcome ApplyDuty(bool deprive, DateTimeOffset now)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var due = new List<(ulong CharUid, int Medals, int Count, DateTimeOffset End)>();
        using (var cmd = new NpgsqlCommand("""
            SELECT char_uid, bossmonmedal_count, unique_item_count, duty_end_date FROM unique_char_duty_info
            WHERE duty_end_date <= @now FOR UPDATE
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("now", now);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) due.Add(((ulong)reader.GetInt64(0), reader.GetInt16(1), reader.GetInt16(2), reader.GetFieldValue<DateTimeOffset>(3)));
        }
        var accomplished = new List<(ulong, DateTimeOffset)>();
        var failed = new List<ulong>();
        foreach (var duty in due)
        {
            if (UniqueItemRules.DutyAccomplished(duty.Medals, duty.Count))
            {
                LogDuty(conn, tx, UniqueItemRules.DutyAccomplishedLog, duty.CharUid, duty.Medals, duty.Count, 0, duty.End);
                var end = now + UniqueItemRules.DutyPeriod;
                Execute(conn, tx, "UPDATE unique_char_duty_info SET bossmonmedal_count = 0, duty_end_date = @end WHERE char_uid = @ch",
                    ("end", end), ("ch", unchecked((long)duty.CharUid)));
                accomplished.Add((duty.CharUid, end));
                continue;
            }
            if (deprive) DepriveAllIn(conn, tx, duty.CharUid);
            failed.Add(duty.CharUid);
        }
        Execute(conn, tx, """
            INSERT INTO unique_item_schedule_log (log_type, start_time, end_time, duty_success_count, duty_fail_count)
            VALUES (@t, @start, NOW(), @ok, @fail)
            """, ("t", (short)UniqueItemRules.DutyScheduleLog), ("start", now), ("ok", accomplished.Count), ("fail", failed.Count));
        tx.Commit();
        return new UniqueDutyOutcome(accomplished, failed);
    }

    public IReadOnlyList<UniqueHeldItem> DepriveAll(ulong charUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var taken = DepriveAllIn(conn, tx, charUid);
        tx.Commit();
        return taken;
    }

    public IReadOnlyList<UniqueHeldItem> DepriveSingle(ulong charUid, IReadOnlyList<UniqueHeldItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var held = Held(conn, tx, charUid);
        var taken = items.Take(UniqueItemRules.MaxWithdrawnPerRequest).Distinct().Where(held.Contains).ToList();
        if (taken.Count == 0) return taken;
        var duty = Duty(conn, tx, charUid);
        LogDuty(conn, tx, UniqueItemRules.DutyDeprivedLog, charUid, duty?.Medals ?? 0, duty?.Count ?? 0, taken.Count, duty?.End ?? DateTimeOffset.UtcNow);
        Remove(conn, tx, taken);
        if (duty is { } current && current.Count > taken.Count)
        {
            Execute(conn, tx, "UPDATE unique_char_duty_info SET unique_item_count = unique_item_count - @n WHERE char_uid = @ch",
                ("n", (short)taken.Count), ("ch", unchecked((long)charUid)));
        }
        else
        {
            Execute(conn, tx, "DELETE FROM unique_char_duty_info WHERE char_uid = @ch", ("ch", unchecked((long)charUid)));
        }
        tx.Commit();
        return taken;
    }

    private static List<UniqueHeldItem> DepriveAllIn(NpgsqlConnection conn, NpgsqlTransaction tx, ulong charUid)
    {
        var held = Held(conn, tx, charUid);
        if (held.Count == 0) return held;
        var duty = Duty(conn, tx, charUid);
        if (duty is { } current) LogDuty(conn, tx, UniqueItemRules.DutyDeprivedLog, charUid, current.Medals, current.Count, held.Count, current.End);
        Remove(conn, tx, held);
        Execute(conn, tx, "DELETE FROM unique_char_duty_info WHERE char_uid = @ch", ("ch", unchecked((long)charUid)));
        return held;
    }

    private static void Remove(NpgsqlConnection conn, NpgsqlTransaction tx, IReadOnlyList<UniqueHeldItem> items)
    {
        foreach (var item in items)
        {
            Execute(conn, tx, "UPDATE unique_item_info SET server_count = server_count - 1, user_count = user_count - 1 WHERE item_index = @i", ("i", item.ItemIndex));
            Execute(conn, tx, "DELETE FROM item_char WHERE item_uid = @u", ("u", unchecked((long)item.ItemUid)));
            Execute(conn, tx, "DELETE FROM unique_item_char WHERE item_uid = @u", ("u", unchecked((long)item.ItemUid)));
            Execute(conn, tx, "DELETE FROM items WHERE item_uid = @u", ("u", unchecked((long)item.ItemUid)));
        }
    }

    private static List<UniqueHeldItem> Held(NpgsqlConnection conn, NpgsqlTransaction tx, ulong charUid)
    {
        using var cmd = new NpgsqlCommand("SELECT item_uid, item_index FROM unique_item_char WHERE char_uid = @ch ORDER BY item_uid FOR UPDATE", conn, tx);
        cmd.Parameters.AddWithValue("ch", unchecked((long)charUid));
        using var reader = cmd.ExecuteReader();
        var held = new List<UniqueHeldItem>();
        while (reader.Read()) held.Add(new UniqueHeldItem((ulong)reader.GetInt64(0), reader.GetInt32(1)));
        return held;
    }

    private static (int Medals, int Count, DateTimeOffset End)? Duty(NpgsqlConnection conn, NpgsqlTransaction tx, ulong charUid)
    {
        using var cmd = new NpgsqlCommand("SELECT bossmonmedal_count, unique_item_count, duty_end_date FROM unique_char_duty_info WHERE char_uid = @ch FOR UPDATE", conn, tx);
        cmd.Parameters.AddWithValue("ch", unchecked((long)charUid));
        using var reader = cmd.ExecuteReader();
        return reader.Read() && !reader.IsDBNull(2) ? (reader.GetInt16(0), reader.GetInt16(1), reader.GetFieldValue<DateTimeOffset>(2)) : null;
    }

    private static void LogDuty(NpgsqlConnection conn, NpgsqlTransaction tx, int logType, ulong charUid, int medals, int count, int deprived, DateTimeOffset end) =>
        Execute(conn, tx, """
            INSERT INTO unique_item_duty_log (log_type, char_uid, bossmonmedal_count, unique_item_count, unique_item_deprive_count, duty_end_date)
            VALUES (@t, @ch, @m, @c, @d, @end)
            """, ("t", (short)logType), ("ch", unchecked((long)charUid)), ("m", (short)medals), ("c", (short)count), ("d", (short)deprived), ("end", end));

    private static int Execute(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        return cmd.ExecuteNonQuery();
    }
}
