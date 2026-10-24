using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGmBlockStore : IGmBlockStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresGmBlockStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public int BlockCharacter(ulong characterId, byte blockType, ushort minutes, ulong gameMasterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT gsp_block_char(@uid, @type, @reason, @gm_type, @gm, @minutes, @desc, @note)", conn);
        cmd.Parameters.AddWithValue("uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("type", (short)blockType);
        cmd.Parameters.AddWithValue("reason", (short)GmBlockRules.NoReason);
        cmd.Parameters.AddWithValue("gm_type", (short)GmBlockRules.ByGameMasterCharacter);
        cmd.Parameters.AddWithValue("gm", unchecked((long)gameMasterId));
        cmd.Parameters.AddWithValue("minutes", (int)minutes);
        cmd.Parameters.AddWithValue("desc", GmBlockRules.GameMasterCharacterDesc);
        cmd.Parameters.AddWithValue("note", GmBlockRules.GameMasterCharacterNote);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public int UnblockCharacter(ulong characterId, byte blockType, ulong gameMasterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT gsp_unblock_char(@uid, @type, @gm_type, @gm)", conn);
        cmd.Parameters.AddWithValue("uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("type", (short)blockType);
        cmd.Parameters.AddWithValue("gm_type", (short)GmBlockRules.ByGameMasterCharacter);
        cmd.Parameters.AddWithValue("gm", unchecked((long)gameMasterId));
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public int SetUserAuthority(string characterName, byte authority)
    {
        ArgumentNullException.ThrowIfNull(characterName);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT usp_update_user_authority_by_char_name(@name, @authority)", conn);
        cmd.Parameters.AddWithValue("name", characterName);
        cmd.Parameters.AddWithValue("authority", (short)authority);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public GmRunningBlock? LatestRunningBlock(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT block_type,
                   (floor(extract(epoch FROM block_to) / 60) - floor(extract(epoch FROM now()) / 60))::integer
              FROM gm_block1
             WHERE uid = @uid AND uid_type = 2 AND block_to > now()
             ORDER BY block_to DESC
             LIMIT 1
            """,
            conn);
        cmd.Parameters.AddWithValue("uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new GmRunningBlock((byte)reader.GetInt16(0), reader.GetInt32(1));
    }
}
