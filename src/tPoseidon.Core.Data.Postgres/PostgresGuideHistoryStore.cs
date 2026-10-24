using tPoseidon.Core.Data;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresGuideHistoryStore : IGuideHistoryStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresGuideHistoryStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public GuideHistory? GetForCharacter(ulong characterId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT system_guide, tutorial_guide
            FROM guide_history
            WHERE char_uid = @char_uid
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new GuideHistory
        {
            CharacterId = characterId,
            SystemGuide = reader.GetString(0),
            TutorialGuide = unchecked((ulong)reader.GetInt64(1)),
        };
    }

    public void UpsertSystemGuide(ulong characterId, string systemGuide)
    {
        ArgumentNullException.ThrowIfNull(systemGuide);
        if (systemGuide.Length > 1000)
        {
            throw new ArgumentException(
                $"system_guide column is varchar(1000); got {systemGuide.Length} chars.",
                nameof(systemGuide));
        }
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guide_history (char_uid, system_guide, tutorial_guide, update_date)
            VALUES (@char_uid, @system_guide, 0, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET system_guide = EXCLUDED.system_guide,
                    update_date  = EXCLUDED.update_date
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("system_guide", systemGuide);
        cmd.ExecuteNonQuery();
    }

    public void UpsertTutorialGuide(ulong characterId, ulong tutorialGuide)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO guide_history (char_uid, system_guide, tutorial_guide, update_date)
            VALUES (@char_uid, '', @tutorial_guide, NOW())
            ON CONFLICT (char_uid) DO UPDATE
                SET tutorial_guide = EXCLUDED.tutorial_guide,
                    update_date    = EXCLUDED.update_date
            """,
            conn);
        cmd.Parameters.AddWithValue("char_uid", unchecked((long)characterId));
        cmd.Parameters.AddWithValue("tutorial_guide", unchecked((long)tutorialGuide));
        cmd.ExecuteNonQuery();
    }
}
