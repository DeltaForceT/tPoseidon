using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresCoupleRulesStore : ICoupleRulesStore
{
    private const string CoupleColumns = """
        couple_code, couple_state, relation_point, love_point, friend_point, advice_index1, advice_index2,
        ring_item_index, dress_item_index, title_index, begin_couple_date, begin_marriage_date, recent_update_at
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresCoupleRulesStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public CoupleRegistration Register(ulong maleCharacterId, ulong femaleCharacterId, DateTimeOffset at)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var exists = new NpgsqlCommand("""
            SELECT 1 FROM couple c JOIN couple_join j ON j.couple_code = c.couple_code
            WHERE c.couple_authority = FALSE AND j.char_uid IN (@m, @f) LIMIT 1
            """, conn, tx))
        {
            exists.Parameters.AddWithValue("m", (long)maleCharacterId);
            exists.Parameters.AddWithValue("f", (long)femaleCharacterId);
            if (exists.ExecuteScalar() is not null) return new CoupleRegistration(CoupleStoreError.AlreadyCoupled, 0);
        }
        long code;
        using (var insert = new NpgsqlCommand("""
            INSERT INTO couple (couple_authority, title_index, ring_item_index, dress_item_index, advice_index1, advice_index2,
                                couple_state, relation_point, love_point, friend_point, recent_update_at, created_at,
                                begin_couple_date, begin_marriage_date, divorce_request_date, divorce_completed_date)
            VALUES (FALSE, 0, 0, 0, 0, 0, @state, @relation, @love, @friend, @at, @at, @at, @at, @at, @at)
            RETURNING couple_code
            """, conn, tx))
        {
            insert.Parameters.AddWithValue("state", (short)CoupleStates.Joined);
            insert.Parameters.AddWithValue("relation", (short)CoupleStates.StartRelationPoint);
            insert.Parameters.AddWithValue("love", (short)CoupleStates.StartLovePoint);
            insert.Parameters.AddWithValue("friend", (short)CoupleStates.StartFriendPoint);
            insert.Parameters.AddWithValue("at", at.UtcDateTime);
            code = (long)insert.ExecuteScalar()!;
        }
        foreach (var (characterId, female) in new[] { (maleCharacterId, false), (femaleCharacterId, true) })
        {
            using var join = new NpgsqlCommand("""
                INSERT INTO couple_join (couple_code, char_uid, char_sex, event_bitmask, divorce_state) VALUES (@c, @u, @s, 0, 0)
                """, conn, tx);
            join.Parameters.AddWithValue("c", code);
            join.Parameters.AddWithValue("u", (long)characterId);
            join.Parameters.AddWithValue("s", female);
            join.ExecuteNonQuery();
            using var character = new NpgsqlCommand("""
                INSERT INTO couple_char (char_uid, couple_code, couple_count, created_at) VALUES (@u, @c, 1, NOW())
                ON CONFLICT (char_uid) DO UPDATE SET couple_code = EXCLUDED.couple_code, couple_count = couple_char.couple_count + 1
                """, conn, tx);
            character.Parameters.AddWithValue("u", (long)characterId);
            character.Parameters.AddWithValue("c", code);
            character.ExecuteNonQuery();
        }
        LogState(conn, tx, code, CoupleStates.Joined, at);
        tx.Commit();
        return new CoupleRegistration(CoupleStoreError.None, (ulong)code);
    }

    public CoupleCharInfo? CharInfo(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT couple_code, divorce_count, wedding_in_count FROM couple_char WHERE char_uid = @u", conn);
        cmd.Parameters.AddWithValue("u", (long)characterId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? new CoupleCharInfo((ulong)reader.GetInt64(0), reader.GetInt16(1), reader.GetInt16(2)) : null;
    }

    public CoupleRecord? Load(ulong coupleCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        return Load(conn, coupleCode);
    }

    public CoupleRecord? LoadForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT couple_code FROM couple_char WHERE char_uid = @u", conn);
        cmd.Parameters.AddWithValue("u", (long)characterId);
        return cmd.ExecuteScalar() is long code && code != 0 ? Load(conn, (ulong)code) : null;
    }

    public bool RequestDivorce(ulong coupleCode, ulong requesterId, ulong receiverId, DateTimeOffset at)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        if (!SetState(conn, tx, coupleCode, CoupleStates.DivorceRequested, "divorce_request_date = @at", at)) return false;
        SetDivorceState(conn, tx, coupleCode, requesterId, CoupleStates.DivorceRequester);
        SetDivorceState(conn, tx, coupleCode, receiverId, CoupleStates.DivorceReceiver);
        LogState(conn, tx, (long)coupleCode, CoupleStates.DivorceRequested, at);
        tx.Commit();
        return true;
    }

    public bool CancelDivorce(ulong coupleCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var now = DateTimeOffset.UtcNow;
        if (!SetState(conn, tx, coupleCode, CoupleStates.Married, "divorce_request_date = begin_marriage_date", now)) return false;
        using (var members = new NpgsqlCommand("UPDATE couple_join SET divorce_state = 0 WHERE couple_code = @c", conn, tx))
        {
            members.Parameters.AddWithValue("c", (long)coupleCode);
            members.ExecuteNonQuery();
        }
        LogState(conn, tx, (long)coupleCode, CoupleStates.DivorceCancelled, now);
        tx.Commit();
        return true;
    }

    public bool ConfirmDivorce(ulong coupleCode, DateTimeOffset at)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        if (!SetState(conn, tx, coupleCode, CoupleStates.Divorced, "divorce_completed_date = @at", at)) return false;
        AddCount(conn, tx, coupleCode, "divorce_count");
        LogState(conn, tx, (long)coupleCode, CoupleStates.Divorced, at);
        tx.Commit();
        return true;
    }

    public bool Delete(ulong coupleCode, byte state, IReadOnlyList<int> questIds, IReadOnlyList<int> skillIds)
    {
        ArgumentNullException.ThrowIfNull(questIds);
        ArgumentNullException.ThrowIfNull(skillIds);
        if (state is not (CoupleStates.CoupleDeleted or CoupleStates.DivorceDeleted)) return false;
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var now = DateTimeOffset.UtcNow;
        var extra = state == CoupleStates.DivorceDeleted ? "couple_authority = TRUE, divorce_completed_date = @at" : "couple_authority = TRUE";
        var members = Members(conn, tx, coupleCode);
        if (members.Count == 0 || !SetState(conn, tx, coupleCode, state, extra, now)) return false;
        AddCount(conn, tx, coupleCode, state == CoupleStates.DivorceDeleted ? "divorce_count" : "couple_delete_count");
        using (var detach = new NpgsqlCommand("UPDATE couple_char SET couple_code = 0 WHERE couple_code = @c", conn, tx))
        {
            detach.Parameters.AddWithValue("c", (long)coupleCode);
            detach.ExecuteNonQuery();
        }
        foreach (var member in members)
        {
            foreach (var sql in new[]
            {
                "DELETE FROM quests WHERE char_uid = @u AND quest_id = ANY(@q)",
                "DELETE FROM quest_history WHERE char_uid = @u AND quest_id = ANY(@q)",
            })
            {
                using var quests = new NpgsqlCommand(sql, conn, tx);
                quests.Parameters.AddWithValue("u", (long)member.CharacterId);
                quests.Parameters.AddWithValue("q", questIds.ToArray());
                quests.ExecuteNonQuery();
            }
            using var skills = new NpgsqlCommand("DELETE FROM skills WHERE char_uid = @u AND skill_id = ANY(@s)", conn, tx);
            skills.Parameters.AddWithValue("u", (long)member.CharacterId);
            skills.Parameters.AddWithValue("s", skillIds.ToArray());
            skills.ExecuteNonQuery();
        }
        LogState(conn, tx, (long)coupleCode, state, now);
        tx.Commit();
        return true;
    }

    public bool SetMarriageOptions(ulong coupleCode, int ringItemIndex, int dressItemIndex, short titleIndex, DateTimeOffset at)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var cmd = new NpgsqlCommand("""
            UPDATE couple SET ring_item_index = @ring, dress_item_index = @dress, title_index = @title, couple_state = @s, recent_update_at = @at
            WHERE couple_code = @c AND couple_authority = FALSE
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("ring", ringItemIndex);
            cmd.Parameters.AddWithValue("dress", dressItemIndex);
            cmd.Parameters.AddWithValue("title", titleIndex);
            cmd.Parameters.AddWithValue("s", (short)CoupleStates.OptionsChosen);
            cmd.Parameters.AddWithValue("at", at.UtcDateTime);
            cmd.Parameters.AddWithValue("c", (long)coupleCode);
            if (cmd.ExecuteNonQuery() != 1) return false;
        }
        LogState(conn, tx, (long)coupleCode, CoupleStates.OptionsChosen, at);
        tx.Commit();
        return true;
    }

    public bool CompleteWedding(ulong coupleCode, DateTimeOffset at, IReadOnlyList<ulong> guests)
    {
        ArgumentNullException.ThrowIfNull(guests);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        if (!SetState(conn, tx, coupleCode, CoupleStates.Married, "begin_marriage_date = @at", at)) return false;
        AddCount(conn, tx, coupleCode, "marriage_count");
        foreach (var guest in guests.Distinct())
        {
            using (var count = new NpgsqlCommand("""
                INSERT INTO couple_char (char_uid, couple_code, wedding_in_count, created_at) VALUES (@u, 0, 1, NOW())
                ON CONFLICT (char_uid) DO UPDATE SET wedding_in_count = couple_char.wedding_in_count + 1
                """, conn, tx))
            {
                count.Parameters.AddWithValue("u", (long)guest);
                count.ExecuteNonQuery();
            }
            using var log = new NpgsqlCommand("INSERT INTO wedding_in_log (couple_code, char_uid, created_at) VALUES (@c, @u, @at)", conn, tx);
            log.Parameters.AddWithValue("c", (long)coupleCode);
            log.Parameters.AddWithValue("u", (long)guest);
            log.Parameters.AddWithValue("at", at.UtcDateTime);
            log.ExecuteNonQuery();
        }
        LogState(conn, tx, (long)coupleCode, CoupleStates.Married, at);
        tx.Commit();
        return true;
    }

    public void RecordCoupleSkill(ulong coupleCode, ulong characterId, bool female, int skillId, DateTimeOffset at)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO couple_skill_log (couple_code, char_uid, char_sex, skill_id, skill_level, skill_updated, action, created_at)
            VALUES (@c, @u, @sex, @skill, 1, @at, 0, @at)
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)coupleCode);
        cmd.Parameters.AddWithValue("u", (long)characterId);
        cmd.Parameters.AddWithValue("sex", (short)(female ? 1 : 0));
        cmd.Parameters.AddWithValue("skill", (short)skillId);
        cmd.Parameters.AddWithValue("at", at.UtcDateTime);
        cmd.ExecuteNonQuery();
    }

    public bool UpdateEventPoints(ulong coupleCode, ulong characterId, byte relation, byte love, byte friendship, long eventMask, byte state, DateTimeOffset at)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        int updated;
        using (var cmd = new NpgsqlCommand("""
            UPDATE couple SET relation_point = @r, love_point = @l, friend_point = @f, couple_state = @s, recent_update_at = @at
            WHERE couple_code = @c
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("r", (short)relation);
            cmd.Parameters.AddWithValue("l", (short)love);
            cmd.Parameters.AddWithValue("f", (short)friendship);
            cmd.Parameters.AddWithValue("s", (short)state);
            cmd.Parameters.AddWithValue("at", at);
            cmd.Parameters.AddWithValue("c", (long)coupleCode);
            updated = cmd.ExecuteNonQuery();
        }
        if (updated != 1)
        {
            tx.Rollback();
            return false;
        }
        using (var cmd = new NpgsqlCommand("UPDATE couple_join SET event_bitmask = @m WHERE couple_code = @c", conn, tx))
        {
            cmd.Parameters.AddWithValue("m", eventMask);
            cmd.Parameters.AddWithValue("c", (long)coupleCode);
            cmd.ExecuteNonQuery();
        }
        for (var bit = 0; bit < 63; bit++)
        {
            if ((eventMask & (1L << bit)) == 0) continue;
            using var cmd = new NpgsqlCommand("""
                INSERT INTO couple_event_history (couple_code, char_uid, event_code, created_at)
                SELECT @c, @u, @e, @at
                WHERE NOT EXISTS (SELECT 1 FROM couple_event_history WHERE couple_code = @c AND event_code = @e AND created_at::date = @at::date)
                """, conn, tx);
            cmd.Parameters.AddWithValue("c", (long)coupleCode);
            cmd.Parameters.AddWithValue("u", (long)characterId);
            cmd.Parameters.AddWithValue("e", (short)(bit + 1));
            cmd.Parameters.AddWithValue("at", at);
            cmd.ExecuteNonQuery();
        }
        if (state == CoupleStates.CloseEnough)
        {
            using var cmd = new NpgsqlCommand("""
                INSERT INTO couple_status_log (couple_code, couple_state, log_at)
                SELECT @c, @s, @at WHERE NOT EXISTS (SELECT 1 FROM couple_status_log WHERE couple_code = @c AND couple_state = @s)
                """, conn, tx);
            cmd.Parameters.AddWithValue("c", (long)coupleCode);
            cmd.Parameters.AddWithValue("s", (short)state);
            cmd.Parameters.AddWithValue("at", at);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return true;
    }

    private static CoupleRecord? Load(NpgsqlConnection conn, ulong coupleCode)
    {
        CoupleRecord? record;
        using (var cmd = new NpgsqlCommand($"SELECT {CoupleColumns} FROM couple WHERE couple_code = @c AND couple_authority = FALSE", conn))
        {
            cmd.Parameters.AddWithValue("c", (long)coupleCode);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            record = new CoupleRecord(
                (ulong)reader.GetInt64(0),
                (byte)reader.GetInt16(1),
                (byte)reader.GetInt16(2),
                (byte)reader.GetInt16(3),
                (byte)reader.GetInt16(4),
                (byte)reader.GetInt16(5),
                (byte)reader.GetInt16(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetInt16(9),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc)),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc)),
                Array.Empty<CoupleMember>(),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc)));
        }
        return record with { Members = Members(conn, null, coupleCode) };
    }

    private static List<CoupleMember> Members(NpgsqlConnection conn, NpgsqlTransaction? tx, ulong coupleCode)
    {
        using var cmd = new NpgsqlCommand($"""
            SELECT j.char_uid, j.char_sex, j.event_bitmask, j.divorce_state, COALESCE(c.name, ''), COALESCE({CharacterSql.Job}, 0)
            FROM couple_join j LEFT JOIN characters c ON c.character_id = j.char_uid
            WHERE j.couple_code = @c ORDER BY j.char_sex
            """, conn, tx);
        cmd.Parameters.AddWithValue("c", (long)coupleCode);
        using var reader = cmd.ExecuteReader();
        var members = new List<CoupleMember>(2);
        while (reader.Read())
        {
            members.Add(new CoupleMember(
                (ulong)reader.GetInt64(0), reader.GetBoolean(1), reader.GetInt64(2), (byte)reader.GetInt16(3),
                reader.GetString(4), Convert.ToInt32(reader.GetValue(5), System.Globalization.CultureInfo.InvariantCulture)));
        }
        return members;
    }

    private static bool SetState(NpgsqlConnection conn, NpgsqlTransaction tx, ulong coupleCode, byte state, string extra, DateTimeOffset at)
    {
        using var cmd = new NpgsqlCommand(
            $"UPDATE couple SET couple_state = @s, recent_update_at = @at, {extra} WHERE couple_code = @c AND couple_authority = FALSE", conn, tx);
        cmd.Parameters.AddWithValue("s", (short)state);
        cmd.Parameters.AddWithValue("at", at.UtcDateTime);
        cmd.Parameters.AddWithValue("c", (long)coupleCode);
        return cmd.ExecuteNonQuery() == 1;
    }

    private static void SetDivorceState(NpgsqlConnection conn, NpgsqlTransaction tx, ulong coupleCode, ulong characterId, byte divorceState)
    {
        using var cmd = new NpgsqlCommand("UPDATE couple_join SET divorce_state = @d WHERE couple_code = @c AND char_uid = @u", conn, tx);
        cmd.Parameters.AddWithValue("d", (short)divorceState);
        cmd.Parameters.AddWithValue("c", (long)coupleCode);
        cmd.Parameters.AddWithValue("u", (long)characterId);
        cmd.ExecuteNonQuery();
    }

    private static void AddCount(NpgsqlConnection conn, NpgsqlTransaction tx, ulong coupleCode, string column)
    {
        using var cmd = new NpgsqlCommand(
            $"UPDATE couple_char SET {column} = {column} + 1 WHERE char_uid IN (SELECT char_uid FROM couple_join WHERE couple_code = @c)", conn, tx);
        cmd.Parameters.AddWithValue("c", (long)coupleCode);
        cmd.ExecuteNonQuery();
    }

    private static void LogState(NpgsqlConnection conn, NpgsqlTransaction tx, long coupleCode, byte state, DateTimeOffset at)
    {
        using var cmd = new NpgsqlCommand("INSERT INTO couple_status_log (couple_code, couple_state, log_at) VALUES (@c, @s, @at)", conn, tx);
        cmd.Parameters.AddWithValue("c", coupleCode);
        cmd.Parameters.AddWithValue("s", (short)state);
        cmd.Parameters.AddWithValue("at", at.UtcDateTime);
        cmd.ExecuteNonQuery();
    }
}
