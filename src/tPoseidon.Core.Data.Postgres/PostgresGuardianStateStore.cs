using Npgsql;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGuardianStateStore : IGuardianStateStore
{
    private const string StatColumns = "pts_ap, pts_ac, pts_dx, pts_maxmp, pts_ma, pts_md, pts_maxwt, pts_da, pts_lk, pts_maxhp, pts_dp, pts_hv";

    private const string StateColumns =
        "g.guardian_uid, g.slot_num, g.guardian_type, g.guardian_name, s.guardian_level, g.f_type, g.rare_type, g.title_index, " +
        "g.pts_p, g.pts_m, g.pts_s, g.pts_c, " +
        "s.pts_ap, s.pts_ac, s.pts_dx, s.pts_maxmp, s.pts_ma, s.pts_md, s.pts_maxwt, s.pts_da, s.pts_lk, s.pts_maxhp, s.pts_dp, s.pts_hv, " +
        "s.guardian_exp, s.guardian_gauge, g.skill_point";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresGuardianStateStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public GuardianAwakenResult Awaken(GuardianAwakening awakening)
    {
        ArgumentNullException.ThrowIfNull(awakening);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        short slotMax;
        using (var close = new NpgsqlCommand(
            "UPDATE char_awakening SET soulseed_flag = 0, awakening_flag = 1 WHERE char_uid = @c RETURNING slot_max_count", conn, tx))
        {
            close.Parameters.AddWithValue("c", (long)awakening.CharacterId);
            if (close.ExecuteScalar() is not short max) return new GuardianAwakenResult(GuardianAwakenError.NoAwakening, 0, 0);
            slotMax = max;
        }
        int owned;
        int nextSlot;
        using (var count = new NpgsqlCommand(
            "SELECT COUNT(*), COALESCE(MAX(slot_num) + 1, 0) FROM char_soulguardian WHERE char_uid = @c", conn, tx))
        {
            count.Parameters.AddWithValue("c", (long)awakening.CharacterId);
            using var reader = count.ExecuteReader();
            reader.Read();
            owned = (int)reader.GetInt64(0);
            nextSlot = reader.GetInt32(1);
        }
        var first = owned == 0;
        if (!first && slotMax <= owned) return new GuardianAwakenResult(GuardianAwakenError.SlotsFull, 0, 0);
        var slot = (byte)(first ? 0 : nextSlot);
        long uid;
        using (var insert = new NpgsqlCommand("""
            INSERT INTO char_soulguardian (char_uid, guardian_type, slot_num, equip_flag, guardian_name, f_type, pts_p, pts_m, pts_s, pts_c, skill_point, rare_type, title_index)
            VALUES (@c, @t, @slot, @eq, @n, @f, @p, @m, @s, @ch, 0, @r, @ti) RETURNING guardian_uid
            """, conn, tx))
        {
            insert.Parameters.AddWithValue("c", (long)awakening.CharacterId);
            insert.Parameters.AddWithValue("t", awakening.GuardianType);
            insert.Parameters.AddWithValue("slot", (short)slot);
            insert.Parameters.AddWithValue("eq", (short)(first ? 1 : 0));
            insert.Parameters.AddWithValue("n", awakening.Name);
            insert.Parameters.AddWithValue("f", (short)awakening.FType);
            insert.Parameters.AddWithValue("p", (short)awakening.PtsP);
            insert.Parameters.AddWithValue("m", (short)awakening.PtsM);
            insert.Parameters.AddWithValue("s", (short)awakening.PtsS);
            insert.Parameters.AddWithValue("ch", (short)awakening.PtsC);
            insert.Parameters.AddWithValue("r", (short)awakening.RareType);
            insert.Parameters.AddWithValue("ti", (short)awakening.TitleIndex);
            uid = (long)insert.ExecuteScalar()!;
        }
        using (var status = new NpgsqlCommand("INSERT INTO char_soulguardian_status (guardian_uid) VALUES (@u)", conn, tx))
        {
            status.Parameters.AddWithValue("u", uid);
            status.ExecuteNonQuery();
        }
        using (var learn = new NpgsqlCommand(
            "INSERT INTO skills (char_uid, skill_id, skill_level) VALUES (@c, @s, 1) ON CONFLICT (char_uid, skill_id) DO NOTHING", conn, tx))
        {
            learn.Parameters.AddWithValue("c", (long)awakening.CharacterId);
            learn.Parameters.AddWithValue("s", awakening.SummonSkillId);
            learn.ExecuteNonQuery();
        }
        tx.Commit();
        return new GuardianAwakenResult(GuardianAwakenError.None, (ulong)uid, slot);
    }

    public void ResetAwakening(ulong characterId, int summonSkillId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        foreach (var sql in new[]
        {
            "UPDATE char_awakening SET soulseed_flag = 0, awakening_flag = 0 WHERE char_uid = @c",
            "DELETE FROM skills WHERE char_uid = @c AND skill_id = @s",
            "DELETE FROM char_soulguardian_skill WHERE guardian_uid IN (SELECT guardian_uid FROM char_soulguardian WHERE char_uid = @c)",
            "DELETE FROM char_soulguardian_status WHERE guardian_uid IN (SELECT guardian_uid FROM char_soulguardian WHERE char_uid = @c)",
            "DELETE FROM char_soulguardian WHERE char_uid = @c",
        })
        {
            using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("c", (long)characterId);
            cmd.Parameters.AddWithValue("s", summonSkillId);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public GuardianLogin LoadLogin(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        short slotMax = 0;
        var awakened = false;
        using (var max = new NpgsqlCommand("SELECT slot_max_count, awakening_flag FROM char_awakening WHERE char_uid = @c", conn))
        {
            max.Parameters.AddWithValue("c", (long)characterId);
            using var row = max.ExecuteReader();
            if (row.Read())
            {
                slotMax = row.GetInt16(0);
                awakened = row.GetInt16(1) != 0;
            }
        }
        var list = new List<GuardianSummary>();
        ulong? equipped = null;
        using (var all = new NpgsqlCommand(
            "SELECT guardian_uid, guardian_type, slot_num, equip_flag, guardian_name, rare_type FROM char_soulguardian WHERE char_uid = @c ORDER BY slot_num", conn))
        {
            all.Parameters.AddWithValue("c", (long)characterId);
            using var reader = all.ExecuteReader();
            while (reader.Read())
            {
                var summary = new GuardianSummary(
                    (ulong)reader.GetInt64(0), reader.GetInt16(1), (byte)reader.GetInt16(2), reader.GetInt16(3) == 1, reader.GetString(4), (byte)reader.GetInt16(5));
                list.Add(summary);
                if (summary.Equipped) equipped = summary.GuardianUid;
            }
        }
        return new GuardianLogin(awakened, slotMax, list, equipped is { } uid ? Load(conn, characterId, uid) : null);
    }

    public GuardianState? Load(ulong characterId, ulong guardianUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        return Load(conn, characterId, guardianUid);
    }

    public bool Equip(ulong characterId, ulong guardianUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var release = new NpgsqlCommand("UPDATE char_soulguardian SET equip_flag = 0 WHERE char_uid = @c", conn, tx))
        {
            release.Parameters.AddWithValue("c", (long)characterId);
            if (release.ExecuteNonQuery() == 0) return false;
        }
        using (var equip = new NpgsqlCommand("UPDATE char_soulguardian SET equip_flag = 1 WHERE guardian_uid = @u AND char_uid = @c", conn, tx))
        {
            equip.Parameters.AddWithValue("u", (long)guardianUid);
            equip.Parameters.AddWithValue("c", (long)characterId);
            if (equip.ExecuteNonQuery() != 1) return false;
        }
        tx.Commit();
        return true;
    }

    public bool SaveExp(ulong guardianUid, int exp, int gauge)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("UPDATE char_soulguardian_status SET guardian_exp = @e, guardian_gauge = @g WHERE guardian_uid = @u", conn);
        cmd.Parameters.AddWithValue("e", exp);
        cmd.Parameters.AddWithValue("g", gauge);
        cmd.Parameters.AddWithValue("u", (long)guardianUid);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool SaveLevelUp(ulong guardianUid, short level, IReadOnlyList<short> statPoints, int skillPoint)
    {
        ArgumentNullException.ThrowIfNull(statPoints);
        if (statPoints.Count != GuardianStateRules.StatCount) throw new ArgumentException($"A guardian has {GuardianStateRules.StatCount} stat points.", nameof(statPoints));
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var assignments = string.Join(", ", StatColumns.Split(", ").Select((column, i) => $"{column} = @p{i}"));
        using (var status = new NpgsqlCommand(
            $"UPDATE char_soulguardian_status SET {assignments}, guardian_level = @l, guardian_exp = 0, guardian_gauge = 0 WHERE guardian_uid = @u", conn, tx))
        {
            for (var i = 0; i < statPoints.Count; i++) status.Parameters.AddWithValue("p" + i, statPoints[i]);
            status.Parameters.AddWithValue("l", level);
            status.Parameters.AddWithValue("u", (long)guardianUid);
            if (status.ExecuteNonQuery() != 1) return false;
        }
        using (var points = new NpgsqlCommand("UPDATE char_soulguardian SET skill_point = @sp WHERE guardian_uid = @u", conn, tx))
        {
            points.Parameters.AddWithValue("sp", skillPoint);
            points.Parameters.AddWithValue("u", (long)guardianUid);
            if (points.ExecuteNonQuery() != 1) return false;
        }
        using (var log = new NpgsqlCommand(
            "INSERT INTO char_soulguardian_levelup_log (guardian_uid, guardian_level, stat_points, skill_point) VALUES (@u, @l, @p, @sp)", conn, tx))
        {
            log.Parameters.AddWithValue("u", (long)guardianUid);
            log.Parameters.AddWithValue("l", level);
            log.Parameters.AddWithValue("p", statPoints.ToArray());
            log.Parameters.AddWithValue("sp", skillPoint);
            log.ExecuteNonQuery();
        }
        tx.Commit();
        return true;
    }

    public bool SaveCards(ulong guardianUid, int skillPoint, int updateCount, IReadOnlyList<GuardianSkill> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (updateCount > 0 && cards.Count != updateCount) return false;
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var clear = new NpgsqlCommand("DELETE FROM char_soulguardian_skill WHERE guardian_uid = @u AND skill_type = @t", conn, tx))
        {
            clear.Parameters.AddWithValue("u", (long)guardianUid);
            clear.Parameters.AddWithValue("t", (short)GuardianSkillType.Card);
            clear.ExecuteNonQuery();
        }
        if (updateCount > 0)
        {
            using var points = new NpgsqlCommand("UPDATE char_soulguardian SET skill_point = @sp WHERE guardian_uid = @u", conn, tx);
            points.Parameters.AddWithValue("sp", skillPoint);
            points.Parameters.AddWithValue("u", (long)guardianUid);
            if (points.ExecuteNonQuery() != 1) return false;
            foreach (var card in cards) Insert(conn, tx, guardianUid, card with { Type = GuardianSkillType.Card });
        }
        tx.Commit();
        return true;
    }

    public bool SetOrder(ulong guardianUid, int skillId, byte slot, bool remove)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        if (remove)
        {
            using var delete = new NpgsqlCommand(
                "DELETE FROM char_soulguardian_skill WHERE guardian_uid = @u AND skill_id = @s AND skill_type = @t", conn, tx);
            delete.Parameters.AddWithValue("u", (long)guardianUid);
            delete.Parameters.AddWithValue("s", skillId);
            delete.Parameters.AddWithValue("t", (short)GuardianSkillType.Order);
            if (delete.ExecuteNonQuery() == 0) return false;
            tx.Commit();
            return true;
        }
        GuardianSkill? card = null;
        using (var find = new NpgsqlCommand(
            "SELECT skill_f_type, skill_level FROM char_soulguardian_skill WHERE guardian_uid = @u AND skill_id = @s AND skill_type = @t", conn, tx))
        {
            find.Parameters.AddWithValue("u", (long)guardianUid);
            find.Parameters.AddWithValue("s", skillId);
            find.Parameters.AddWithValue("t", (short)GuardianSkillType.Card);
            using var reader = find.ExecuteReader();
            if (reader.Read()) card = new GuardianSkill(skillId, GuardianSkillType.Order, (byte)reader.GetInt16(0), reader.GetInt16(1), slot);
        }
        if (card is null || Occupied(conn, tx, guardianUid, GuardianSkillType.Order, slot)) return false;
        Insert(conn, tx, guardianUid, card);
        tx.Commit();
        return true;
    }

    public bool SetSpecial(ulong guardianUid, int skillId, byte fType, byte slot)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO char_soulguardian_skill (guardian_uid, skill_id, skill_type, skill_f_type, skill_level, skill_slot)
            VALUES (@u, @s, @t, @f, 1, @slot)
            ON CONFLICT (guardian_uid, skill_type, skill_slot) DO UPDATE SET skill_id = EXCLUDED.skill_id, skill_f_type = EXCLUDED.skill_f_type, latest_update = NOW()
            """, conn);
        cmd.Parameters.AddWithValue("u", (long)guardianUid);
        cmd.Parameters.AddWithValue("s", skillId);
        cmd.Parameters.AddWithValue("t", (short)GuardianSkillType.Special);
        cmd.Parameters.AddWithValue("f", (short)fType);
        cmd.Parameters.AddWithValue("slot", (short)slot);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool Rename(ulong guardianUid, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("UPDATE char_soulguardian SET guardian_name = @n WHERE guardian_uid = @u", conn);
        cmd.Parameters.AddWithValue("n", name);
        cmd.Parameters.AddWithValue("u", (long)guardianUid);
        return cmd.ExecuteNonQuery() == 1;
    }

    private static GuardianState? Load(NpgsqlConnection conn, ulong characterId, ulong guardianUid)
    {
        GuardianState? state;
        using (var cmd = new NpgsqlCommand(
            $"SELECT {StateColumns} FROM char_soulguardian g JOIN char_soulguardian_status s ON s.guardian_uid = g.guardian_uid WHERE g.guardian_uid = @u AND g.char_uid = @c", conn))
        {
            cmd.Parameters.AddWithValue("u", (long)guardianUid);
            cmd.Parameters.AddWithValue("c", (long)characterId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            state = new GuardianState(
                (ulong)reader.GetInt64(0), (byte)reader.GetInt16(1), reader.GetInt16(2), reader.GetString(3), reader.GetInt16(4),
                (byte)reader.GetInt16(5), (byte)reader.GetInt16(6), (byte)reader.GetInt16(7),
                (byte)reader.GetInt16(8), (byte)reader.GetInt16(9), (byte)reader.GetInt16(10), (byte)reader.GetInt16(11),
                Enumerable.Range(12, GuardianStateRules.StatCount).Select(reader.GetInt16).ToArray(),
                reader.GetInt32(24), reader.GetInt32(25), reader.GetInt32(26), []);
        }
        var skills = new List<GuardianSkill>();
        using (var cmd = new NpgsqlCommand(
            "SELECT skill_id, skill_type, skill_f_type, skill_level, skill_slot FROM char_soulguardian_skill WHERE guardian_uid = @u ORDER BY skill_type, skill_slot", conn))
        {
            cmd.Parameters.AddWithValue("u", (long)guardianUid);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                skills.Add(new GuardianSkill(reader.GetInt32(0), (GuardianSkillType)reader.GetInt16(1), (byte)reader.GetInt16(2), reader.GetInt16(3), (byte)reader.GetInt16(4)));
            }
        }
        return state with { Skills = skills };
    }

    private static bool Occupied(NpgsqlConnection conn, NpgsqlTransaction tx, ulong guardianUid, GuardianSkillType type, byte slot)
    {
        using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM char_soulguardian_skill WHERE guardian_uid = @u AND skill_type = @t AND skill_slot = @slot", conn, tx);
        cmd.Parameters.AddWithValue("u", (long)guardianUid);
        cmd.Parameters.AddWithValue("t", (short)type);
        cmd.Parameters.AddWithValue("slot", (short)slot);
        return cmd.ExecuteScalar() is not null;
    }

    private static void Insert(NpgsqlConnection conn, NpgsqlTransaction tx, ulong guardianUid, GuardianSkill skill)
    {
        using var cmd = new NpgsqlCommand("""
            INSERT INTO char_soulguardian_skill (guardian_uid, skill_id, skill_type, skill_f_type, skill_level, skill_slot)
            VALUES (@u, @s, @t, @f, @l, @slot)
            """, conn, tx);
        cmd.Parameters.AddWithValue("u", (long)guardianUid);
        cmd.Parameters.AddWithValue("s", skill.SkillId);
        cmd.Parameters.AddWithValue("t", (short)skill.Type);
        cmd.Parameters.AddWithValue("f", (short)skill.FType);
        cmd.Parameters.AddWithValue("l", skill.Level);
        cmd.Parameters.AddWithValue("slot", (short)skill.Slot);
        cmd.ExecuteNonQuery();
    }
}
