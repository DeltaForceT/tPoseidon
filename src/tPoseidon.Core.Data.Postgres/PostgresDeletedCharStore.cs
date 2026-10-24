using System.Net;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresDeletedCharStore : IDeletedCharStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDeletedCharStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public void SoftDelete(ulong characterId, uint accountId, string characterName, IPAddress? userIp)
    {
        ArgumentNullException.ThrowIfNull(characterName);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO deleted_char (char_uid, account_id, char_name, user_ip, deleted_at)
            VALUES (@char_uid, @account_id, @char_name, @user_ip, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET account_id = EXCLUDED.account_id,
                    char_name  = EXCLUDED.char_name,
                    user_ip    = EXCLUDED.user_ip,
                    deleted_at = EXCLUDED.deleted_at
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        cmd.Parameters.AddWithValue("char_name", characterName);
        cmd.Parameters.AddWithValue("user_ip", (object?)userIp ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public DeletedChar? FindByCharId(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT char_uid, account_id, char_name, user_ip, deleted_at FROM deleted_char WHERE char_uid = @char_uid",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<DeletedChar> ListForAccount(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT char_uid, account_id, char_name, user_ip, deleted_at FROM deleted_char WHERE account_id = @account_id ORDER BY deleted_at DESC",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        using var reader = cmd.ExecuteReader();
        var result = new List<DeletedChar>();
        while (reader.Read()) result.Add(Read(reader));
        return result;
    }

    public bool Purge(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM deleted_char WHERE char_uid = @char_uid",
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        return cmd.ExecuteNonQuery() == 1;
    }

    public bool Restore(ulong characterId, string? newName, string? gmId, string? reason)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();

        DeletedChar? rec;
        using (var selCmd = new NpgsqlCommand(
            "SELECT char_uid, account_id, char_name, user_ip, deleted_at FROM deleted_char WHERE char_uid = @char_uid",
            conn, tx))
        {
            selCmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            using var reader = selCmd.ExecuteReader();
            rec = reader.Read() ? Read(reader) : null;
        }
        if (rec is null) { tx.Rollback(); return false; }

        using (var delCmd = new NpgsqlCommand(
            "DELETE FROM deleted_char WHERE char_uid = @char_uid",
            conn, tx))
        {
            delCmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            delCmd.ExecuteNonQuery();
        }

        using (var logCmd = new NpgsqlCommand(
            """
            INSERT INTO char_restore_log
                (account_id, char_uid, old_name, new_name, deleted_at, restored_at, gm_id, reason)
            VALUES
                (@account_id, @char_uid, @old_name, @new_name, @deleted_at, NOW(), @gm_id, @reason)
            """,
            conn, tx))
        {
            logCmd.Parameters.AddWithValue("account_id", (long)rec.AccountId);
            logCmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
            logCmd.Parameters.AddWithValue("old_name", rec.CharacterName);
            logCmd.Parameters.AddWithValue("new_name", newName ?? rec.CharacterName);
            logCmd.Parameters.AddWithValue("deleted_at", rec.DeletedAt.UtcDateTime);
            logCmd.Parameters.AddWithValue("gm_id", (object?)gmId ?? DBNull.Value);
            logCmd.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
            logCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return true;
    }

    public IReadOnlyList<CharRestoreLogEntry> GetRestoreLog(ulong characterId, int limit = 50)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT seq, account_id, char_uid, old_name, new_name, deleted_at, restored_at, gm_id, reason
            FROM char_restore_log
            WHERE char_uid = @char_uid
            ORDER BY seq DESC
            LIMIT @limit
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("limit", limit);
        using var reader = cmd.ExecuteReader();
        var result = new List<CharRestoreLogEntry>();
        while (reader.Read())
        {
            result.Add(new CharRestoreLogEntry
            {
                Seq = reader.GetInt64(0),
                AccountId = unchecked((uint)reader.GetInt64(1)),
                CharacterId = unchecked((ulong)reader.GetInt64(2)),
                OldName = reader.GetString(3),
                NewName = reader.GetString(4),
                DeletedAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
                RestoredAt = new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero),
                GmId = reader.IsDBNull(7) ? null : reader.GetString(7),
                Reason = reader.IsDBNull(8) ? null : reader.GetString(8),
            });
        }
        return result;
    }

    private static DeletedChar Read(NpgsqlDataReader reader) => new()
    {
        CharacterId = unchecked((ulong)reader.GetInt64(0)),
        AccountId = unchecked((uint)reader.GetInt64(1)),
        CharacterName = reader.GetString(2),
        UserIp = reader.IsDBNull(3) ? null : reader.GetFieldValue<IPAddress>(3),
        DeletedAt = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
    };
}
