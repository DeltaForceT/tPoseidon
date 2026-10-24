using tPoseidon.Core.Data;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresAwakeningStore : IAwakeningStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAwakeningStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public CharAwakening GetOrCreate(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO char_awakening (char_uid)
            VALUES (@c)
            ON CONFLICT (char_uid) DO UPDATE SET char_uid = EXCLUDED.char_uid
            RETURNING char_uid, soulseed_flag, awakening_flag, slot_max_count, created_at
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public CharAwakening Upsert(CharAwakening awakening)
    {
        ArgumentNullException.ThrowIfNull(awakening);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO char_awakening
                (char_uid, soulseed_flag, awakening_flag, slot_max_count, created_at)
            VALUES
                (@c, @sf, @af, @smc, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET soulseed_flag  = EXCLUDED.soulseed_flag,
                    awakening_flag = EXCLUDED.awakening_flag,
                    slot_max_count = EXCLUDED.slot_max_count
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)awakening.CharacterId));
        cmd.Parameters.AddWithValue("sf", (short)awakening.SoulSeedFlag);
        cmd.Parameters.AddWithValue("af", (short)awakening.AwakeningFlag);
        cmd.Parameters.AddWithValue("smc", awakening.SlotMaxCount);
        cmd.ExecuteNonQuery();
        return awakening;
    }

    public bool MarkLevelUp(ulong characterId, int charLevel)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO char_awakening_levelup_check (char_uid, char_level, levelup_at)
            VALUES (@c, @lvl, NOW())
            ON CONFLICT (char_uid, char_level) DO NOTHING
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("lvl", charLevel);
        return cmd.ExecuteNonQuery() == 1;
    }

    public IReadOnlyList<CharAwakeningLevelUp> GetLevelUpLog(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT char_uid, char_level, levelup_at FROM char_awakening_levelup_check WHERE char_uid = @c ORDER BY char_level",
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var result = new List<CharAwakeningLevelUp>();
        while (reader.Read())
        {
            result.Add(new CharAwakeningLevelUp
            {
                CharacterId = unchecked((ulong)reader.GetInt64(0)),
                CharLevel = reader.GetInt32(1),
                LevelUpAt = new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero),
            });
        }
        return result;
    }

    public bool Delete(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM char_awakening WHERE char_uid = @c", conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    private static CharAwakening Read(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        SoulSeedFlag = (byte)reader.GetInt16(1),
        AwakeningFlag = (byte)reader.GetInt16(2),
        SlotMaxCount = reader.GetInt16(3),
        CreatedAt = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
    };
}

public sealed class PostgresSoulGuardianStore : ISoulGuardianStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSoulGuardianStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public CharSoulGuardian CreateGuardian(ulong characterId, short guardianType, byte slotNum, string name, byte fType)
    {
        ArgumentNullException.ThrowIfNull(name);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO char_soulguardian
                (char_uid, guardian_type, slot_num, guardian_name, f_type, created_at)
            VALUES
                (@c, @gt, @sn, @name, @ft, NOW())
            RETURNING guardian_uid, char_uid, guardian_type, slot_num, equip_flag,
                      guardian_name, f_type, pts_p, pts_m, pts_s, pts_c,
                      skill_point, created_at
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("gt", guardianType);
        cmd.Parameters.AddWithValue("sn", (short)slotNum);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("ft", (short)fType);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public CharSoulGuardian? FindGuardian(ulong guardianUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(GuardianSelect + " WHERE guardian_uid = @g", conn);
        cmd.Parameters.AddWithValue("g", unchecked((long)guardianUid));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<CharSoulGuardian> GetGuardiansForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            GuardianSelect + " WHERE char_uid = @c ORDER BY guardian_uid",
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var result = new List<CharSoulGuardian>();
        while (reader.Read()) result.Add(Read(reader));
        return result;
    }

    public CharSoulGuardian UpdateGuardian(CharSoulGuardian guardian)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE char_soulguardian SET
                guardian_type = @gt,
                slot_num      = @sn,
                equip_flag    = @ef,
                guardian_name = @name,
                f_type        = @ft,
                pts_p         = @pp,
                pts_m         = @pm,
                pts_s         = @ps,
                pts_c         = @pc,
                skill_point   = @sp
            WHERE guardian_uid = @g
            """,
            conn);
        cmd.Parameters.AddWithValue("g", unchecked((long)guardian.GuardianUid));
        cmd.Parameters.AddWithValue("gt", guardian.GuardianType);
        cmd.Parameters.AddWithValue("sn", (short)guardian.SlotNum);
        cmd.Parameters.AddWithValue("ef", (short)guardian.EquipFlag);
        cmd.Parameters.AddWithValue("name", guardian.GuardianName);
        cmd.Parameters.AddWithValue("ft", (short)guardian.FType);
        cmd.Parameters.AddWithValue("pp", (short)guardian.PtsP);
        cmd.Parameters.AddWithValue("pm", (short)guardian.PtsM);
        cmd.Parameters.AddWithValue("ps", (short)guardian.PtsS);
        cmd.Parameters.AddWithValue("pc", (short)guardian.PtsC);
        cmd.Parameters.AddWithValue("sp", guardian.SkillPoint);
        cmd.ExecuteNonQuery();
        return guardian;
    }

    public bool DeleteGuardian(ulong guardianUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM char_soulguardian WHERE guardian_uid = @g", conn);
        cmd.Parameters.AddWithValue("g", unchecked((long)guardianUid));
        return cmd.ExecuteNonQuery() == 1;
    }

    public void SetSlot(ulong characterId, byte slotNum, ulong? guardianUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO char_soulguardian_slot (char_uid, slot_num, guardian_uid)
            VALUES (@c, @sn, @g)
            ON CONFLICT (char_uid, slot_num) DO UPDATE
                SET guardian_uid = EXCLUDED.guardian_uid
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("sn", (short)slotNum);
        cmd.Parameters.AddWithValue("g", guardianUid.HasValue ? (object)unchecked((long)guardianUid.Value) : DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<CharSoulGuardianSlot> GetSlots(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT char_uid, slot_num, guardian_uid FROM char_soulguardian_slot WHERE char_uid = @c ORDER BY slot_num",
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        var result = new List<CharSoulGuardianSlot>();
        while (reader.Read())
        {
            result.Add(new CharSoulGuardianSlot
            {
                CharacterId = unchecked((ulong)reader.GetInt64(0)),
                SlotNum = (byte)reader.GetInt16(1),
                GuardianUid = reader.IsDBNull(2) ? null : unchecked((ulong)reader.GetInt64(2)),
            });
        }
        return result;
    }

    private const string GuardianSelect = """
        SELECT guardian_uid, char_uid, guardian_type, slot_num, equip_flag,
               guardian_name, f_type, pts_p, pts_m, pts_s, pts_c,
               skill_point, created_at
        FROM char_soulguardian
        """;

    private static CharSoulGuardian Read(NpgsqlDataReader reader) => new()
    {
        GuardianUid = unchecked((ulong)reader.GetInt64(0)),
        CharacterId = unchecked((ulong)reader.GetInt64(1)),
        GuardianType = reader.GetInt16(2),
        SlotNum = (byte)reader.GetInt16(3),
        EquipFlag = (byte)reader.GetInt16(4),
        GuardianName = reader.GetString(5),
        FType = (byte)reader.GetInt16(6),
        PtsP = (byte)reader.GetInt16(7),
        PtsM = (byte)reader.GetInt16(8),
        PtsS = (byte)reader.GetInt16(9),
        PtsC = (byte)reader.GetInt16(10),
        SkillPoint = reader.GetInt32(11),
        CreatedAt = new DateTimeOffset(reader.GetDateTime(12), TimeSpan.Zero),
    };
}

public sealed class PostgresSoulSeedStore : ISoulSeedStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSoulSeedStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public CharSoulSeed GetOrCreate(ulong characterId, int seedItemIndex)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO char_soulseed (char_uid, seed_item_index)
            VALUES (@c, @idx)
            ON CONFLICT (char_uid) DO UPDATE
                SET char_uid = EXCLUDED.char_uid
            RETURNING char_uid, seed_item_index, power_point, magic_point, sense_point,
                      charm_point, special_power_point, special_magic_point,
                      special_sense_point, special_charm_point,
                      start_at, last_update_at
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("idx", seedItemIndex);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Read(reader);
    }

    public CharSoulSeed Upsert(CharSoulSeed soulSeed)
    {
        ArgumentNullException.ThrowIfNull(soulSeed);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO char_soulseed
                (char_uid, seed_item_index, power_point, magic_point, sense_point,
                 charm_point, special_power_point, special_magic_point,
                 special_sense_point, special_charm_point,
                 start_at, last_update_at)
            VALUES
                (@c, @idx, @pp, @mp, @sp, @cp, @spp, @smp, @ssp, @scp, NOW(), NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET seed_item_index      = EXCLUDED.seed_item_index,
                    power_point          = EXCLUDED.power_point,
                    magic_point          = EXCLUDED.magic_point,
                    sense_point          = EXCLUDED.sense_point,
                    charm_point          = EXCLUDED.charm_point,
                    special_power_point  = EXCLUDED.special_power_point,
                    special_magic_point  = EXCLUDED.special_magic_point,
                    special_sense_point  = EXCLUDED.special_sense_point,
                    special_charm_point  = EXCLUDED.special_charm_point,
                    last_update_at       = NOW()
            """,
            conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)soulSeed.CharacterId));
        cmd.Parameters.AddWithValue("idx", soulSeed.SeedItemIndex);
        cmd.Parameters.AddWithValue("pp", soulSeed.PowerPoint);
        cmd.Parameters.AddWithValue("mp", soulSeed.MagicPoint);
        cmd.Parameters.AddWithValue("sp", soulSeed.SensePoint);
        cmd.Parameters.AddWithValue("cp", soulSeed.CharmPoint);
        cmd.Parameters.AddWithValue("spp", soulSeed.SpecialPowerPoint);
        cmd.Parameters.AddWithValue("smp", soulSeed.SpecialMagicPoint);
        cmd.Parameters.AddWithValue("ssp", soulSeed.SpecialSensePoint);
        cmd.Parameters.AddWithValue("scp", soulSeed.SpecialCharmPoint);
        cmd.ExecuteNonQuery();
        return soulSeed;
    }

    public bool Delete(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM char_soulseed WHERE char_uid = @c", conn);
        cmd.Parameters.AddWithValue("c", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    private static CharSoulSeed Read(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        SeedItemIndex = reader.GetInt32(1),
        PowerPoint = reader.GetInt16(2),
        MagicPoint = reader.GetInt16(3),
        SensePoint = reader.GetInt16(4),
        CharmPoint = reader.GetInt16(5),
        SpecialPowerPoint = reader.GetInt16(6),
        SpecialMagicPoint = reader.GetInt16(7),
        SpecialSensePoint = reader.GetInt16(8),
        SpecialCharmPoint = reader.GetInt16(9),
        StartAt = new DateTimeOffset(reader.GetDateTime(10), TimeSpan.Zero),
        LastUpdateAt = new DateTimeOffset(reader.GetDateTime(11), TimeSpan.Zero),
    };
}
