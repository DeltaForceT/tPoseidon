using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGuildStore : IGuildStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresGuildStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public Guild CreateGuild(string guildName, uint masterAccountId, ulong masterCharacterId, string guildIntro = "")
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(guildIntro);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        long guildCode;
        DateTime createdAt;
        using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild
                (guild_name, master_account_id, master_char_uid, guild_intro, guild_created_at)
            VALUES
                (@guild_name, @account_id, @char_uid, @intro, NOW())
            RETURNING guild_code, guild_created_at
            """,
            conn, tx))
        {
            cmd.Parameters.AddWithValue("guild_name", guildName);
            cmd.Parameters.AddWithValue("account_id", (long)masterAccountId);
            cmd.Parameters.AddWithValue("char_uid", unchecked((long)masterCharacterId));
            cmd.Parameters.AddWithValue("intro", guildIntro);
            using var reader = cmd.ExecuteReader();
            reader.Read();
            guildCode = reader.GetInt64(0);
            createdAt = reader.GetDateTime(1);
        }

        using (var memberCmd = new NpgsqlCommand(
            """
            INSERT INTO guild_member
                (guild_code, char_uid, member_nick, member_created_at, grade_code, member_right)
            VALUES
                (@guild_code, @char_uid, '', NOW(), 0, 255)
            """,
            conn, tx))
        {
            memberCmd.Parameters.AddWithValue("guild_code", guildCode);
            memberCmd.Parameters.AddWithValue("char_uid", unchecked((long)masterCharacterId));
            memberCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return new Guild
        {
            GuildCode = unchecked((ulong)guildCode),
            GuildName = guildName,
            MasterAccountId = masterAccountId,
            MasterCharacterId = masterCharacterId,
            GuildIntro = guildIntro,
            GuildCreatedAt = new DateTimeOffset(createdAt, TimeSpan.Zero),
        };
    }

    public Guild? FindGuild(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            GuildSelectColumns + " WHERE guild_code = @guild_code",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadGuild(reader) : null;
    }

    public Guild? FindGuildByName(string guildName)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            GuildSelectColumns + " WHERE LOWER(guild_name) = LOWER(@guild_name)",
            conn);
        cmd.Parameters.AddWithValue("guild_name", guildName);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadGuild(reader) : null;
    }

    public IReadOnlyList<Guild> ListGuilds(int offset = 0, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            GuildSelectColumns + " ORDER BY guild_point DESC OFFSET @offset LIMIT @limit",
            conn);
        cmd.Parameters.AddWithValue("offset", offset);
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<Guild>();
        while (reader.Read()) result.Add(ReadGuild(reader));
        return result;
    }

    public Guild UpdateGuildHeader(Guild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE guild
            SET guild_name           = @guild_name,
                guild_password       = @guild_password,
                guild_gelder         = @gelder,
                guild_point          = @point,
                guild_members        = @members,
                guild_intro          = @intro,
                guild_notice         = @notice,
                emblem_code          = @emblem,
                guild_join_point     = @jp,
                guild_sms_point      = @sp,
                guild_url            = @url,
                guild_pw             = @pw,
                guild_point_increase = @inc,
                guild_ranking        = @ranking,
                guild_ranking_point  = @ranking_point
            WHERE guild_code = @guild_code
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guild.GuildCode));
        cmd.Parameters.AddWithValue("guild_name", guild.GuildName);
        cmd.Parameters.AddWithValue("guild_password", guild.GuildPassword);
        cmd.Parameters.AddWithValue("gelder", guild.GuildGelder);
        cmd.Parameters.AddWithValue("point", guild.GuildPoint);
        cmd.Parameters.AddWithValue("members", guild.GuildMembers);
        cmd.Parameters.AddWithValue("intro", guild.GuildIntro);
        cmd.Parameters.AddWithValue("notice", guild.GuildNotice);
        cmd.Parameters.AddWithValue("emblem", guild.EmblemCode);
        cmd.Parameters.AddWithValue("jp", (short)guild.GuildJoinPoint);
        cmd.Parameters.AddWithValue("sp", (short)guild.GuildSmsPoint);
        cmd.Parameters.AddWithValue("url", guild.GuildUrl);
        cmd.Parameters.AddWithValue("pw", guild.GuildPw);
        cmd.Parameters.AddWithValue("inc", guild.GuildPointIncrease);
        cmd.Parameters.AddWithValue("ranking", guild.GuildRanking);
        cmd.Parameters.AddWithValue("ranking_point", guild.GuildRankingPoint);
        cmd.ExecuteNonQuery();
        return guild;
    }

    public bool DisbandGuild(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM guild WHERE guild_code = @guild_code", conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        return cmd.ExecuteNonQuery() == 1;
    }

    public GuildMember AddMember(ulong guildCode, ulong characterId, string memberNick, byte gradeCode = 0)
    {
        ArgumentNullException.ThrowIfNull(memberNick);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        using (var insertCmd = new NpgsqlCommand(
            """
            INSERT INTO guild_member
                (guild_code, char_uid, member_nick, member_created_at, grade_code)
            VALUES
                (@guild_code, @char_uid, @nick, NOW(), @grade)
            ON CONFLICT (guild_code, char_uid) DO UPDATE
                SET member_nick = EXCLUDED.member_nick,
                    grade_code  = EXCLUDED.grade_code
            """,
            conn, tx))
        {
            insertCmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
            insertCmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            insertCmd.Parameters.AddWithValue("nick", memberNick);
            insertCmd.Parameters.AddWithValue("grade", (short)gradeCode);
            insertCmd.ExecuteNonQuery();
        }

        using (var incCmd = new NpgsqlCommand(
            "UPDATE guild SET guild_members = guild_members + 1 WHERE guild_code = @guild_code",
            conn, tx))
        {
            incCmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
            incCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return FindMember(guildCode, characterId)
            ?? throw new InvalidOperationException("guild_member missing after insert");
    }

    public bool RemoveMember(ulong guildCode, ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        int affected;
        using (var delCmd = new NpgsqlCommand(
            "DELETE FROM guild_member WHERE guild_code = @guild_code AND char_uid = @char_uid",
            conn, tx))
        {
            delCmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
            delCmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            affected = delCmd.ExecuteNonQuery();
        }

        if (affected == 1)
        {
            using var decCmd = new NpgsqlCommand(
                "UPDATE guild SET guild_members = GREATEST(0, guild_members - 1) WHERE guild_code = @guild_code",
                conn, tx);
            decCmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
            decCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return affected == 1;
    }

    public GuildMember? FindMember(ulong guildCode, ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            MemberSelectColumns + " WHERE guild_code = @guild_code AND char_uid = @char_uid",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadMember(reader) : null;
    }

    public IReadOnlyList<GuildMember> GetMembers(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            MemberSelectColumns + " WHERE guild_code = @guild_code ORDER BY grade_code, member_point DESC",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildMember>();
        while (reader.Read()) result.Add(ReadMember(reader));
        return result;
    }

    public GuildMember? FindMembershipForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            MemberSelectColumns + " WHERE char_uid = @char_uid LIMIT 1",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadMember(reader) : null;
    }

    public bool UpdateMemberGrade(ulong guildCode, ulong characterId, byte gradeCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE guild_member SET grade_code = @grade WHERE guild_code = @guild_code AND char_uid = @char_uid",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("grade", (short)gradeCode);
        return cmd.ExecuteNonQuery() == 1;
    }

    public int AddMemberPoints(ulong guildCode, ulong characterId, int delta)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE guild_member
            SET member_point = member_point + @delta
            WHERE guild_code = @guild_code AND char_uid = @char_uid
            RETURNING member_point
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("delta", delta);
        var result = cmd.ExecuteScalar();
        return result is int i ? i : 0;
    }

    public GuildMemberStandby AddStandby(ulong guildCode, ulong characterId, byte standbyState = 0)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_member_standby (guild_code, char_uid, standby_state, standby_at)
            VALUES (@guild_code, @char_uid, @state, NOW())
            ON CONFLICT (guild_code, char_uid) DO UPDATE
                SET standby_state = EXCLUDED.standby_state,
                    standby_at    = EXCLUDED.standby_at
            RETURNING standby_at
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("state", (short)standbyState);
        var at = (DateTime)cmd.ExecuteScalar()!;
        return new GuildMemberStandby
        {
            GuildCode = guildCode,
            CharacterId = characterId,
            StandbyState = standbyState,
            StandbyAt = new DateTimeOffset(at, TimeSpan.Zero),
        };
    }

    public IReadOnlyList<GuildMemberStandby> GetStandbyForGuild(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT guild_code, char_uid, standby_state, standby_at FROM guild_member_standby WHERE guild_code = @guild_code ORDER BY standby_at",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildMemberStandby>();
        while (reader.Read())
        {
            result.Add(new GuildMemberStandby
            {
                GuildCode = unchecked((ulong)reader.GetInt64(0)),
                CharacterId = unchecked((ulong)reader.GetInt64(1)),
                StandbyState = (byte)reader.GetInt16(2),
                StandbyAt = new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
            });
        }
        return result;
    }

    public bool RemoveStandby(ulong guildCode, ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM guild_member_standby WHERE guild_code = @guild_code AND char_uid = @char_uid",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    public GuildGrade UpsertGrade(GuildGrade grade)
    {
        ArgumentNullException.ThrowIfNull(grade);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_grade (guild_code, grade_code, grade_point, grade_name)
            VALUES (@guild_code, @grade_code, @grade_point, @grade_name)
            ON CONFLICT (guild_code, grade_code) DO UPDATE
                SET grade_point = EXCLUDED.grade_point,
                    grade_name  = EXCLUDED.grade_name
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)grade.GuildCode));
        cmd.Parameters.AddWithValue("grade_code", (short)grade.GradeCode);
        cmd.Parameters.AddWithValue("grade_point", grade.GradePoint);
        cmd.Parameters.AddWithValue("grade_name", grade.GradeName);
        cmd.ExecuteNonQuery();
        return grade;
    }

    public IReadOnlyList<GuildGrade> GetGrades(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT guild_code, grade_code, grade_point, grade_name FROM guild_grade WHERE guild_code = @guild_code ORDER BY grade_code",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildGrade>();
        while (reader.Read())
        {
            result.Add(new GuildGrade
            {
                GuildCode = unchecked((ulong)reader.GetInt64(0)),
                GradeCode = (byte)reader.GetInt16(1),
                GradePoint = reader.GetInt32(2),
                GradeName = reader.GetString(3),
            });
        }
        return result;
    }

    public bool DeleteGrade(ulong guildCode, byte gradeCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM guild_grade WHERE guild_code = @guild_code AND grade_code = @grade_code",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("grade_code", (short)gradeCode);
        return cmd.ExecuteNonQuery() == 1;
    }

    public GuildRecruit UpsertRecruit(GuildRecruit recruit)
    {
        ArgumentNullException.ThrowIfNull(recruit);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_recruit (guild_code, char_uid, recruit_title, recruit_content, recruit_updated_at)
            VALUES (@guild_code, @char_uid, @title, @content, NOW())
            ON CONFLICT (guild_code) DO UPDATE
                SET char_uid          = EXCLUDED.char_uid,
                    recruit_title     = EXCLUDED.recruit_title,
                    recruit_content   = EXCLUDED.recruit_content,
                    recruit_updated_at = NOW()
            RETURNING recruit_updated_at
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)recruit.GuildCode));
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)recruit.CharacterId));
        cmd.Parameters.AddWithValue("title", recruit.RecruitTitle);
        cmd.Parameters.AddWithValue("content", recruit.RecruitContent);
        var at = (DateTime)cmd.ExecuteScalar()!;
        return recruit with { RecruitUpdatedAt = new DateTimeOffset(at, TimeSpan.Zero) };
    }

    public GuildRecruit? GetRecruit(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT guild_code, char_uid, recruit_title, recruit_content, recruit_updated_at FROM guild_recruit WHERE guild_code = @guild_code",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new GuildRecruit
        {
            GuildCode = unchecked((ulong)reader.GetInt64(0)),
            CharacterId = unchecked((ulong)reader.GetInt64(1)),
            RecruitTitle = reader.GetString(2),
            RecruitContent = reader.GetString(3),
            RecruitUpdatedAt = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
        };
    }

    public bool DeleteRecruit(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM guild_recruit WHERE guild_code = @guild_code", conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        return cmd.ExecuteNonQuery() == 1;
    }

    public GuildNotice AddNotice(ulong guildCode, ulong authorCharId, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_notice (guild_code, char_uid, notice_content, notice_created_at)
            VALUES (@guild_code, @char_uid, @content, NOW())
            RETURNING notice_code, notice_created_at
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)authorCharId));
        cmd.Parameters.AddWithValue("content", content);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new GuildNotice
        {
            NoticeCode = reader.GetInt64(0),
            GuildCode = guildCode,
            CharacterId = authorCharId,
            NoticeContent = content,
            NoticeCreatedAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        };
    }

    public IReadOnlyList<GuildNotice> GetNotices(ulong guildCode, int limit = 20)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT notice_code, guild_code, char_uid, notice_content, notice_created_at
            FROM guild_notice
            WHERE guild_code = @guild_code
            ORDER BY notice_created_at DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildNotice>();
        while (reader.Read())
        {
            result.Add(new GuildNotice
            {
                NoticeCode = reader.GetInt64(0),
                GuildCode = unchecked((ulong)reader.GetInt64(1)),
                CharacterId = unchecked((ulong)reader.GetInt64(2)),
                NoticeContent = reader.GetString(3),
                NoticeCreatedAt = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
            });
        }
        return result;
    }

    public bool DeleteNotice(long noticeCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM guild_notice WHERE notice_code = @notice_code", conn);
        cmd.Parameters.AddWithValue("notice_code", noticeCode);
        return cmd.ExecuteNonQuery() == 1;
    }

    public GuildLogEntry AppendLog(ulong guildCode, byte logType, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_log (log_type, guild_code, log_msg, log_at)
            VALUES (@log_type, @guild_code, @log_msg, NOW())
            RETURNING log_seq, log_at
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("log_type", (short)logType);
        cmd.Parameters.AddWithValue("log_msg", message);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new GuildLogEntry
        {
            LogSeq = reader.GetInt64(0),
            LogAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
            LogType = logType,
            GuildCode = guildCode,
            LogMsg = message,
        };
    }

    public IReadOnlyList<GuildLogEntry> GetLog(ulong guildCode, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT log_seq, log_at, log_type, guild_code, log_msg
            FROM guild_log
            WHERE guild_code = @guild_code
            ORDER BY log_seq DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildLogEntry>();
        while (reader.Read())
        {
            result.Add(new GuildLogEntry
            {
                LogSeq = reader.GetInt64(0),
                LogAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
                LogType = (byte)reader.GetInt16(2),
                GuildCode = unchecked((ulong)reader.GetInt64(3)),
                LogMsg = reader.GetString(4),
            });
        }
        return result;
    }

    public GuildPenalty AddPenalty(ulong characterId, string guildName, byte penaltyType, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_penalty (char_uid, guild_name, penalty_type, secede_at, release_at)
            VALUES (@char_uid, @guild_name, @penalty_type, NOW(), NOW() + (@duration_sec * interval '1 second'))
            RETURNING secede_at, release_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("guild_name", guildName);
        cmd.Parameters.AddWithValue("penalty_type", (short)penaltyType);
        cmd.Parameters.AddWithValue("duration_sec", (long)duration.TotalSeconds);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new GuildPenalty
        {
            CharacterId = characterId,
            GuildName = guildName,
            PenaltyType = penaltyType,
            SecedeAt = new DateTimeOffset(reader.GetDateTime(0), TimeSpan.Zero),
            ReleaseAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        };
    }

    public IReadOnlyList<GuildPenalty> GetPenaltiesForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT char_uid, guild_name, penalty_type, secede_at, release_at
            FROM guild_penalty
            WHERE char_uid = @char_uid
            ORDER BY secede_at DESC
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildPenalty>();
        while (reader.Read())
        {
            result.Add(new GuildPenalty
            {
                CharacterId = unchecked((ulong)reader.GetInt64(0)),
                GuildName = reader.GetString(1),
                PenaltyType = (byte)reader.GetInt16(2),
                SecedeAt = new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
                ReleaseAt = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
            });
        }
        return result;
    }

    public GuildPointHistoryEntry AppendPointHistory(ulong guildCode, ulong? characterId, int delta, byte historyType)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        long seq;
        DateTime createdAt;
        using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_point_history (char_uid, add_guild_point, history_type, guild_code, created_at)
            VALUES (@char_uid, @delta, @history_type, @guild_code, NOW())
            RETURNING seq, created_at
            """,
            conn, tx))
        {
            cmd.Parameters.AddWithValue("char_uid", characterId.HasValue ? (object)unchecked((long)characterId.Value) : DBNull.Value);
            cmd.Parameters.AddWithValue("delta", delta);
            cmd.Parameters.AddWithValue("history_type", (short)historyType);
            cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
            using var reader = cmd.ExecuteReader();
            reader.Read();
            seq = reader.GetInt64(0);
            createdAt = reader.GetDateTime(1);
        }

        using (var updCmd = new NpgsqlCommand(
            "UPDATE guild SET guild_point = guild_point + @delta WHERE guild_code = @guild_code",
            conn, tx))
        {
            updCmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
            updCmd.Parameters.AddWithValue("delta", delta);
            updCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return new GuildPointHistoryEntry
        {
            Seq = seq,
            CharacterId = characterId,
            AddGuildPoint = delta,
            CreatedAt = new DateTimeOffset(createdAt, TimeSpan.Zero),
            HistoryType = historyType,
            GuildCode = guildCode,
        };
    }

    public IReadOnlyList<GuildPointHistoryEntry> GetPointHistory(ulong guildCode, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT seq, char_uid, add_guild_point, created_at, history_type, guild_code
            FROM guild_point_history
            WHERE guild_code = @guild_code
            ORDER BY seq DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildPointHistoryEntry>();
        while (reader.Read())
        {
            result.Add(new GuildPointHistoryEntry
            {
                Seq = reader.GetInt64(0),
                CharacterId = reader.IsDBNull(1) ? null : unchecked((ulong)reader.GetInt64(1)),
                AddGuildPoint = reader.GetInt32(2),
                CreatedAt = new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
                HistoryType = (byte)reader.GetInt16(4),
                GuildCode = unchecked((ulong)reader.GetInt64(5)),
            });
        }
        return result;
    }

    public GuildLadder UpsertLadder(GuildLadder ladder)
    {
        ArgumentNullException.ThrowIfNull(ladder);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_ladder
                (guild_code, guild_ladder, match_count, win_count, lose_count, draw_count, disc_count, ladder_increase)
            VALUES
                (@guild_code, @ladder, @match, @win, @lose, @draw, @disc, @inc)
            ON CONFLICT (guild_code) DO UPDATE
                SET guild_ladder    = EXCLUDED.guild_ladder,
                    match_count     = EXCLUDED.match_count,
                    win_count       = EXCLUDED.win_count,
                    lose_count      = EXCLUDED.lose_count,
                    draw_count      = EXCLUDED.draw_count,
                    disc_count      = EXCLUDED.disc_count,
                    ladder_increase = EXCLUDED.ladder_increase
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)ladder.GuildCode));
        cmd.Parameters.AddWithValue("ladder", ladder.LadderScore);
        cmd.Parameters.AddWithValue("match", ladder.MatchCount);
        cmd.Parameters.AddWithValue("win", ladder.WinCount);
        cmd.Parameters.AddWithValue("lose", ladder.LoseCount);
        cmd.Parameters.AddWithValue("draw", ladder.DrawCount);
        cmd.Parameters.AddWithValue("disc", ladder.DiscCount);
        cmd.Parameters.AddWithValue("inc", ladder.LadderIncrease);
        cmd.ExecuteNonQuery();
        return ladder;
    }

    public GuildLadder? GetLadder(ulong guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT guild_code, guild_ladder, match_count, win_count, lose_count, draw_count, disc_count, ladder_increase FROM guild_ladder WHERE guild_code = @guild_code",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new GuildLadder
        {
            GuildCode = unchecked((ulong)reader.GetInt64(0)),
            LadderScore = reader.GetInt32(1),
            MatchCount = reader.GetInt16(2),
            WinCount = reader.GetInt16(3),
            LoseCount = reader.GetInt16(4),
            DrawCount = reader.GetInt16(5),
            DiscCount = reader.GetInt16(6),
            LadderIncrease = reader.GetInt32(7),
        };
    }

    public GuildLadderLogEntry AppendLadderLog(ulong guildCode, string guildName, int newLadder, int increase)
        => AppendLadderLogInternal("guild_ladder_log", guildCode, guildName, newLadder, increase);

    public GuildLadderLogEntry AppendLadderIncreaseUpdate(ulong guildCode, string guildName, int newLadder, int increase)
        => AppendLadderLogInternal("guild_ladder_increase_update", guildCode, guildName, newLadder, increase);

    private GuildLadderLogEntry AppendLadderLogInternal(string table, ulong guildCode, string guildName, int newLadder, int increase)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            $"INSERT INTO {table} (guild_code, guild_name, guild_ladder, ladder_increase, log_at) " +
            "VALUES (@guild_code, @guild_name, @ladder, @inc, NOW()) RETURNING seq, log_at",
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("guild_name", guildName);
        cmd.Parameters.AddWithValue("ladder", newLadder);
        cmd.Parameters.AddWithValue("inc", increase);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new GuildLadderLogEntry
        {
            Seq = reader.GetInt64(0),
            GuildCode = guildCode,
            GuildName = guildName,
            GuildLadder = newLadder,
            LadderIncrease = increase,
            LogAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        };
    }

    public IReadOnlyList<GuildLadderLogEntry> GetLadderLog(ulong guildCode, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT seq, guild_code, guild_name, guild_ladder, ladder_increase, log_at
            FROM guild_ladder_log
            WHERE guild_code = @guild_code
            ORDER BY seq DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildLadderLogEntry>();
        while (reader.Read())
        {
            result.Add(new GuildLadderLogEntry
            {
                Seq = reader.GetInt64(0),
                GuildCode = reader.IsDBNull(1) ? null : unchecked((ulong)reader.GetInt64(1)),
                GuildName = reader.IsDBNull(2) ? null : reader.GetString(2),
                GuildLadder = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                LadderIncrease = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                LogAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
            });
        }
        return result;
    }

    public GuildMatchEntry RecordMatch(GuildMatchEntry match)
    {
        ArgumentNullException.ThrowIfNull(match);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_match
                (match_code, guild_code, join_type, join_users, kill_count, death_count,
                 get_ladder, total_ladder, guild_value, matched_at)
            VALUES
                (@match_code, @guild_code, @jt, @ju, @kc, @dc, @gl, @tl, @gv, NOW())
            RETURNING match_code, matched_at
            """,
            conn);
        cmd.Parameters.AddWithValue("match_code", match.MatchCode);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)match.GuildCode));
        cmd.Parameters.AddWithValue("jt", (short)match.JoinType);
        cmd.Parameters.AddWithValue("ju", (short)match.JoinUsers);
        cmd.Parameters.AddWithValue("kc", (short)match.KillCount);
        cmd.Parameters.AddWithValue("dc", (short)match.DeathCount);
        cmd.Parameters.AddWithValue("gl", match.GetLadder);
        cmd.Parameters.AddWithValue("tl", match.TotalLadder);
        cmd.Parameters.AddWithValue("gv", match.GuildValue);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return match with
        {
            MatchCode = reader.GetInt64(0),
            MatchedAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        };
    }

    public IReadOnlyList<GuildMatchEntry> GetMatches(ulong guildCode, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT match_code, guild_code, join_type, join_users, kill_count, death_count,
                   get_ladder, total_ladder, guild_value, matched_at
            FROM guild_match
            WHERE guild_code = @guild_code
            ORDER BY match_code DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildMatchEntry>();
        while (reader.Read())
        {
            result.Add(new GuildMatchEntry
            {
                MatchCode = reader.GetInt64(0),
                GuildCode = unchecked((ulong)reader.GetInt64(1)),
                JoinType = (byte)reader.GetInt16(2),
                JoinUsers = (byte)reader.GetInt16(3),
                KillCount = (byte)reader.GetInt16(4),
                DeathCount = (byte)reader.GetInt16(5),
                GetLadder = reader.GetInt16(6),
                TotalLadder = reader.GetInt32(7),
                GuildValue = reader.GetInt32(8),
                MatchedAt = new DateTimeOffset(reader.GetDateTime(9), TimeSpan.Zero),
            });
        }
        return result;
    }

    public GuildRankingEntry SnapshotRanking(GuildRankingEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_ranking
                (week_day, guild_ranking, guild_code, guild_name,
                 guild_point_increase, guild_ranking_point, snapshot_at)
            VALUES
                (@wd, @rank, @guild_code, @guild_name, @gpi, @grp, NOW())
            RETURNING seq, snapshot_at
            """,
            conn);
        cmd.Parameters.AddWithValue("wd", entry.WeekDay);
        cmd.Parameters.AddWithValue("rank", entry.Ranking);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)entry.GuildCode));
        cmd.Parameters.AddWithValue("guild_name", entry.GuildName);
        cmd.Parameters.AddWithValue("gpi", entry.GuildPointIncrease);
        cmd.Parameters.AddWithValue("grp", entry.GuildRankingPoint);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return entry with
        {
            Seq = reader.GetInt64(0),
            SnapshotAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        };
    }

    public IReadOnlyList<GuildRankingEntry> GetRanking(string weekDay, int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(weekDay);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT seq, week_day, guild_ranking, guild_code, guild_name,
                   guild_point_increase, guild_ranking_point, snapshot_at
            FROM guild_ranking
            WHERE week_day = @wd
            ORDER BY guild_ranking
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("wd", weekDay);
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildRankingEntry>();
        while (reader.Read())
        {
            result.Add(new GuildRankingEntry
            {
                Seq = reader.GetInt64(0),
                WeekDay = reader.GetString(1),
                Ranking = reader.GetInt32(2),
                GuildCode = unchecked((ulong)reader.GetInt64(3)),
                GuildName = reader.GetString(4),
                GuildPointIncrease = reader.GetInt32(5),
                GuildRankingPoint = reader.GetInt32(6),
                SnapshotAt = new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero),
            });
        }
        return result;
    }

    public GuildStateEntry AppendState(ulong guildCode, byte stateType, string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guild_state (guild_code, state_type, state_note, reg_at)
            VALUES (@guild_code, @st, @note, NOW())
            RETURNING seq, reg_at
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("st", (short)stateType);
        cmd.Parameters.AddWithValue("note", note);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new GuildStateEntry
        {
            Seq = reader.GetInt64(0),
            GuildCode = guildCode,
            StateType = stateType,
            StateNote = note,
            RegAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        };
    }

    public IReadOnlyList<GuildStateEntry> GetStateLog(ulong guildCode, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT seq, guild_code, state_type, state_note, reg_at
            FROM guild_state
            WHERE guild_code = @guild_code
            ORDER BY seq DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("guild_code", unchecked((long)guildCode));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<GuildStateEntry>();
        while (reader.Read())
        {
            result.Add(new GuildStateEntry
            {
                Seq = reader.GetInt64(0),
                GuildCode = unchecked((ulong)reader.GetInt64(1)),
                StateType = (byte)reader.GetInt16(2),
                StateNote = reader.GetString(3),
                RegAt = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
            });
        }
        return result;
    }

    private const string GuildSelectColumns = """
        SELECT guild_code, guild_name, guild_password, master_account_id, master_char_uid,
               guild_created_at, guild_gelder, guild_point, guild_members,
               guild_intro, guild_notice, emblem_code,
               guild_join_point, guild_sms_point, guild_url, guild_pw,
               guild_point_increase, guild_ranking, guild_ranking_point
        FROM guild
        """;

    private const string MemberSelectColumns = """
        SELECT guild_code, char_uid, member_nick, member_created_at,
               member_point, grade_code, member_right
        FROM guild_member
        """;

    private static Guild ReadGuild(NpgsqlDataReader reader) => new()
    {
        GuildCode = unchecked((ulong)reader.GetInt64(0)),
        GuildName = reader.GetString(1),
        GuildPassword = reader.GetString(2),
        MasterAccountId = unchecked((uint)reader.GetInt64(3)),
        MasterCharacterId = unchecked((ulong)reader.GetInt64(4)),
        GuildCreatedAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
        GuildGelder = reader.GetInt64(6),
        GuildPoint = reader.GetInt32(7),
        GuildMembers = reader.GetInt16(8),
        GuildIntro = reader.GetString(9),
        GuildNotice = reader.GetString(10),
        EmblemCode = reader.GetInt32(11),
        GuildJoinPoint = (byte)reader.GetInt16(12),
        GuildSmsPoint = (byte)reader.GetInt16(13),
        GuildUrl = reader.GetString(14),
        GuildPw = reader.GetString(15),
        GuildPointIncrease = reader.GetInt32(16),
        GuildRanking = reader.GetInt32(17),
        GuildRankingPoint = reader.GetInt32(18),
    };

    private static GuildMember ReadMember(NpgsqlDataReader reader) => new()
    {
        GuildCode = unchecked((ulong)reader.GetInt64(0)),
        CharacterId = unchecked((ulong)reader.GetInt64(1)),
        MemberNick = reader.GetString(2),
        MemberCreatedAt = new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
        MemberPoint = reader.GetInt32(4),
        GradeCode = (byte)reader.GetInt16(5),
        MemberRight = reader.GetInt16(6),
    };
}
