using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresCharStore : ICharStore
{
    private const string CharacterSelectColumns =
        "character_id, account_id, slot, name, species, ftype, job_tier, " +
        "hair, money, level, position_x, position_y, map_id, " +
        "starter_ear, starter_tail, " +
        "pts_p, pts_m, pts_s, pts_c, " +
        "hp, mp, exp, tm_exp_p, tm_exp_m, tm_exp_s, tm_exp_c, " +
        "wh_slots, login_time, logout_time, total_con_time, " +
        "ap_pts, ac_pts, dx_pts, ma_pts, md_pts, da_pts, lk_pts, " +
        "dp_pts, hv_pts, max_hp_pts, max_mp_pts, max_wt_pts, attr_pts, " +
        "tm_level_p, tm_level_m, tm_level_s, tm_level_c, " +
        "tm_point, tm_level, tm_exp, " +
        "type_2, job_2, job_2_changed, " +
        "type_3, job_3, job_3_changed, " +
        "change_job_queststate, " +
        "depth_count, dig_count, " +
        "hair_2, hair_3, job_state, " +
        "char_state, home, " +
        "char_authority, guild_state, is_guild_chief, gen_time, " +
        "(SELECT is_member FROM accounts WHERE accounts.account_id = characters.account_id)";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresCharStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<Character> GetCharactersForAccount(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            $"SELECT {CharacterSelectColumns} FROM characters WHERE account_id = @account_id ORDER BY slot",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        using var reader = cmd.ExecuteReader();
        var result = new List<Character>();
        while (reader.Read()) result.Add(ReadCharacter(reader, []));
        return result;
    }

    public Character? GetCharacterById(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            $"SELECT {CharacterSelectColumns} FROM characters WHERE character_id = @character_id",
            conn);
        cmd.Parameters.AddWithValue("character_id", unchecked((long)characterId));
        var bossKills = LoadBossKills(conn, characterId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadCharacter(reader, bossKills);
    }

    private static List<BossKillHistoryRecord> LoadBossKills(NpgsqlConnection conn, ulong characterId)
    {
        using var cmd = new NpgsqlCommand("SELECT mon_id, kill_count, kill_date FROM bossmon WHERE char_uid = @ch ORDER BY kill_date, mon_id", conn);
        cmd.Parameters.AddWithValue("ch", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var history = new List<BossKillHistoryRecord>();
        while (reader.Read())
        {
            history.Add(new BossKillHistoryRecord(reader.GetInt32(0), reader.GetInt32(1), new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc))));
        }
        return history;
    }

    public void AddCharacter(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO characters
                (character_id, account_id, slot, name, species, ftype, job_tier,
                 hair, money, level, position_x, position_y, map_id,
                 starter_ear, starter_tail,
                 pts_p, pts_m, pts_s, pts_c,
                 hp, mp, home)
            VALUES
                (@character_id, @account_id, @slot, @name, @species, @ftype, @job_tier,
                 @hair, @money, @level, @position_x, @position_y, @map_id,
                 @starter_ear, @starter_tail,
                 @pts_p, @pts_m, @pts_s, @pts_c,
                 @hp, @mp, @home)
            """,
            conn);
        cmd.Parameters.AddWithValue("character_id", unchecked((long)character.CharacterId));
        cmd.Parameters.AddWithValue("account_id", (long)character.OwnerAccountId);
        cmd.Parameters.AddWithValue("slot", (short)character.Slot);
        cmd.Parameters.AddWithValue("name", character.Name);
        cmd.Parameters.AddWithValue("species", (short)character.Species);
        cmd.Parameters.AddWithValue("ftype", (short)character.FType);
        cmd.Parameters.AddWithValue("job_tier", (short)character.JobTier);
        cmd.Parameters.AddWithValue("hair", (int)character.Hair);
        cmd.Parameters.AddWithValue("money", character.Money);
        cmd.Parameters.AddWithValue("level", (int)character.Level);
        cmd.Parameters.AddWithValue("position_x", (int)character.PositionX);
        cmd.Parameters.AddWithValue("position_y", (int)character.PositionY);
        cmd.Parameters.AddWithValue("map_id", (int)character.MapId);
        cmd.Parameters.AddWithValue("starter_ear", (int)character.StarterEarItemIndex);
        cmd.Parameters.AddWithValue("starter_tail", (int)character.StarterTailItemIndex);
        cmd.Parameters.AddWithValue("home", (short)character.Home);
        cmd.Parameters.AddWithValue("pts_p", (short)character.PtsP);
        cmd.Parameters.AddWithValue("pts_m", (short)character.PtsM);
        cmd.Parameters.AddWithValue("pts_s", (short)character.PtsS);
        cmd.Parameters.AddWithValue("pts_c", (short)character.PtsC);
        cmd.Parameters.AddWithValue("hp", character.CurrentHp);
        cmd.Parameters.AddWithValue("mp", character.CurrentMp);

        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException(
                $"Cannot add character {character.CharacterId}: " +
                $"violates uniqueness constraint ({ex.ConstraintName}).", ex);
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            throw new InvalidOperationException(
                $"Cannot add character {character.CharacterId}: " +
                $"owner account {character.OwnerAccountId} does not exist.", ex);
        }
    }

    public bool DeleteCharacter(ulong characterId, uint ownerAccountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM characters WHERE character_id = @character_id AND account_id = @account_id",
            conn);
        cmd.Parameters.AddWithValue("character_id", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("account_id", (long)ownerAccountId);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool SetCharacterMap(ulong characterId, ushort mapId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE characters SET map_id = @map_id WHERE character_id = @character_id",
            conn);
        cmd.Parameters.AddWithValue("map_id", (int)mapId);
        cmd.Parameters.AddWithValue("character_id", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool SetCharacterHome(ulong characterId, ushort homeMapId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE characters SET home = @home WHERE character_id = @character_id",
            conn);
        cmd.Parameters.AddWithValue("home", (short)homeMapId);
        cmd.Parameters.AddWithValue("character_id", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool SetCharacterPosition(ulong characterId, ushort mapId, ushort positionX, ushort positionY)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE characters SET map_id = @map_id, position_x = @pos_x, position_y = @pos_y WHERE character_id = @character_id",
            conn);
        cmd.Parameters.AddWithValue("map_id", (int)mapId);
        cmd.Parameters.AddWithValue("pos_x", (int)positionX);
        cmd.Parameters.AddWithValue("pos_y", (int)positionY);
        cmd.Parameters.AddWithValue("character_id", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool SaveCharacterStats(ulong characterId, CharacterStatsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE characters SET
                hp = @hp, mp = @mp,
                level = @level, exp = @exp,
                money = @money,
                tm_exp = @tm_exp, tm_level = @tm_level, tm_point = @tm_point,
                ap_pts = @ap, ac_pts = @ac, dx_pts = @dx,
                ma_pts = @ma, md_pts = @md, da_pts = @da, lk_pts = @lk,
                dp_pts = @dp, hv_pts = @hv,
                max_hp_pts = @max_hp, max_mp_pts = @max_mp, max_wt_pts = @max_wt,
                attr_pts = @attr,
                tm_level_p = @tlp, tm_level_m = @tlm, tm_level_s = @tls, tm_level_c = @tlc
              WHERE character_id = @character_id",
            conn);
        cmd.Parameters.AddWithValue("hp", snapshot.CurrentHp);
        cmd.Parameters.AddWithValue("mp", snapshot.CurrentMp);
        cmd.Parameters.AddWithValue("level", (int)snapshot.Level);
        cmd.Parameters.AddWithValue("exp", (long)snapshot.Exp);
        cmd.Parameters.AddWithValue("money", snapshot.Money);
        cmd.Parameters.AddWithValue("tm_exp", snapshot.TmExp);
        cmd.Parameters.AddWithValue("tm_level", (short)snapshot.TmLevel);
        cmd.Parameters.AddWithValue("tm_point", (short)snapshot.TmPoint);
        cmd.Parameters.AddWithValue("ap", (short)snapshot.ApPoints);
        cmd.Parameters.AddWithValue("ac", (short)snapshot.AcPoints);
        cmd.Parameters.AddWithValue("dx", (short)snapshot.DxPoints);
        cmd.Parameters.AddWithValue("ma", (short)snapshot.MaPoints);
        cmd.Parameters.AddWithValue("md", (short)snapshot.MdPoints);
        cmd.Parameters.AddWithValue("da", (short)snapshot.DaPoints);
        cmd.Parameters.AddWithValue("lk", (short)snapshot.LkPoints);
        cmd.Parameters.AddWithValue("dp", (short)snapshot.DpPoints);
        cmd.Parameters.AddWithValue("hv", (short)snapshot.HvPoints);
        cmd.Parameters.AddWithValue("max_hp", (short)snapshot.MaxHpPoints);
        cmd.Parameters.AddWithValue("max_mp", (short)snapshot.MaxMpPoints);
        cmd.Parameters.AddWithValue("max_wt", (short)snapshot.MaxWtPoints);
        cmd.Parameters.AddWithValue("attr", (short)snapshot.AttrPoints);
        cmd.Parameters.AddWithValue("tlp", (short)snapshot.TmLevelPower);
        cmd.Parameters.AddWithValue("tlm", (short)snapshot.TmLevelMagic);
        cmd.Parameters.AddWithValue("tls", (short)snapshot.TmLevelSense);
        cmd.Parameters.AddWithValue("tlc", (short)snapshot.TmLevelCharm);
        cmd.Parameters.AddWithValue("character_id", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    public uint EditCharacter(ulong characterId, CharacterEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        return edit.Kind switch
        {
            CharacterEditKind.Rename => Rename(conn, characterId, edit),
            CharacterEditKind.Hair => UpdateMany(
                conn, characterId, $"UPDATE characters SET {HairColumn(edit.HairSlot)} = @value WHERE character_id = @character_id", ("value", (int)edit.HairColor)),
            CharacterEditKind.Gender => UpdateMany(
                conn,
                characterId,
                "UPDATE characters SET species = @species, hair = 0, starter_ear = @ear, starter_tail = @tail WHERE character_id = @character_id",
                ("species", (short)edit.NewSpecies), ("ear", (int)edit.EarItemIndex), ("tail", (int)edit.TailItemIndex)),
            CharacterEditKind.GrowthWeights => UpdateMany(
                conn,
                characterId,
                "UPDATE characters SET pts_p = @p, pts_m = @m, pts_s = @s, pts_c = @c WHERE character_id = @character_id",
                ("p", (short)edit.PtsP), ("m", (short)edit.PtsM), ("s", (short)edit.PtsS), ("c", (short)edit.PtsC)),
            CharacterEditKind.WarehouseSlots => UpdateMany(
                conn, characterId, "UPDATE characters SET wh_slots = @value WHERE character_id = @character_id", ("value", (short)edit.WarehouseSlots)),
            _ => CharacterEditCodes.Failed,
        };
    }

    private static readonly string[] NameCopies =
    [
        "UPDATE ranking SET char_name = @name WHERE char_uid = @character_id",
        "UPDATE post_date SET char_name = @name WHERE char_uid = @character_id",
        "UPDATE tbl_gm_char SET char_name = @name WHERE lower(char_name) = lower(@old)",
    ];

    private static string HairColumn(byte slot) => slot switch
    {
        2 => "hair_2",
        3 => "hair_3",
        _ => "hair",
    };

    private static uint UpdateMany(NpgsqlConnection conn, ulong characterId, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        cmd.Parameters.AddWithValue("character_id", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1 ? CharacterEditCodes.Done : CharacterEditCodes.Failed;
    }

    private static uint Rename(NpgsqlConnection conn, ulong characterId, CharacterEdit edit)
    {
        using var transaction = conn.BeginTransaction();
        using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('characters.name'))", conn, transaction))
        {
            lockCmd.ExecuteNonQuery();
        }
        using (var taken = new NpgsqlCommand("SELECT 1 FROM characters WHERE lower(name) = lower(@name) LIMIT 1", conn, transaction))
        {
            taken.Parameters.AddWithValue("name", edit.NewName);
            if (taken.ExecuteScalar() is not null) return CharacterEditCodes.NameTaken;
        }
        var forbidden = new List<string>();
        using (var words = new NpgsqlCommand("SELECT word FROM forbidden_name", conn, transaction))
        using (var reader = words.ExecuteReader())
        {
            while (reader.Read()) forbidden.Add(reader.GetString(0));
        }
        if (!CharacterNameValidity.IsValid(edit.NewName, forbidden)) return CharacterEditCodes.InvalidName;
        string oldName;
        using (var update = new NpgsqlCommand(
            @"UPDATE characters AS changed SET name = @name
              FROM (SELECT name FROM characters WHERE character_id = @character_id FOR UPDATE) AS before
              WHERE changed.character_id = @character_id
              RETURNING before.name",
            conn,
            transaction))
        {
            update.Parameters.AddWithValue("name", edit.NewName);
            update.Parameters.AddWithValue("character_id", unchecked((long)characterId));
            if (update.ExecuteScalar() is not string previous) return CharacterEditCodes.Failed;
            oldName = previous;
        }
        using (var history = new NpgsqlCommand(
            "INSERT INTO char_name_history (char_uid, old_name, new_name, reason) VALUES (@character_id, @old, @new, @reason)",
            conn,
            transaction))
        {
            history.Parameters.AddWithValue("character_id", unchecked((long)characterId));
            history.Parameters.AddWithValue("old", oldName);
            history.Parameters.AddWithValue("new", edit.NewName);
            history.Parameters.AddWithValue("reason", $"by item using({edit.ItemIndex})");
            history.ExecuteNonQuery();
        }
        foreach (var copy in NameCopies)
        {
            using var rewrite = new NpgsqlCommand(copy, conn, transaction);
            rewrite.Parameters.AddWithValue("name", edit.NewName);
            rewrite.Parameters.AddWithValue("old", oldName);
            rewrite.Parameters.AddWithValue("character_id", unchecked((long)characterId));
            rewrite.ExecuteNonQuery();
        }
        transaction.Commit();
        return CharacterEditCodes.Done;
    }

    internal static Character ReadCharacter(NpgsqlDataReader reader, IReadOnlyList<BossKillHistoryRecord> bossKills) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        OwnerAccountId = unchecked((uint)reader.GetInt64(1)),
        Slot = (byte)reader.GetInt16(2),
        Name = reader.GetString(3),
        Species = (Species)reader.GetInt16(4),
        FType = (FType)reader.GetInt16(5),
        JobTier = (byte)reader.GetInt16(6),
        Hair = (ushort)reader.GetInt32(7),
        Money = reader.GetInt64(8),
        Level = (ushort)reader.GetInt32(9),
        PositionX = (ushort)reader.GetInt32(10),
        PositionY = (ushort)reader.GetInt32(11),
        MapId = (ushort)reader.GetInt32(12),
        StarterEarItemIndex = (ushort)reader.GetInt32(13),
        StarterTailItemIndex = (ushort)reader.GetInt32(14),
        PtsP = (byte)reader.GetInt16(15),
        PtsM = (byte)reader.GetInt16(16),
        PtsS = (byte)reader.GetInt16(17),
        PtsC = (byte)reader.GetInt16(18),
        CurrentHp = reader.GetInt64(19),
        CurrentMp = reader.GetInt64(20),
        Exp = unchecked((ulong)reader.GetInt64(21)),
        TmExpPower = unchecked((ulong)reader.GetInt64(22)),
        TmExpMagic = unchecked((ulong)reader.GetInt64(23)),
        TmExpSense = unchecked((ulong)reader.GetInt64(24)),
        TmExpCharm = unchecked((ulong)reader.GetInt64(25)),
        WarehouseSlots = (ushort)reader.GetInt16(26),
        LoginTime = new DateTimeOffset(reader.GetDateTime(27), TimeSpan.Zero),
        LogoutTime = new DateTimeOffset(reader.GetDateTime(28), TimeSpan.Zero),
        TotalConnectedSeconds = unchecked((uint)reader.GetInt32(29)),
        ApPoints = (ushort)reader.GetInt16(30),
        AcPoints = (ushort)reader.GetInt16(31),
        DxPoints = (ushort)reader.GetInt16(32),
        MaPoints = (ushort)reader.GetInt16(33),
        MdPoints = (ushort)reader.GetInt16(34),
        DaPoints = (ushort)reader.GetInt16(35),
        LkPoints = (ushort)reader.GetInt16(36),
        DpPoints = (ushort)reader.GetInt16(37),
        HvPoints = (ushort)reader.GetInt16(38),
        MaxHpPoints = (ushort)reader.GetInt16(39),
        MaxMpPoints = (ushort)reader.GetInt16(40),
        MaxWtPoints = (ushort)reader.GetInt16(41),
        AttrPoints = (ushort)reader.GetInt16(42),
        TmLevelPower = (ushort)reader.GetInt16(43),
        TmLevelMagic = (ushort)reader.GetInt16(44),
        TmLevelSense = (ushort)reader.GetInt16(45),
        TmLevelCharm = (ushort)reader.GetInt16(46),
        TmPoint = (ushort)reader.GetInt16(47),
        TmLevel = (ushort)reader.GetInt16(48),
        TmExp = reader.GetInt64(49),
        Type2 = (ushort)reader.GetInt16(50),
        Job2 = (byte)reader.GetInt16(51),
        Job2ChangedAt = new DateTimeOffset(reader.GetDateTime(52), TimeSpan.Zero),
        Type3 = (ushort)reader.GetInt16(53),
        Job3 = (byte)reader.GetInt16(54),
        Job3ChangedAt = new DateTimeOffset(reader.GetDateTime(55), TimeSpan.Zero),
        ChangeJobQuestState = (byte)reader.GetInt16(56),
        DepthCount = unchecked((ulong)reader.GetInt64(57)),
        DigCount = unchecked((uint)reader.GetInt32(58)),
        Hair2 = (ushort)reader.GetInt32(59),
        Hair3 = (ushort)reader.GetInt32(60),
        JobState = (byte)reader.GetInt16(61),
        CharState = (byte)reader.GetInt16(62),
        Home = (ushort)reader.GetInt16(63),
        CharAuthority = (byte)reader.GetInt16(64),
        GuildState = reader.GetInt16(65),
        IsGuildChief = reader.GetBoolean(66),
        GenTime = new DateTimeOffset(reader.GetDateTime(67), TimeSpan.Zero),
        IsMember = !reader.IsDBNull(68) && reader.GetBoolean(68),
        BossKills = bossKills,
    };
}
