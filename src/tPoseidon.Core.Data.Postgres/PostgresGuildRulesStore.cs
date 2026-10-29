using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGuildRulesStore : IGuildRulesStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresGuildRulesStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public GuildResult<GuildFounded> Create(ulong characterId, string guildName, string guildIntro)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(guildIntro);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var character = (long)characterId;
        if (Scalar<long?>(conn, tx, "SELECT account_id FROM characters WHERE character_id = @c", ("c", character)) is not long account)
        {
            return GuildResult.Fail<GuildFounded>(101);
        }
        if (Exists(conn, tx, "SELECT 1 FROM guild_member_standby WHERE char_uid = @c AND standby_state IN (0, 1)", ("c", character)))
        {
            return GuildResult.Fail<GuildFounded>(107);
        }
        if (Exists(conn, tx, "SELECT 1 FROM guild WHERE lower(guild_name) = lower(@n)", ("n", guildName)))
        {
            return GuildResult.Fail<GuildFounded>(102);
        }
        if (Exists(conn, tx, "SELECT 1 FROM guild WHERE master_account_id = @a", ("a", account)))
        {
            return GuildResult.Fail<GuildFounded>(103);
        }
        if (ActivePenalty(conn, tx, character, onlyTypes: true) is { } penalty)
        {
            return GuildResult.Fail<GuildFounded>(104, penalty);
        }
        var code = Scalar<long>(conn, tx, """
            INSERT INTO guild (guild_name, guild_password, master_account_id, master_char_uid, guild_point, guild_members, guild_intro,
                guild_pw, guild_ranking, guild_max_members)
            VALUES (@n, '', @a, @c, @p, 1, @i, '0000', @r, 64)
            RETURNING guild_code
            """, ("n", guildName), ("a", account), ("c", character), ("p", GuildRules.NewGuildPoint), ("i", guildIntro), ("r", GuildRules.NewGuildRanking));
        var grades = new List<GuildGrade>();
        for (var i = 0; i < GuildRules.DefaultGrades.Count; i++)
        {
            var (name, point) = GuildRules.DefaultGrades[i];
            var grade = (byte)(i + 1);
            Execute(conn, tx, "INSERT INTO guild_grade (guild_code, grade_code, grade_point, grade_name) VALUES (@g, @k, @p, @n)",
                ("g", code), ("k", (short)grade), ("p", point), ("n", name));
            grades.Add(new GuildGrade { GuildCode = (ulong)code, GradeCode = grade, GradePoint = point, GradeName = name });
        }
        Execute(conn, tx, """
            INSERT INTO guild_member (guild_code, char_uid, member_nick, member_point, grade_code, member_right)
            VALUES (@g, @c, @nick, 0, @grade, 0)
            """, ("g", code), ("c", character), ("nick", GuildRules.MasterNick), ("grade", (short)GuildRules.MasterGrade));
        Execute(conn, tx, "INSERT INTO guild_member_standby (guild_code, char_uid, standby_state) VALUES (@g, @c, @s)",
            ("g", code), ("c", character), ("s", (short)GuildRules.StandbyMember));
        Execute(conn, tx, "INSERT INTO guild_ladder (guild_code, guild_ladder) VALUES (@g, 0)", ("g", code));
        SetCharacterGuild(conn, tx, character, code, GuildRules.Joined, chief: true);
        tx.Commit();
        return GuildResult.Ok(new GuildFounded((uint)code, GuildRules.MasterNick, grades));
    }

    public GuildResult<bool> Apply(ulong characterId, uint guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var character = (long)characterId;
        long guild = guildCode;
        if (Exists(conn, tx, "SELECT 1 FROM characters WHERE character_id = @c AND level < @l", ("c", character), ("l", GuildRules.MinApplyLevel)))
        {
            return GuildResult.Fail<bool>(107);
        }
        if (Exists(conn, tx, "SELECT 1 FROM guild_state WHERE guild_code = @g AND state_type = @t", ("g", guild), ("t", (short)GuildRules.ClosedState)))
        {
            return GuildResult.Fail<bool>(108);
        }
        if (Exists(conn, tx, "SELECT 1 FROM guild_member WHERE char_uid = @c", ("c", character)))
        {
            return GuildResult.Fail<bool>(101);
        }
        if (Exists(conn, tx, "SELECT 1 FROM guild_member_standby WHERE char_uid = @c AND standby_state = 0", ("c", character)))
        {
            return GuildResult.Fail<bool>(102);
        }
        if (Value(conn, tx, "SELECT guild_members, guild_max_members FROM guild WHERE guild_code = @g FOR UPDATE",
            r => (Members: (int)r.GetInt16(0), Max: (int)r.GetInt16(1)), ("g", guild)) is not { } size)
        {
            return GuildResult.Fail<bool>(104);
        }
        if (size.Members >= size.Max) return GuildResult.Fail<bool>(105);
        if (ActivePenalty(conn, tx, character, onlyTypes: false) is { } penalty)
        {
            return GuildResult.Fail<bool>(103, penalty);
        }
        Execute(conn, tx, "INSERT INTO guild_member_standby (guild_code, char_uid, standby_state) VALUES (@g, @c, 0)", ("g", guild), ("c", character));
        SetCharacterGuild(conn, tx, character, 0, GuildRules.Applying, chief: false);
        tx.Commit();
        return GuildResult.Ok(true);
    }

    public int CancelApplication(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var character = (long)characterId;
        if (Exists(conn, tx, "SELECT 1 FROM guild_member WHERE char_uid = @c", ("c", character))) return 102;
        if (Execute(conn, tx, "DELETE FROM guild_member_standby WHERE char_uid = @c AND standby_state = 0", ("c", character)) != 1) return 101;
        SetCharacterGuild(conn, tx, character, 0, GuildRules.NotInGuild, chief: false);
        tx.Commit();
        return GuildRules.Ok;
    }

    public GuildResult<GuildPermitted> Permit(ulong characterId, uint guildCode, byte permit)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var character = (long)characterId;
        long guild = guildCode;
        var approve = permit == GuildRules.Approve;
        short joinPoint = 0;
        if (approve)
        {
            if (Exists(conn, tx, "SELECT 1 FROM guild_member WHERE char_uid = @c AND guild_code <> @g", ("c", character), ("g", guild)))
            {
                return GuildResult.Fail<GuildPermitted>(101);
            }
            if (Exists(conn, tx, "SELECT 1 FROM guild_member WHERE char_uid = @c AND guild_code = @g", ("c", character), ("g", guild)))
            {
                return GuildResult.Fail<GuildPermitted>(106);
            }
            if (Value(conn, tx, "SELECT guild_members, guild_max_members, guild_join_point FROM guild WHERE guild_code = @g FOR UPDATE",
                r => (Members: (int)r.GetInt16(0), Max: (int)r.GetInt16(1), JoinPoint: r.GetInt16(2)), ("g", guild)) is not { } size)
            {
                return GuildResult.Fail<GuildPermitted>(104);
            }
            if (size.Members >= size.Max) return GuildResult.Fail<GuildPermitted>(105);
            joinPoint = size.JoinPoint;
        }
        if (Execute(conn, tx, """
            UPDATE guild_member_standby SET standby_state = @p, standby_at = NOW()
            WHERE guild_code = @g AND char_uid = @c AND standby_state = 0
            """, ("p", (short)permit), ("g", guild), ("c", character)) == 0)
        {
            return GuildResult.Fail<GuildPermitted>(102);
        }
        if (approve)
        {
            if (Execute(conn, tx, """
                INSERT INTO guild_member (guild_code, char_uid, member_nick, member_point, grade_code, member_right)
                VALUES (@g, @c, '', @p, @grade, 0)
                """, ("g", guild), ("c", character), ("p", (int)joinPoint), ("grade", (short)GuildRules.NewMemberGrade)) != 1)
            {
                return GuildResult.Fail<GuildPermitted>(103);
            }
            Execute(conn, tx, "UPDATE guild SET guild_members = guild_members + 1 WHERE guild_code = @g", ("g", guild));
            SetCharacterGuild(conn, tx, character, guild, GuildRules.Joined, chief: false);
        }
        else
        {
            SetCharacterGuild(conn, tx, character, 0, GuildRules.NotInGuild, chief: false);
        }
        var permitted = Row(conn, tx, $"SELECT c.account_id, c.name, c.level, {CharacterSql.Job} FROM characters c WHERE c.character_id = @c",
            r => new GuildPermitted((uint)r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetInt16(3)), ("c", character));
        if (permitted is null) return GuildResult.Fail<GuildPermitted>(103);
        tx.Commit();
        return GuildResult.Ok(permitted);
    }

    public int Leave(ulong characterId, uint guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var character = (long)characterId;
        long guild = guildCode;
        if (Scalar<string?>(conn, tx, "SELECT guild_name FROM guild WHERE guild_code = @g FOR UPDATE", ("g", guild)) is not { } guildName) return 101;
        if (Exists(conn, tx, "SELECT 1 FROM guild WHERE master_char_uid = @c", ("c", character))) return 102;
        if (Execute(conn, tx, "DELETE FROM guild_member WHERE guild_code = @g AND char_uid = @c", ("g", guild), ("c", character)) != 1) return 103;
        if (Execute(conn, tx, "DELETE FROM guild_member_standby WHERE guild_code = @g AND char_uid = @c AND standby_state = 1", ("g", guild), ("c", character)) != 1) return 104;
        if (Execute(conn, tx, "UPDATE guild SET guild_members = guild_members - 1 WHERE guild_code = @g", ("g", guild)) != 1) return 106;
        var release = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(GuildRules.LeavePenaltyDays), TimeSpan.Zero);
        if (!AddPenalty(conn, tx, character, guildName, GuildRules.LeavePenalty, release)) return 105;
        SetCharacterGuild(conn, tx, character, 0, GuildRules.NotInGuild, chief: false);
        tx.Commit();
        return GuildRules.Ok;
    }

    public GuildResult<uint> Kick(ulong characterId, uint guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var character = (long)characterId;
        long guild = guildCode;
        if (Exists(conn, tx, "SELECT 1 FROM guild WHERE guild_code = @g AND master_char_uid = @c", ("g", guild), ("c", character)))
        {
            return GuildResult.Fail<uint>(101);
        }
        if (Execute(conn, tx, "DELETE FROM guild_member WHERE guild_code = @g AND char_uid = @c", ("g", guild), ("c", character)) != 1)
        {
            return GuildResult.Fail<uint>(102);
        }
        if (Execute(conn, tx, "DELETE FROM guild_member_standby WHERE guild_code = @g AND char_uid = @c AND standby_state = 1", ("g", guild), ("c", character)) != 1)
        {
            return GuildResult.Fail<uint>(103);
        }
        if (Execute(conn, tx, "UPDATE guild SET guild_members = guild_members - 1 WHERE guild_code = @g", ("g", guild)) != 1)
        {
            return GuildResult.Fail<uint>(104);
        }
        SetCharacterGuild(conn, tx, character, 0, GuildRules.NotInGuild, chief: false);
        var account = Scalar<long>(conn, tx, "SELECT account_id FROM characters WHERE character_id = @c", ("c", character));
        tx.Commit();
        return GuildResult.Ok((uint)account);
    }

    public int Dissolve(ulong chiefCharacterId, uint guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var chief = (long)chiefCharacterId;
        long guild = guildCode;
        if (Value(conn, tx, "SELECT guild_name, master_char_uid FROM guild WHERE guild_code = @g FOR UPDATE",
            r => (Name: r.GetString(0), Master: r.GetInt64(1)), ("g", guild)) is not { } found)
        {
            return 101;
        }
        if (found.Master != chief) return 102;
        if (Scalar<bool>(conn, tx, "SELECT EXISTS (SELECT 1 FROM guild_depot_item WHERE guild_code = @g)", ("g", guild))) return GuildRules.DepotNotEmpty;
        Execute(conn, tx, """
            UPDATE characters SET guild_code = 0, guild_state = 0, is_guild_chief = FALSE
            WHERE character_id IN (SELECT char_uid FROM guild_member_standby WHERE guild_code = @g)
            """, ("g", guild));
        Execute(conn, tx, "DELETE FROM guild_recruit WHERE guild_code = @g", ("g", guild));
        Execute(conn, tx, "DELETE FROM guild_member_standby WHERE guild_code = @g", ("g", guild));
        Execute(conn, tx, "DELETE FROM guild_member WHERE guild_code = @g", ("g", guild));
        Execute(conn, tx, "DELETE FROM guild_grade WHERE guild_code = @g", ("g", guild));
        Execute(conn, tx, "DELETE FROM guild_notice WHERE guild_code = @g", ("g", guild));
        Execute(conn, tx, "DELETE FROM guild_ladder WHERE guild_code = @g", ("g", guild));
        Execute(conn, tx, "DELETE FROM guild_match WHERE guild_code = @g", ("g", guild));
        Execute(conn, tx, "DELETE FROM guild_log WHERE guild_code = @g", ("g", guild));
        Execute(conn, tx, "DELETE FROM guild_state WHERE guild_code = @g", ("g", guild));
        if (Execute(conn, tx, "DELETE FROM guild WHERE guild_code = @g", ("g", guild)) != 1) return 107;
        if (!AddPenalty(conn, tx, chief, found.Name, GuildRules.DissolvePenalty, DateTimeOffset.UtcNow.AddMonths(GuildRules.DissolvePenaltyMonths))) return 108;
        tx.Commit();
        return GuildRules.Ok;
    }

    public CharGuildInfo CharacterGuild(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var character = (long)characterId;
        var member = Row(conn, tx, """
            SELECT m.guild_code, m.member_nick, m.grade_code, g.emblem_code, g.guild_point, m.member_right,
                EXISTS (SELECT 1 FROM guild_notice n JOIN characters c ON c.character_id = m.char_uid
                        WHERE n.guild_code = m.guild_code AND n.notice_created_at > c.logout_time)
            FROM guild_member m JOIN guild g ON g.guild_code = m.guild_code
            WHERE m.char_uid = @c
            """, r => new CharGuildInfo(GuildRules.Joined, (uint)r.GetInt64(0), r.GetString(1), (byte)r.GetInt16(2), r.GetInt32(3),
                r.GetBoolean(6), r.GetInt32(4), r.GetInt16(5)), ("c", character));
        if (member is not null) return member;
        var applying = Scalar<long?>(conn, tx,
            "SELECT guild_code FROM guild_member_standby WHERE char_uid = @c AND standby_state = 0 ORDER BY standby_at DESC LIMIT 1", ("c", character));
        return applying is long code
            ? new CharGuildInfo(GuildRules.Applying, (uint)code, string.Empty, 0, 0, false, 0, 0)
            : new CharGuildInfo(GuildRules.NotInGuild, 0, string.Empty, 0, 0, false, 0, 0);
    }

    public GuildDetail? Detail(uint guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        long guild = guildCode;
        var head = Value(conn, tx, """
            SELECT g.guild_code, g.guild_name, g.master_account_id, g.master_char_uid, g.guild_created_at, g.guild_gelder, g.guild_point,
                g.guild_members, g.guild_intro, g.guild_notice, g.emblem_code, g.guild_join_point, g.guild_sms_point, g.guild_url, g.guild_pw,
                g.guild_point_increase, g.guild_ranking, g.guild_ranking_point, g.guild_max_members, COALESCE(c.name, ''),
                COALESCE(l.guild_ladder, 0), COALESCE(l.match_count, 0::smallint), COALESCE(l.win_count, 0::smallint), COALESCE(l.lose_count, 0::smallint),
                COALESCE(l.draw_count, 0::smallint), COALESCE(l.disc_count, 0::smallint), g.depot_tabs, g.guild_coin
            FROM guild g
            LEFT JOIN characters c ON c.character_id = g.master_char_uid
            LEFT JOIN guild_ladder l ON l.guild_code = g.guild_code
            WHERE g.guild_code = @g
            """, r => (
                Guild: new Guild
                {
                    GuildCode = (ulong)r.GetInt64(0),
                    GuildName = r.GetString(1),
                    MasterAccountId = (uint)r.GetInt64(2),
                    MasterCharacterId = (ulong)r.GetInt64(3),
                    GuildCreatedAt = r.GetFieldValue<DateTimeOffset>(4),
                    GuildGelder = r.GetInt64(5),
                    GuildPoint = r.GetInt32(6),
                    GuildMembers = r.GetInt16(7),
                    GuildIntro = r.GetString(8),
                    GuildNotice = r.GetString(9),
                    EmblemCode = r.GetInt32(10),
                    GuildJoinPoint = (byte)r.GetInt16(11),
                    GuildSmsPoint = (byte)r.GetInt16(12),
                    GuildUrl = r.GetString(13),
                    GuildPw = r.GetString(14),
                    GuildPointIncrease = r.GetInt32(15),
                    GuildRanking = r.GetInt32(16),
                    GuildRankingPoint = r.GetInt32(17),
                },
                Max: (int)r.GetInt16(18),
                MasterName: r.GetString(19),
                Ladder: new GuildLadder
                {
                    GuildCode = (ulong)r.GetInt64(0),
                    LadderScore = r.GetInt32(20),
                    MatchCount = r.GetInt16(21),
                    WinCount = r.GetInt16(22),
                    LoseCount = r.GetInt16(23),
                    DrawCount = r.GetInt16(24),
                    DiscCount = r.GetInt16(25),
                },
                DepotTabs: (int)r.GetInt16(26),
                Coins: r.GetInt32(27)), ("g", guild));
        if (head is not { } found) return null;
        var grades = Rows(conn, tx, "SELECT grade_code, grade_point, grade_name FROM guild_grade WHERE guild_code = @g ORDER BY grade_code",
            r => new GuildGrade { GuildCode = guildCode, GradeCode = (byte)r.GetInt16(0), GradePoint = r.GetInt32(1), GradeName = r.GetString(2) }, ("g", guild));
        var notices = Rows(conn, tx, """
            SELECT notice_code, char_uid, notice_content, notice_created_at FROM guild_notice
            WHERE guild_code = @g ORDER BY notice_created_at DESC LIMIT @n
            """, r => new GuildNotice
            {
                NoticeCode = r.GetInt64(0),
                GuildCode = guildCode,
                CharacterId = (ulong)r.GetInt64(1),
                NoticeContent = r.GetString(2),
                NoticeCreatedAt = r.GetFieldValue<DateTimeOffset>(3),
            }, ("g", guild), ("n", GuildRules.NoticesShown));
        return new GuildDetail(found.Guild, found.Max, found.MasterName, found.Ladder, grades, notices, found.DepotTabs, found.Coins);
    }

    public IReadOnlyList<GuildRosterEntry> Members(uint guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        return Rows(conn, tx, $"""
            SELECT m.char_uid, c.name, m.member_nick, m.grade_code, m.member_point, m.member_right, c.level, {CharacterSql.Job}, m.member_created_at,
                c.logout_time
            FROM guild_member m JOIN characters c ON c.character_id = m.char_uid
            WHERE m.guild_code = @g ORDER BY m.grade_code, m.member_created_at
            """, r => new GuildRosterEntry((ulong)r.GetInt64(0), r.GetString(1), r.GetString(2), (byte)r.GetInt16(3), r.GetInt32(4),
                r.GetInt16(5), r.GetInt32(6), r.GetInt16(7), r.GetFieldValue<DateTimeOffset>(8), r.GetFieldValue<DateTimeOffset>(9)), ("g", (long)guildCode));
    }

    public IReadOnlyList<GuildApplicant> Applicants(uint guildCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        return Rows(conn, tx, $"""
            SELECT s.char_uid, c.name, c.level, {CharacterSql.Job}, s.standby_at
            FROM guild_member_standby s JOIN characters c ON c.character_id = s.char_uid
            WHERE s.guild_code = @g AND s.standby_state = 0 ORDER BY s.standby_at
            """, r => new GuildApplicant((ulong)r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetInt16(3), r.GetFieldValue<DateTimeOffset>(4)),
            ("g", (long)guildCode));
    }

    private static GuildPenalty? ActivePenalty(NpgsqlConnection conn, NpgsqlTransaction tx, long character, bool onlyTypes)
    {
        var filter = onlyTypes ? $" AND penalty_type IN ({GuildRules.DissolvePenalty}, {GuildRules.GmPenalty})" : string.Empty;
        return Row(conn, tx, $"""
            SELECT guild_name, penalty_type, secede_at, release_at FROM guild_penalty
            WHERE char_uid = @c AND release_at > NOW(){filter} ORDER BY release_at DESC LIMIT 1
            """, r => new GuildPenalty
            {
                CharacterId = (ulong)character,
                GuildName = r.GetString(0),
                PenaltyType = (byte)r.GetInt16(1),
                SecedeAt = r.GetFieldValue<DateTimeOffset>(2),
                ReleaseAt = r.GetFieldValue<DateTimeOffset>(3),
            }, ("c", character));
    }

    private static bool AddPenalty(NpgsqlConnection conn, NpgsqlTransaction tx, long character, string guildName, byte type, DateTimeOffset release) =>
        Execute(conn, tx, "INSERT INTO guild_penalty (char_uid, guild_name, penalty_type, release_at) VALUES (@c, @n, @t, @r)",
            ("c", character), ("n", guildName), ("t", (short)type), ("r", release)) == 1;

    private static void SetCharacterGuild(NpgsqlConnection conn, NpgsqlTransaction tx, long character, long guild, byte state, bool chief) =>
        Execute(conn, tx, "UPDATE characters SET guild_code = @g, guild_state = @s, is_guild_chief = @m WHERE character_id = @c",
            ("g", guild), ("s", (short)state), ("m", chief), ("c", character));

    private static NpgsqlCommand Command(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, (string Name, object Value)[] parameters)
    {
        var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        return cmd;
    }

    private static int Execute(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = Command(conn, tx, sql, parameters);
        return cmd.ExecuteNonQuery();
    }

    private static bool Exists(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = Command(conn, tx, sql, parameters);
        return cmd.ExecuteScalar() is not null;
    }

    private static T Scalar<T>(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = Command(conn, tx, sql, parameters);
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? default! : (T)value;
    }

    private static T? Row<T>(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, Func<NpgsqlDataReader, T> read, params (string Name, object Value)[] parameters)
        where T : class
    {
        using var cmd = Command(conn, tx, sql, parameters);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? read(reader) : default;
    }

    private static T? Value<T>(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, Func<NpgsqlDataReader, T> read, params (string Name, object Value)[] parameters)
        where T : struct
    {
        using var cmd = Command(conn, tx, sql, parameters);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? read(reader) : null;
    }

    private static List<T> Rows<T>(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, Func<NpgsqlDataReader, T> read, params (string Name, object Value)[] parameters)
    {
        using var cmd = Command(conn, tx, sql, parameters);
        using var reader = cmd.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read()) rows.Add(read(reader));
        return rows;
    }
}
