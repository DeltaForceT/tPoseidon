using tPoseidon.Core.Data;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresSafetyPasswordStore : ISafetyPasswordStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSafetyPasswordStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public SafetyPassword? Find(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT safety_pw_hash, lock_flag, lock_limit_bitmask, email, created_at, updated_at
            FROM account_safety_password
            WHERE account_id = @account_id
            """,
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new SafetyPassword
        {
            AccountId = accountId,
            PasswordHash = reader.GetString(0),
            LockFlag = (byte)reader.GetInt16(1),
            LockLimitBitmask = (byte)reader.GetInt16(2),
            Email = reader.GetString(3),
            CreatedAt = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
        };
    }

    public void Upsert(SafetyPassword record)
    {
        ArgumentNullException.ThrowIfNull(record);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO account_safety_password
                (account_id, safety_pw_hash, lock_flag, lock_limit_bitmask, email, created_at, updated_at)
            VALUES
                (@account_id, @hash, @lock_flag, @lock_limit, @email, NOW(), NOW())
            ON CONFLICT (account_id) DO UPDATE
                SET safety_pw_hash     = EXCLUDED.safety_pw_hash,
                    lock_flag          = EXCLUDED.lock_flag,
                    lock_limit_bitmask = EXCLUDED.lock_limit_bitmask,
                    email              = EXCLUDED.email,
                    updated_at         = NOW()
            """,
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)record.AccountId);
        cmd.Parameters.AddWithValue("hash", record.PasswordHash);
        cmd.Parameters.AddWithValue("lock_flag", (short)record.LockFlag);
        cmd.Parameters.AddWithValue("lock_limit", (short)record.LockLimitBitmask);
        cmd.Parameters.AddWithValue("email", record.Email);
        cmd.ExecuteNonQuery();
    }

    public bool SetLockFlag(uint accountId, byte lockFlag)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE account_safety_password
            SET lock_flag = @lock_flag,
                updated_at = NOW()
            WHERE account_id = @account_id
            """,
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        cmd.Parameters.AddWithValue("lock_flag", (short)lockFlag);
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool Delete(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM account_safety_password WHERE account_id = @account_id",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        return cmd.ExecuteNonQuery() == 1;
    }

    public void AppendLog(SafetyPasswordLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO account_safety_password_log
                (log_type, account_id, safety_pw_hash,
                 lock_flag, lock_limit_bitmask, email,
                 gm_id, reason)
            VALUES
                (@log_type, @account_id, @safety_pw_hash,
                 @lock_flag, @lock_limit_bitmask, @email,
                 @gm_id, @reason)
            """,
            conn);
        cmd.Parameters.AddWithValue("log_type", (short)entry.LogType);
        cmd.Parameters.AddWithValue("account_id", (long)entry.AccountId);
        cmd.Parameters.AddWithValue("safety_pw_hash", entry.PasswordHash);
        cmd.Parameters.AddWithValue("lock_flag", entry.LockFlag.HasValue ? (object)(short)entry.LockFlag.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("lock_limit_bitmask", entry.LockLimitBitmask.HasValue ? (object)(short)entry.LockLimitBitmask.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("email", entry.Email);
        cmd.Parameters.AddWithValue("gm_id", (object?)entry.GmId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("reason", (object?)entry.Reason ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<SafetyPasswordLogEntry> GetLog(uint accountId, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT log_seq, log_type, account_id, safety_pw_hash,
                   lock_flag, lock_limit_bitmask, email,
                   created_at, gm_id, reason
            FROM account_safety_password_log
            WHERE account_id = @account_id
            ORDER BY log_seq DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<SafetyPasswordLogEntry>();
        while (reader.Read())
        {
            result.Add(new SafetyPasswordLogEntry
            {
                LogSeq = reader.GetInt64(0),
                LogType = (SafetyPasswordLogType)(byte)reader.GetInt16(1),
                AccountId = unchecked((uint)reader.GetInt64(2)),
                PasswordHash = reader.GetString(3),
                LockFlag = reader.IsDBNull(4) ? null : (byte)reader.GetInt16(4),
                LockLimitBitmask = reader.IsDBNull(5) ? null : (byte)reader.GetInt16(5),
                Email = reader.GetString(6),
                CreatedAt = new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero),
                GmId = reader.IsDBNull(8) ? null : reader.GetString(8),
                Reason = reader.IsDBNull(9) ? null : reader.GetString(9),
            });
        }
        return result;
    }
}
