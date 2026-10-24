using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresPvpStore : IPvpStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPvpStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public PvpMatch RecordMatch(PvpMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO pvp_match
                (match_begin, match_end, match_type, match_map, match_timeout,
                 join_type, char_uid, match_char_uid, get_ladder, get_exp, match_result)
            VALUES
                (@mb, @me, @mt, @mm, @to, @jt, @c, @mc, @gl, @ge, @mr)
            RETURNING match_seq
            """,
            conn);
        cmd.Parameters.AddWithValue("mb", match.MatchBegin.UtcDateTime);
        cmd.Parameters.AddWithValue("me", match.MatchEnd.UtcDateTime);
        cmd.Parameters.AddWithValue("mt", (short)match.MatchType);
        cmd.Parameters.AddWithValue("mm", match.MatchMap);
        cmd.Parameters.AddWithValue("to", (short)match.MatchTimeout);
        cmd.Parameters.AddWithValue("jt", (short)match.JoinType);
        cmd.Parameters.AddWithValue("c", unchecked((long)match.CharacterId));
        cmd.Parameters.AddWithValue("mc", unchecked((long)match.OpponentCharacterId));
        cmd.Parameters.AddWithValue("gl", match.GetLadder);
        cmd.Parameters.AddWithValue("ge", match.GetExp);
        cmd.Parameters.AddWithValue("mr", (short)match.MatchResult);
        var seq = (long)cmd.ExecuteScalar()!;
        return match with { MatchSeq = seq };
    }

    public IReadOnlyList<PvpMatch> GetMatchesForCharacter(ulong characterId, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT match_seq, match_begin, match_end, match_type, match_map, match_timeout,
                   join_type, char_uid, match_char_uid, get_ladder, get_exp, match_result
            FROM pvp_match
            WHERE char_uid = @c
            ORDER BY match_end DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<PvpMatch>();
        while (reader.Read())
        {
            result.Add(new PvpMatch
            {
                MatchSeq = reader.GetInt64(0),
                MatchBegin = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
                MatchEnd = new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero),
                MatchType = (byte)reader.GetInt16(3),
                MatchMap = reader.GetInt16(4),
                MatchTimeout = (byte)reader.GetInt16(5),
                JoinType = (byte)reader.GetInt16(6),
                CharacterId = unchecked((ulong)reader.GetInt64(7)),
                OpponentCharacterId = unchecked((ulong)reader.GetInt64(8)),
                GetLadder = reader.GetInt32(9),
                GetExp = reader.GetInt32(10),
                MatchResult = (byte)reader.GetInt16(11),
            });
        }
        return result;
    }

    public PvpLadder GetOrCreateLadder(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO pvp_ladder (char_uid) VALUES (@c)
            ON CONFLICT (char_uid) DO UPDATE SET char_uid = EXCLUDED.char_uid
            RETURNING char_uid, pvp_ladder, match_count, win_count, lose_count, draw_count, disc_count
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return ReadLadder(reader);
    }

    public PvpLadder UpsertLadder(PvpLadder ladder)
    {
        ArgumentNullException.ThrowIfNull(ladder);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO pvp_ladder
                (char_uid, pvp_ladder, match_count, win_count, lose_count, draw_count, disc_count)
            VALUES
                (@c, @l, @m, @w, @lo, @d, @ds)
            ON CONFLICT (char_uid) DO UPDATE
                SET pvp_ladder  = EXCLUDED.pvp_ladder,
                    match_count = EXCLUDED.match_count,
                    win_count   = EXCLUDED.win_count,
                    lose_count  = EXCLUDED.lose_count,
                    draw_count  = EXCLUDED.draw_count,
                    disc_count  = EXCLUDED.disc_count
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)ladder.CharacterId));
        cmd.Parameters.AddWithValue("l", ladder.Ladder);
        cmd.Parameters.AddWithValue("m", ladder.MatchCount);
        cmd.Parameters.AddWithValue("w", ladder.WinCount);
        cmd.Parameters.AddWithValue("lo", ladder.LoseCount);
        cmd.Parameters.AddWithValue("d", ladder.DrawCount);
        cmd.Parameters.AddWithValue("ds", ladder.DiscCount);
        cmd.ExecuteNonQuery();
        return ladder;
    }

    public bool DeleteLadder(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM pvp_ladder WHERE char_uid = @c", conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    private static PvpLadder ReadLadder(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        Ladder = reader.GetInt32(1),
        MatchCount = reader.GetInt16(2),
        WinCount = reader.GetInt16(3),
        LoseCount = reader.GetInt16(4),
        DrawCount = reader.GetInt16(5),
        DiscCount = reader.GetInt16(6),
    };
}

public sealed class PostgresFriendStore : IFriendStore
{
    private const string FriendTypeSql =
        "CASE c.job_state WHEN 2 THEN c.type_2 WHEN 3 THEN c.type_3 ELSE c.species END";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresFriendStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<FriendGroup> GetGroups(ulong ownerCharacterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT group_num, group_name FROM friend_group_info WHERE owner_character_id = @o ORDER BY group_num", conn);
        cmd.Parameters.AddWithValue("o", (long)ownerCharacterId);
        using var reader = cmd.ExecuteReader();
        var groups = new List<FriendGroup>();
        while (reader.Read()) groups.Add(new FriendGroup((byte)reader.GetInt16(0), reader.GetString(1)));
        return groups;
    }

    public void CreateDefaultGroups(ulong ownerCharacterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        for (var i = 0; i < FriendRules.DefaultGroupNames.Count; i++)
        {
            using var cmd = new NpgsqlCommand(
                "INSERT INTO friend_group_info (owner_character_id, group_num, group_name) VALUES (@o, @g, @n) ON CONFLICT DO NOTHING",
                conn, tx);
            cmd.Parameters.AddWithValue("o", (long)ownerCharacterId);
            cmd.Parameters.AddWithValue("g", (short)i);
            cmd.Parameters.AddWithValue("n", FriendRules.DefaultGroupNames[i]);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public bool RenameGroup(ulong ownerCharacterId, byte groupNum, string groupName)
    {
        ArgumentNullException.ThrowIfNull(groupName);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE friend_group_info SET group_name = @n WHERE owner_character_id = @o AND group_num = @g", conn);
        cmd.Parameters.AddWithValue("o", (long)ownerCharacterId);
        cmd.Parameters.AddWithValue("g", (short)groupNum);
        cmd.Parameters.AddWithValue("n", groupName);
        return cmd.ExecuteNonQuery() == 1;
    }

    public FriendEntry? FindCharacter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT c.character_id, c.name, " + FriendTypeSql + " FROM characters c WHERE LOWER(c.name) = LOWER(@n) LIMIT 2", conn);
        cmd.Parameters.AddWithValue("n", name);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var found = new FriendEntry(unchecked((ulong)reader.GetInt64(0)), 0, reader.GetString(1), (ushort)Convert.ToInt32(reader.GetValue(2)));
        return reader.Read() ? null : found;
    }

    public IReadOnlyList<FriendEntry> GetFriends(ulong ownerCharacterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT f.friend_character_id, f.group_num, c.name, " + FriendTypeSql + " " +
            "FROM friend_info f JOIN characters c ON c.character_id = f.friend_character_id " +
            "WHERE f.owner_character_id = @o AND f.deleted = FALSE",
            conn);
        cmd.Parameters.AddWithValue("o", (long)ownerCharacterId);
        using var reader = cmd.ExecuteReader();
        var friends = new List<FriendEntry>();
        while (reader.Read())
        {
            friends.Add(new FriendEntry((ulong)reader.GetInt64(0), (byte)reader.GetInt16(1), reader.GetString(2), (ushort)reader.GetInt16(3)));
        }
        return friends;
    }

    public FriendAddResult AddFriend(ulong ownerCharacterId, ulong friendCharacterId, byte groupNum)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var exists = new NpgsqlCommand(
            "SELECT 1 FROM friend_info WHERE owner_character_id = @o AND friend_character_id = @f", conn, tx))
        {
            exists.Parameters.AddWithValue("o", (long)ownerCharacterId);
            exists.Parameters.AddWithValue("f", (long)friendCharacterId);
            if (exists.ExecuteScalar() is not null) return FriendAddResult.AlreadyFriends;
        }
        if (InsertFriendRow(conn, tx, ownerCharacterId, friendCharacterId, groupNum) != 1) return FriendAddResult.InsertFailed;
        if (InsertFriendRow(conn, tx, friendCharacterId, ownerCharacterId, groupNum) != 1) return FriendAddResult.ReverseInsertFailed;
        tx.Commit();
        return FriendAddResult.Added;
    }

    public bool SetFriendGroup(ulong ownerCharacterId, ulong friendCharacterId, byte groupNum)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE friend_info SET group_num = @g WHERE owner_character_id = @o AND friend_character_id = @f", conn);
        cmd.Parameters.AddWithValue("o", (long)ownerCharacterId);
        cmd.Parameters.AddWithValue("f", (long)friendCharacterId);
        cmd.Parameters.AddWithValue("g", (short)groupNum);
        return cmd.ExecuteNonQuery() == 1;
    }

    public FriendRemoveResult RemoveFriend(ulong ownerCharacterId, ulong friendCharacterId, bool friendOnline)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var own = new NpgsqlCommand(
            "DELETE FROM friend_info WHERE owner_character_id = @o AND friend_character_id = @f", conn, tx))
        {
            own.Parameters.AddWithValue("o", (long)ownerCharacterId);
            own.Parameters.AddWithValue("f", (long)friendCharacterId);
            if (own.ExecuteNonQuery() != 1) return FriendRemoveResult.NotFriends;
        }
        using (var reverse = new NpgsqlCommand(
            friendOnline
                ? "DELETE FROM friend_info WHERE owner_character_id = @f AND friend_character_id = @o"
                : "UPDATE friend_info SET deleted = TRUE WHERE owner_character_id = @f AND friend_character_id = @o",
            conn, tx))
        {
            reverse.Parameters.AddWithValue("o", (long)ownerCharacterId);
            reverse.Parameters.AddWithValue("f", (long)friendCharacterId);
            if (reverse.ExecuteNonQuery() != 1) return FriendRemoveResult.ReverseMissing;
        }
        tx.Commit();
        return FriendRemoveResult.Removed;
    }

    public IReadOnlyList<string> TakeDeletedNotifications(ulong ownerCharacterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var names = new List<string>();
        using (var select = new NpgsqlCommand(
            "SELECT c.name FROM friend_info f JOIN characters c ON c.character_id = f.friend_character_id " +
            "WHERE f.owner_character_id = @o AND f.deleted = TRUE",
            conn, tx))
        {
            select.Parameters.AddWithValue("o", (long)ownerCharacterId);
            using var reader = select.ExecuteReader();
            while (reader.Read()) names.Add(reader.GetString(0));
        }
        using (var purge = new NpgsqlCommand(
            "DELETE FROM friend_info WHERE owner_character_id = @o AND deleted = TRUE", conn, tx))
        {
            purge.Parameters.AddWithValue("o", (long)ownerCharacterId);
            purge.ExecuteNonQuery();
        }
        tx.Commit();
        return names;
    }

    public int CountMessages(ulong ownerCharacterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM friend_sms WHERE owner_character_id = @o", conn);
        cmd.Parameters.AddWithValue("o", (long)ownerCharacterId);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public bool SendMessage(ulong ownerCharacterId, ulong senderCharacterId, byte kind, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "INSERT INTO friend_sms (owner_character_id, sender_character_id, content, sms_kind) " +
            "SELECT @o, @s, @c, @k WHERE (SELECT COUNT(*) FROM friend_sms WHERE owner_character_id = @o) < @most",
            conn);
        cmd.Parameters.AddWithValue("o", (long)ownerCharacterId);
        cmd.Parameters.AddWithValue("s", (long)senderCharacterId);
        cmd.Parameters.AddWithValue("c", content);
        cmd.Parameters.AddWithValue("k", (short)kind);
        cmd.Parameters.AddWithValue("most", (long)FriendRules.MostStoredMessages);
        return cmd.ExecuteNonQuery() == 1;
    }

    public IReadOnlyList<FriendMessage> TakeMessages(ulong ownerCharacterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var messages = new List<FriendMessage>();
        using (var select = new NpgsqlCommand(
            "SELECT m.sender_character_id, COALESCE(c.name, ''), m.sent_at, m.content, m.sms_kind " +
            "FROM friend_sms m LEFT JOIN characters c ON c.character_id = m.sender_character_id " +
            "WHERE m.owner_character_id = @o ORDER BY m.sms_kind DESC, m.sms_seq",
            conn, tx))
        {
            select.Parameters.AddWithValue("o", (long)ownerCharacterId);
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                var sender = (ulong)reader.GetInt64(0);
                messages.Add(new FriendMessage(
                    sender,
                    sender == FriendRules.GmSenderCharacterId ? FriendRules.GmSenderName : reader.GetString(1),
                    reader.GetFieldValue<DateTimeOffset>(2),
                    reader.GetString(3),
                    (byte)reader.GetInt16(4)));
            }
        }
        using (var purge = new NpgsqlCommand("DELETE FROM friend_sms WHERE owner_character_id = @o", conn, tx))
        {
            purge.Parameters.AddWithValue("o", (long)ownerCharacterId);
            purge.ExecuteNonQuery();
        }
        tx.Commit();
        return messages;
    }

    public void RemoveAllFor(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        foreach (var sql in RemoveAllStatements)
        {
            using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("c", (long)characterId);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static readonly string[] RemoveAllStatements =
    {
        "DELETE FROM friend_info WHERE owner_character_id = @c OR friend_character_id = @c",
        "DELETE FROM friend_sms WHERE owner_character_id = @c",
    };

    private static int InsertFriendRow(NpgsqlConnection conn, NpgsqlTransaction tx, ulong owner, ulong friend, byte groupNum)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO friend_info (owner_character_id, friend_character_id, group_num) VALUES (@o, @f, @g)", conn, tx);
        cmd.Parameters.AddWithValue("o", (long)owner);
        cmd.Parameters.AddWithValue("f", (long)friend);
        cmd.Parameters.AddWithValue("g", (short)groupNum);
        return cmd.ExecuteNonQuery();
    }
}

public sealed class PostgresAccountBlockStore : IAccountBlockStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAccountBlockStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public AccountBlock Add(AccountBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO account_block
                (target_account_id, block_type, block_reason, gm_account_id,
                 block_from, block_to, description, created_at)
            VALUES
                (@target, @type, @reason, @gm, @from, @to, @desc, NOW())
            RETURNING block_seq, created_at
            """,
            conn);
        cmd.Parameters.AddWithValue("target", (long)block.TargetAccountId);
        cmd.Parameters.AddWithValue("type", (short)block.BlockType);
        cmd.Parameters.AddWithValue("reason", (short)block.BlockReason);
        cmd.Parameters.AddWithValue("gm", block.GmAccountId.HasValue ? (object)(long)block.GmAccountId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("from", block.BlockFrom.UtcDateTime);
        cmd.Parameters.AddWithValue("to", block.BlockTo.UtcDateTime);
        cmd.Parameters.AddWithValue("desc", block.Description);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return block with
        {
            BlockSeq = reader.GetInt64(0),
            CreatedAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        };
    }

    public IReadOnlyList<AccountBlock> GetActiveBlocks(uint targetAccountId, DateTimeOffset now)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BlockSelect + " WHERE target_account_id = @t AND block_to > @now ORDER BY block_seq DESC",
            conn);
        cmd.Parameters.AddWithValue("t", (long)targetAccountId);
        cmd.Parameters.AddWithValue("now", now.UtcDateTime);
        return ReadBlocks(cmd);
    }

    public IReadOnlyList<AccountBlock> GetAllBlocks(uint targetAccountId, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            BlockSelect + " WHERE target_account_id = @t ORDER BY block_seq DESC LIMIT @limit",
            conn);
        cmd.Parameters.AddWithValue("t", (long)targetAccountId);
        cmd.Parameters.AddWithValue("limit", limit);
        return ReadBlocks(cmd);
    }

    public bool RemoveBlock(long blockSeq)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM account_block WHERE block_seq = @s", conn);
        cmd.Parameters.AddWithValue("s", blockSeq);
        return cmd.ExecuteNonQuery() == 1;
    }

    private const string BlockSelect = """
        SELECT block_seq, target_account_id, block_type, block_reason, gm_account_id,
               block_from, block_to, description, created_at
        FROM account_block
        """;

    private static List<AccountBlock> ReadBlocks(NpgsqlCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var result = new List<AccountBlock>();
        while (reader.Read())
        {
            result.Add(new AccountBlock
            {
                BlockSeq = reader.GetInt64(0),
                TargetAccountId = unchecked((uint)reader.GetInt64(1)),
                BlockType = (byte)reader.GetInt16(2),
                BlockReason = (byte)reader.GetInt16(3),
                GmAccountId = reader.IsDBNull(4) ? null : unchecked((uint)reader.GetInt64(4)),
                BlockFrom = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
                BlockTo = new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero),
                Description = reader.GetString(7),
                CreatedAt = new DateTimeOffset(reader.GetDateTime(8), TimeSpan.Zero),
            });
        }
        return result;
    }
}

public sealed class PostgresAccountLogoutStore : IAccountLogoutStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAccountLogoutStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public AccountLogoutEntry Append(uint accountId, DateTimeOffset loginAt, DateTimeOffset logoutAt, byte logoutWorld, int? playTime)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO account_logout (account_id, login_at, logout_at, logout_world, play_time)
            VALUES (@a, @lin, @lout, @w, @pt)
            RETURNING log_seq
            """,
            conn);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        cmd.Parameters.AddWithValue("lin", loginAt.UtcDateTime);
        cmd.Parameters.AddWithValue("lout", logoutAt.UtcDateTime);
        cmd.Parameters.AddWithValue("w", (short)logoutWorld);
        cmd.Parameters.AddWithValue("pt", playTime.HasValue ? (object)playTime.Value : DBNull.Value);
        var seq = (long)cmd.ExecuteScalar()!;
        return new AccountLogoutEntry
        {
            LogSeq = seq,
            AccountId = accountId,
            LoginAt = loginAt,
            LogoutAt = logoutAt,
            LogoutWorld = logoutWorld,
            PlayTime = playTime,
        };
    }

    public IReadOnlyList<AccountLogoutEntry> GetForAccount(uint accountId, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT log_seq, account_id, login_at, logout_at, logout_world, play_time
            FROM account_logout
            WHERE account_id = @a
            ORDER BY log_seq DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<AccountLogoutEntry>();
        while (reader.Read())
        {
            result.Add(new AccountLogoutEntry
            {
                LogSeq = reader.GetInt64(0),
                AccountId = unchecked((uint)reader.GetInt64(1)),
                LoginAt = new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero),
                LogoutAt = new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
                LogoutWorld = (byte)reader.GetInt16(4),
                PlayTime = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            });
        }
        return result;
    }
}

public sealed class PostgresFortuneStore : IFortuneStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresFortuneStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public FortuneInfo GetOrCreate(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO fortune_info (char_uid, expires_at)
            VALUES (@c, NOW() + interval '1 day')
            ON CONFLICT (char_uid) DO UPDATE SET char_uid = EXCLUDED.char_uid
            RETURNING char_uid, view_count, title_id, item_attr_string, updated_at, expires_at
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public FortuneInfo? Find(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT char_uid, view_count, title_id, item_attr_string, updated_at, expires_at FROM fortune_info WHERE char_uid = @c",
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public void Update(FortuneInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO fortune_info
                (char_uid, view_count, title_id, item_attr_string, updated_at, expires_at)
            VALUES
                (@c, @vc, @ti, @ias, @upd, @exp)
            ON CONFLICT (char_uid) DO UPDATE
                SET view_count       = EXCLUDED.view_count,
                    title_id         = EXCLUDED.title_id,
                    item_attr_string = EXCLUDED.item_attr_string,
                    updated_at       = EXCLUDED.updated_at,
                    expires_at       = EXCLUDED.expires_at
            """,
            conn, tx))
        {
            cmd.Parameters.AddWithValue("c", unchecked((long)info.CharacterId));
            cmd.Parameters.AddWithValue("vc", info.ViewCount);
            cmd.Parameters.AddWithValue("ti", info.TitleId);
            cmd.Parameters.AddWithValue("ias", info.ItemAttrString);
            cmd.Parameters.AddWithValue("upd", info.UpdatedAt.UtcDateTime);
            cmd.Parameters.AddWithValue("exp", info.ExpiresAt.UtcDateTime);
            cmd.ExecuteNonQuery();
        }
        using (var log = new NpgsqlCommand(
            "INSERT INTO fortune_info_log (char_uid, view_count, title_id, item_attr_string) VALUES (@c, @vc, @ti, @ias)",
            conn, tx))
        {
            log.Parameters.AddWithValue("c", unchecked((long)info.CharacterId));
            log.Parameters.AddWithValue("vc", info.ViewCount);
            log.Parameters.AddWithValue("ti", info.TitleId);
            log.Parameters.AddWithValue("ias", info.ItemAttrString);
            log.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public FortuneInfo Upsert(FortuneInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO fortune_info
                (char_uid, view_count, title_id, item_attr_string, updated_at, expires_at)
            VALUES
                (@c, @vc, @ti, @ias, NOW(), @exp)
            ON CONFLICT (char_uid) DO UPDATE
                SET view_count       = EXCLUDED.view_count,
                    title_id         = EXCLUDED.title_id,
                    item_attr_string = EXCLUDED.item_attr_string,
                    updated_at       = NOW(),
                    expires_at       = EXCLUDED.expires_at
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)info.CharacterId));
        cmd.Parameters.AddWithValue("vc", info.ViewCount);
        cmd.Parameters.AddWithValue("ti", info.TitleId);
        cmd.Parameters.AddWithValue("ias", info.ItemAttrString);
        cmd.Parameters.AddWithValue("exp", info.ExpiresAt.UtcDateTime);
        cmd.ExecuteNonQuery();
        return info;
    }

    public void AppendLog(FortuneInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO fortune_info_log (char_uid, view_count, title_id, item_attr_string)
            VALUES (@c, @vc, @ti, @ias)
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)info.CharacterId));
        cmd.Parameters.AddWithValue("vc", info.ViewCount);
        cmd.Parameters.AddWithValue("ti", info.TitleId);
        cmd.Parameters.AddWithValue("ias", info.ItemAttrString);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<FortuneInfoLogEntry> GetLog(ulong characterId, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT log_seq, char_uid, view_count, title_id, item_attr_string, logged_at
            FROM fortune_info_log
            WHERE char_uid = @c
            ORDER BY log_seq DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<FortuneInfoLogEntry>();
        while (reader.Read())
        {
            result.Add(new FortuneInfoLogEntry
            {
                LogSeq = reader.GetInt64(0),
                CharacterId = unchecked((ulong)reader.GetInt64(1)),
                ViewCount = reader.GetInt32(2),
                TitleId = reader.GetInt32(3),
                ItemAttrString = reader.GetString(4),
                LoggedAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
            });
        }
        return result;
    }

    public bool Delete(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM fortune_info WHERE char_uid = @c", conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    private static FortuneInfo Read(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        ViewCount = reader.GetInt32(1),
        TitleId = reader.GetInt32(2),
        ItemAttrString = reader.GetString(3),
        UpdatedAt = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
        ExpiresAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
    };
}

public sealed class PostgresBroadcastStore : IBroadcastStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBroadcastStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public BroadcastMessage AddMessage(BroadcastMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO broadcast
                (char_uid, island_code, message, message_flag, broadcast_type, color_type)
            VALUES
                (@c, @ic, @msg, @mf, @bt, @ct)
            RETURNING broadcast_code, created_at
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)message.CharacterId));
        cmd.Parameters.AddWithValue("ic", (short)message.IslandCode);
        cmd.Parameters.AddWithValue("msg", message.Message);
        cmd.Parameters.AddWithValue("mf", (short)message.MessageFlag);
        cmd.Parameters.AddWithValue("bt", (short)message.BroadcastType);
        cmd.Parameters.AddWithValue("ct", (short)message.ColorType);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return message with
        {
            BroadcastCode = reader.GetInt64(0),
            CreatedAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        };
    }

    public IReadOnlyList<BroadcastMessage> GetActiveForIsland(byte islandCode, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT broadcast_code, char_uid, island_code, message, message_flag,
                   broadcast_type, color_type, created_at
            FROM broadcast
            WHERE island_code = @ic
            ORDER BY broadcast_code DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("ic", (short)islandCode);
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<BroadcastMessage>();
        while (reader.Read())
        {
            result.Add(new BroadcastMessage
            {
                BroadcastCode = reader.GetInt64(0),
                CharacterId = unchecked((ulong)reader.GetInt64(1)),
                IslandCode = (byte)reader.GetInt16(2),
                Message = reader.GetString(3),
                MessageFlag = (byte)reader.GetInt16(4),
                BroadcastType = (byte)reader.GetInt16(5),
                ColorType = (byte)reader.GetInt16(6),
                CreatedAt = new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero),
            });
        }
        return result;
    }

    public bool DeleteMessage(long broadcastCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM broadcast WHERE broadcast_code = @b", conn);
        cmd.Parameters.AddWithValue("b", broadcastCode);
        return cmd.ExecuteNonQuery() == 1;
    }

    public void AppendLog(BroadcastMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO broadcast_log
                (char_uid, island_code, message, broadcast_type, color_type)
            VALUES
                (@c, @ic, @msg, @bt, @ct)
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)message.CharacterId));
        cmd.Parameters.AddWithValue("ic", (short)message.IslandCode);
        cmd.Parameters.AddWithValue("msg", message.Message);
        cmd.Parameters.AddWithValue("bt", (short)message.BroadcastType);
        cmd.Parameters.AddWithValue("ct", (short)message.ColorType);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<BroadcastLogEntry> GetLog(ulong characterId, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT log_seq, char_uid, island_code, message, broadcast_type, color_type, logged_at
            FROM broadcast_log
            WHERE char_uid = @c
            ORDER BY log_seq DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<BroadcastLogEntry>();
        while (reader.Read())
        {
            result.Add(new BroadcastLogEntry
            {
                LogSeq = reader.GetInt64(0),
                CharacterId = unchecked((ulong)reader.GetInt64(1)),
                IslandCode = (byte)reader.GetInt16(2),
                Message = reader.GetString(3),
                BroadcastType = (byte)reader.GetInt16(4),
                ColorType = (byte)reader.GetInt16(5),
                LoggedAt = new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero),
            });
        }
        return result;
    }
}
