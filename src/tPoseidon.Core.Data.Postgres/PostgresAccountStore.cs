using System.Net;
using System.Security.Cryptography;
using Npgsql;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresAccountStore : IAccountStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _clock;
    private readonly ICharStore _chars;

    public PostgresAccountStore(NpgsqlDataSource dataSource, TimeProvider? clock = null, ICharStore? charStore = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
        _clock = clock ?? TimeProvider.System;
        _chars = charStore ?? new PostgresCharStore(dataSource);
    }

        public const string DefaultConnectionString =
        "Host=localhost;Port=5433;Database=modern_trickster;Username=mt;Password=mt_dev";

        public static string? ResolveSchema() =>
        Environment.GetEnvironmentVariable("MT_DB_SCHEMA");

        public static string ResolveConnectionString()
    {
        return Environment.GetEnvironmentVariable("MT_DB_CONNECTION") ?? DefaultConnectionString;
    }

    public Account? GetAccountByUsername(string username)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT account_id, username, password_placeholder FROM accounts WHERE LOWER(username) = LOWER(@username)",
            conn);
        cmd.Parameters.AddWithValue("username", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadAccount(reader);
    }

    public IReadOnlyList<string> ListUsernames()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT username FROM accounts ORDER BY LOWER(username)", conn);
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    public Account? GetAccountById(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT account_id, username, password_placeholder FROM accounts WHERE account_id = @account_id",
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadAccount(reader);
    }

    public Account? AuthenticateAccount(string username, string passwordPlaceholder)
    {
        var account = GetAccountByUsername(username);
        if (account is null) return null;

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(account.PasswordPlaceholder),
            System.Text.Encoding.UTF8.GetBytes(passwordPlaceholder))
            ? account
            : null;
    }

        public void UpsertAccount(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO accounts (account_id, username, password_placeholder)
            VALUES (@account_id, @username, @password_placeholder)
            ON CONFLICT (account_id) DO NOTHING
            """,
            conn);
        cmd.Parameters.AddWithValue("account_id", (long)account.AccountId);
        cmd.Parameters.AddWithValue("username", account.Username);
        cmd.Parameters.AddWithValue("password_placeholder", account.PasswordPlaceholder);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<Character> GetCharactersForAccount(uint accountId) =>
        _chars.GetCharactersForAccount(accountId);

    public Character? GetCharacterById(ulong characterId) =>
        _chars.GetCharacterById(characterId);

    public void AddCharacter(Character character) =>
        _chars.AddCharacter(character);

    public bool SetCharacterMap(ulong characterId, ushort mapId) =>
        _chars.SetCharacterMap(characterId, mapId);

    public bool SetCharacterHome(ulong characterId, ushort homeMapId) =>
        _chars.SetCharacterHome(characterId, homeMapId);

    public bool SetCharacterPosition(ulong characterId, ushort mapId, ushort positionX, ushort positionY) =>
        _chars.SetCharacterPosition(characterId, mapId, positionX, positionY);

    public bool SaveCharacterStats(ulong characterId, CharacterStatsSnapshot snapshot) =>
        _chars.SaveCharacterStats(characterId, snapshot);

    public uint EditCharacter(ulong characterId, CharacterEdit edit) =>
        _chars.EditCharacter(characterId, edit);

    public bool DeleteCharacter(ulong characterId, uint ownerAccountId) =>
        _chars.DeleteCharacter(characterId, ownerAccountId);

    public SessionToken IssueSessionToken(uint accountId, TimeSpan ttl) =>
        SeedSessionToken(NextTokenValue(), accountId, ttl);

    public SessionToken SeedSessionToken(uint tokenValue, uint accountId, TimeSpan ttl)
    {
        var now = _clock.GetUtcNow();
        var expires = now + ttl;
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO session_tokens (token_value, account_id, issued_at, expires_at)
            VALUES (@token_value, @account_id, @issued_at, @expires_at)
            ON CONFLICT (token_value) DO UPDATE
            SET account_id = EXCLUDED.account_id,
                issued_at = EXCLUDED.issued_at,
                expires_at = EXCLUDED.expires_at
            """,
            conn);
        cmd.Parameters.AddWithValue("token_value", (long)tokenValue);
        cmd.Parameters.AddWithValue("account_id", (long)accountId);
        cmd.Parameters.AddWithValue("issued_at", now.UtcDateTime);
        cmd.Parameters.AddWithValue("expires_at", expires.UtcDateTime);
        cmd.ExecuteNonQuery();

        return new SessionToken
        {
            Value = tokenValue,
            AccountId = accountId,
            IssuedAt = now,
            ExpiresAt = expires,
        };
    }

    public SessionToken? ValidateAndConsumeSessionToken(uint tokenValue)
    {
        var now = _clock.GetUtcNow();
        using var conn = _dataSource.OpenConnectionForActiveSchema();

        using var cmd = new NpgsqlCommand(
            """
            DELETE FROM session_tokens
            WHERE token_value = @token_value
            RETURNING token_value, account_id, issued_at, expires_at
            """,
            conn);
        cmd.Parameters.AddWithValue("token_value", (long)tokenValue);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var token = new SessionToken
        {
            Value = unchecked((uint)reader.GetInt64(0)),
            AccountId = unchecked((uint)reader.GetInt64(1)),
            IssuedAt = reader.GetFieldValue<DateTime>(2),
            ExpiresAt = reader.GetFieldValue<DateTime>(3),
        };
        return token.IsExpired(now) ? null : token;
    }

    private static Account ReadAccount(NpgsqlDataReader reader) => new()
    {
        AccountId = unchecked((uint)reader.GetInt64(0)),
        Username = reader.GetString(1),
        PasswordPlaceholder = reader.GetString(2),

        CharacterIds = Array.Empty<ulong>(),
    };

    private static uint NextTokenValue()
    {
        Span<byte> buf = stackalloc byte[4];
        RandomNumberGenerator.Fill(buf);
        return BitConverter.ToUInt32(buf);
    }

    public Account RegisterAccount(string username, string passwordHash, string authority = "A")
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);

        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"INSERT INTO accounts (account_id, username, password_placeholder, password_hash,
                                    account_authority, account_access, account_created_at)
              VALUES (nextval('accounts_account_id_seq'), @u, '', @ph, @auth, 0, NOW())
              RETURNING account_id", conn);
        cmd.Parameters.AddWithValue("u", username);
        cmd.Parameters.AddWithValue("ph", passwordHash);
        cmd.Parameters.AddWithValue("auth", authority);
        try
        {
            var idObj = cmd.ExecuteScalar();
            if (idObj is null) throw new InvalidOperationException("RegisterAccount returned no id");
            var newId = unchecked((uint)Convert.ToInt64(idObj));
            return new Account
            {
                AccountId = newId,
                Username = username,
                PasswordPlaceholder = string.Empty,
                CharacterIds = Array.Empty<ulong>(),
            };
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException($"Username '{username}' already exists.", ex);
        }
    }

    public bool VerifyPassword(string username, string plaintextPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentException.ThrowIfNullOrEmpty(plaintextPassword);

        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT password_hash FROM accounts WHERE username = @u LIMIT 1", conn);
        cmd.Parameters.AddWithValue("u", username);
        var hashObj = cmd.ExecuteScalar();
        if (hashObj is null || hashObj is DBNull) return false;
        var hash = (string)hashObj;
        if (string.IsNullOrEmpty(hash)) return false;
        return PasswordHasher.Verify(plaintextPassword, hash);
    }

    public void IssueGameAuthcode(uint accountId, long authcode, IPAddress clientIp)
    {
        ArgumentNullException.ThrowIfNull(clientIp);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE accounts SET
                acc_game_authcode         = @code,
                acc_game_state            = 2,
                acc_game_state_changed_at = NOW(),
                acc_game_latest_ip        = @ip,
                acc_game_latest_login_at  = NOW(),
                acc_game_login_count      = acc_game_login_count + 1,
                account_latest_login_at   = NOW(),
                account_latest_ip         = @ip,
                account_login_count       = account_login_count + 1
              WHERE account_id = @id", conn);
        cmd.Parameters.AddWithValue("code", authcode);
        cmd.Parameters.AddWithValue("ip", clientIp);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        cmd.ExecuteNonQuery();
    }

    public void TransitionToWorldSelected(uint accountId, IPAddress clientIp, short world, short island, ulong selectedCharUid)
    {
        ArgumentNullException.ThrowIfNull(clientIp);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE accounts SET
                acc_game_state_changed_at   = NOW(),
                acc_game_latest_ip          = @ip,
                acc_game_latest_world       = @w,
                acc_game_latest_island      = @i,
                acc_game_selected_char_uid  = @cuid
              WHERE account_id = @id AND acc_game_state = 4", conn);
        cmd.Parameters.AddWithValue("ip", clientIp);
        cmd.Parameters.AddWithValue("w", world);
        cmd.Parameters.AddWithValue("i", island);
        cmd.Parameters.AddWithValue("cuid", (long)selectedCharUid);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        cmd.ExecuteNonQuery();
    }

    public bool TransitionToServerSelected(uint accountId, short world, short island)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE accounts SET
                acc_game_state            = 3,
                acc_game_state_changed_at = NOW(),
                acc_game_latest_world     = @w,
                acc_game_latest_island    = @i
              WHERE account_id = @id AND acc_game_state = 2", conn);
        cmd.Parameters.AddWithValue("w", world);
        cmd.Parameters.AddWithValue("i", island);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        return cmd.ExecuteNonQuery() == 1;
    }

    public AccountGameLogin? GetGameLogin(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT acc_game_authcode, acc_game_state FROM accounts WHERE account_id = @id", conn);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new AccountGameLogin(accountId, reader.GetInt64(0), reader.GetInt16(1));
    }

    public short AccountAccess(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT account_access FROM accounts WHERE account_id = @id", conn);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        return cmd.ExecuteScalar() is short access ? access : (short)0;
    }

    public void RefreshLatestIp(uint accountId, IPAddress clientIp)
    {
        ArgumentNullException.ThrowIfNull(clientIp);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "UPDATE accounts SET acc_game_latest_ip = @ip WHERE account_id = @id", conn);
        cmd.Parameters.AddWithValue("ip", clientIp);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        cmd.ExecuteNonQuery();
    }

    public PendingGameJoin? FindPendingGameJoin(IPAddress peerIp, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(peerIp);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"SELECT account_id, acc_game_authcode, acc_game_latest_world, acc_game_latest_island, acc_game_selected_char_uid
              FROM accounts
              WHERE acc_game_state = 4
                AND acc_game_selected_char_uid IS NOT NULL
                AND acc_game_latest_ip = @ip
                AND acc_game_state_changed_at > NOW() - @win
              ORDER BY acc_game_state_changed_at DESC
              LIMIT 1", conn);
        cmd.Parameters.AddWithValue("ip", peerIp);
        cmd.Parameters.Add(new NpgsqlParameter("win", NpgsqlTypes.NpgsqlDbType.Interval) { Value = window });
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var accountId = unchecked((uint)reader.GetInt64(0));
        var authcode = reader.GetInt64(1);
        var world = reader.IsDBNull(2) ? (short)0 : reader.GetInt16(2);
        var island = reader.IsDBNull(3) ? (short)0 : reader.GetInt16(3);
        var selectedCharUid = reader.IsDBNull(4) ? 0UL : unchecked((ulong)reader.GetInt64(4));
        return new PendingGameJoin(accountId, authcode, world, island, selectedCharUid);
    }

    public void TransitionToInGame(uint accountId, short island)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE accounts SET
                acc_game_selected_char_uid = NULL,
                acc_game_latest_island     = @i
              WHERE account_id = @id AND acc_game_state = 4 AND acc_game_selected_char_uid IS NOT NULL", conn);
        cmd.Parameters.AddWithValue("i", island);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        var rows = cmd.ExecuteNonQuery();
        if (rows == 0)
        {
            throw new InvalidOperationException(
                $"Account {accountId} has no pending character in state 4; cannot enter the game.");
        }
    }

    public void TransitionToOffline(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE accounts SET
                acc_game_state             = 0,
                acc_game_state_changed_at  = NOW(),
                acc_game_latest_logout_at  = NOW(),
                acc_game_playtime_minutes  = acc_game_playtime_minutes
                                             + GREATEST(0, EXTRACT(EPOCH FROM (NOW() - acc_game_latest_login_at))::int / 60)
              WHERE account_id = @id", conn);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        cmd.ExecuteNonQuery();
    }

    public LobbyAccountState? GetLobbyAccountState(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT account_access, acc_game_access, acc_game_state, acc_game_latest_login_at FROM accounts WHERE account_id = @id", conn);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new LobbyAccountState(
            reader.GetInt16(0),
            reader.GetInt16(1),
            reader.GetInt16(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3));
    }

    public PenaltyNotice? FindRunningPenalty(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"SELECT p.penalty_name, pu.release_date
                FROM penalty_user AS pu
                JOIN penalty AS p ON p.penalty_code = pu.penalty_code
               WHERE pu.account_id = @id AND pu.released_date IS NULL
               ORDER BY pu.release_date DESC
               LIMIT 1", conn);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new PenaltyNotice(reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1));
    }

    public int ReleaseEndedPenalties()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT uspj_penalty_user_update_release()", conn);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public int ResetLobbyStates()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE accounts SET
                acc_game_state         = 0,
                acc_game_logout_state  = 2
              WHERE acc_game_state IN (2, 3)", conn);
        return cmd.ExecuteNonQuery();
    }

    public bool EndLobbySession(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE accounts SET
                acc_game_state            = 0,
                acc_game_state_changed_at = NOW(),
                acc_game_latest_logout_at = NOW(),
                acc_game_logout_state     = 3
              WHERE account_id = @id AND acc_game_state = 2", conn);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        return cmd.ExecuteNonQuery() == 1;
    }

    public int ResetWorldStates()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE accounts SET
                acc_game_state             = 0,
                acc_game_state_changed_at  = NOW(),
                acc_game_latest_logout_at  = NOW(),
                acc_game_logout_state      = 4,
                acc_game_selected_char_uid = NULL,
                acc_game_playtime_minutes  = acc_game_playtime_minutes
                                             + GREATEST(0, EXTRACT(EPOCH FROM (NOW() - acc_game_latest_login_at))::int / 60)
              WHERE acc_game_state <> 0", conn);
        return cmd.ExecuteNonQuery();
    }

    public bool JoinGame(uint accountId, long authcode, short island)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE accounts SET
                acc_game_state            = 4,
                acc_game_state_changed_at = NOW(),
                acc_game_latest_island    = @i
              WHERE account_id = @id AND acc_game_state = 3 AND acc_game_authcode = @code", conn);
        cmd.Parameters.AddWithValue("i", island);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        cmd.Parameters.AddWithValue("code", authcode);
        return cmd.ExecuteNonQuery() == 1;
    }

    public void ResetGameState(uint accountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            @"UPDATE accounts SET
                acc_game_state            = 0,
                acc_game_state_changed_at = NOW()
              WHERE account_id = @id", conn);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        cmd.ExecuteNonQuery();
    }
}
