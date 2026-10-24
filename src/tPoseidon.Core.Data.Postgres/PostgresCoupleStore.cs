using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresCoupleStore : ICoupleStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCoupleStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public Couple CreateCouple(ulong charAId, ulong charBId, bool charASex, bool charBSex)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        long coupleCode;
        DateTime createdAt;
        using (var cmd = new NpgsqlCommand(
            "INSERT INTO couple DEFAULT VALUES RETURNING couple_code, created_at",
            conn, tx))
        {
            using var reader = cmd.ExecuteReader();
            reader.Read();
            coupleCode = reader.GetInt64(0);
            createdAt = reader.GetDateTime(1);
        }

        foreach (var (cid, sex) in new[] { (charAId, charASex), (charBId, charBSex) })
        {
            using var joinCmd = new NpgsqlCommand(
                """
                INSERT INTO couple_join (couple_code, char_uid, char_sex, event_bitmask, divorce_state)
                VALUES (@couple_code, @char_uid, @sex, 0, 0)
                """,
                conn, tx);
            joinCmd.Parameters.AddWithValue("couple_code", coupleCode);
            joinCmd.Parameters.AddWithValue("char_uid", unchecked((long)cid));
            joinCmd.Parameters.AddWithValue("sex", sex);
            joinCmd.ExecuteNonQuery();

            using var charCmd = new NpgsqlCommand(
                """
                INSERT INTO couple_char (char_uid, couple_code, created_at)
                VALUES (@char_uid, @couple_code, NOW())
                ON CONFLICT (char_uid) DO UPDATE
                    SET couple_code = EXCLUDED.couple_code
                """,
                conn, tx);
            charCmd.Parameters.AddWithValue("char_uid", unchecked((long)cid));
            charCmd.Parameters.AddWithValue("couple_code", coupleCode);
            charCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return new Couple
        {
            CoupleCode = unchecked((ulong)coupleCode),
            CreatedAt = new DateTimeOffset(createdAt, TimeSpan.Zero),
            RecentUpdateAt = new DateTimeOffset(createdAt, TimeSpan.Zero),
        };
    }

    public Couple? FindCouple(ulong coupleCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(CoupleSelect + " WHERE couple_code = @c", conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)coupleCode));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public Couple? FindCoupleForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            CoupleSelect + " WHERE couple_code = (SELECT couple_code FROM couple_char WHERE char_uid = @c)",
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public Couple UpdateCoupleHeader(Couple couple)
    {
        ArgumentNullException.ThrowIfNull(couple);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE couple SET
                couple_authority = @auth,
                title_index      = @ti,
                ring_item_index  = @ri,
                dress_item_index = @di,
                advice_index1    = @a1,
                advice_index2    = @a2,
                couple_state     = @cs,
                relation_point   = @rp,
                love_point       = @lp,
                friend_point     = @fp,
                recent_update_at = NOW()
            WHERE couple_code = @c
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)couple.CoupleCode));
        cmd.Parameters.AddWithValue("auth", couple.CoupleAuthority);
        cmd.Parameters.AddWithValue("ti", couple.TitleIndex);
        cmd.Parameters.AddWithValue("ri", couple.RingItemIndex);
        cmd.Parameters.AddWithValue("di", couple.DressItemIndex);
        cmd.Parameters.AddWithValue("a1", (short)couple.AdviceIndex1);
        cmd.Parameters.AddWithValue("a2", (short)couple.AdviceIndex2);
        cmd.Parameters.AddWithValue("cs", (short)couple.CoupleState);
        cmd.Parameters.AddWithValue("rp", (short)couple.RelationPoint);
        cmd.Parameters.AddWithValue("lp", (short)couple.LovePoint);
        cmd.Parameters.AddWithValue("fp", (short)couple.FriendPoint);
        cmd.ExecuteNonQuery();
        return couple;
    }

    public bool DissolveCouple(ulong coupleCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM couple WHERE couple_code = @c", conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)coupleCode));
        return cmd.ExecuteNonQuery() == 1;
    }

    public CoupleChar? FindCharRecord(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT char_uid, couple_code, couple_count, couple_delete_count,
                   marriage_count, divorce_count, wedding_in_count, created_at
            FROM couple_char
            WHERE char_uid = @c
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new CoupleChar
        {
            CharacterId = unchecked((ulong)reader.GetInt64(0)),
            CoupleCode = unchecked((ulong)reader.GetInt64(1)),
            CoupleCount = reader.GetInt16(2),
            CoupleDeleteCount = reader.GetInt16(3),
            MarriageCount = reader.GetInt16(4),
            DivorceCount = reader.GetInt16(5),
            WeddingInCount = reader.GetInt16(6),
            CreatedAt = new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero),
        };
    }

    public CoupleChar IncrementCounter(ulong characterId, CoupleCharCounter counter)
    {
        var column = counter switch
        {
            CoupleCharCounter.Couple => "couple_count",
            CoupleCharCounter.CoupleDelete => "couple_delete_count",
            CoupleCharCounter.Marriage => "marriage_count",
            CoupleCharCounter.Divorce => "divorce_count",
            CoupleCharCounter.WeddingIn => "wedding_in_count",
            _ => throw new ArgumentOutOfRangeException(nameof(counter)),
        };

        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            $"""
            INSERT INTO couple_char (char_uid, couple_code, {column}, created_at)
            VALUES (@c, 0, 1, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET {column} = couple_char.{column} + 1
            RETURNING char_uid, couple_code, couple_count, couple_delete_count,
                      marriage_count, divorce_count, wedding_in_count, created_at
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new CoupleChar
        {
            CharacterId = unchecked((ulong)reader.GetInt64(0)),
            CoupleCode = unchecked((ulong)reader.GetInt64(1)),
            CoupleCount = reader.GetInt16(2),
            CoupleDeleteCount = reader.GetInt16(3),
            MarriageCount = reader.GetInt16(4),
            DivorceCount = reader.GetInt16(5),
            WeddingInCount = reader.GetInt16(6),
            CreatedAt = new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero),
        };
    }

    public IReadOnlyList<CoupleJoin> GetJoins(ulong coupleCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT couple_code, char_uid, char_sex, event_bitmask, divorce_state FROM couple_join WHERE couple_code = @c",
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)coupleCode));
        using var reader = cmd.ExecuteReader();
        var result = new List<CoupleJoin>();
        while (reader.Read())
        {
            result.Add(new CoupleJoin
            {
                CoupleCode = unchecked((ulong)reader.GetInt64(0)),
                CharacterId = unchecked((ulong)reader.GetInt64(1)),
                CharSex = reader.GetBoolean(2),
                EventBitmask = reader.GetInt64(3),
                DivorceState = (byte)reader.GetInt16(4),
            });
        }
        return result;
    }

    public bool UpdateDivorceState(ulong coupleCode, ulong characterId, byte divorceState)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE couple_join SET divorce_state = @ds WHERE couple_code = @c AND char_uid = @u",
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)coupleCode));
        cmd.Parameters.AddWithValue("u", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("ds", (short)divorceState);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool SetEventBitmask(ulong coupleCode, ulong characterId, long bitmask)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE couple_join SET event_bitmask = @b WHERE couple_code = @c AND char_uid = @u",
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)coupleCode));
        cmd.Parameters.AddWithValue("u", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("b", bitmask);
        return cmd.ExecuteNonQuery() == 1;
    }

    private const string CoupleSelect = """
        SELECT couple_code, couple_authority, title_index, ring_item_index,
               dress_item_index, advice_index1, advice_index2, couple_state,
               relation_point, love_point, friend_point,
               recent_update_at, created_at
        FROM couple
        """;

    private static Couple Read(NpgsqlDataReader reader) => new()
    {
        CoupleCode = unchecked((ulong)reader.GetInt64(0)),
        CoupleAuthority = reader.GetBoolean(1),
        TitleIndex = reader.GetInt16(2),
        RingItemIndex = reader.GetInt32(3),
        DressItemIndex = reader.GetInt32(4),
        AdviceIndex1 = (byte)reader.GetInt16(5),
        AdviceIndex2 = (byte)reader.GetInt16(6),
        CoupleState = (byte)reader.GetInt16(7),
        RelationPoint = (byte)reader.GetInt16(8),
        LovePoint = (byte)reader.GetInt16(9),
        FriendPoint = (byte)reader.GetInt16(10),
        RecentUpdateAt = new DateTimeOffset(reader.GetDateTime(11), TimeSpan.Zero),
        CreatedAt = new DateTimeOffset(reader.GetDateTime(12), TimeSpan.Zero),
    };
}
