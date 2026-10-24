using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresWeddingBoardStore : IWeddingBoardStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresWeddingBoardStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public IReadOnlyList<WeddingArticle> Articles()
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            SELECT b.couple_code, b.card_type, b.dress_type, b.male_hair_type, b.female_hair_type, b.male_job_type, b.female_job_type,
                   b.card_msg, b.rsv_title, b.male_name, b.female_name
            FROM couple_wedding_rsv_board b JOIN couple c ON c.couple_code = b.couple_code
            WHERE c.couple_authority = FALSE
            ORDER BY b.latest_update_at DESC, b.couple_code DESC
            """, conn);
        using var reader = cmd.ExecuteReader();
        var articles = new List<WeddingArticle>();
        while (reader.Read())
        {
            articles.Add(new WeddingArticle(
                (ulong)reader.GetInt64(0), (byte)reader.GetInt16(1), reader.GetInt32(2), (byte)reader.GetInt16(3), (byte)reader.GetInt16(4),
                (byte)reader.GetInt16(5), (byte)reader.GetInt16(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10)));
        }
        return articles;
    }

    public bool HasArticle(ulong coupleCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM couple_wedding_rsv_board WHERE couple_code = @c)", conn);
        cmd.Parameters.AddWithValue("c", (long)coupleCode);
        return cmd.ExecuteScalar() is true;
    }

    public WeddingArticleSave SaveArticle(WeddingArticle article, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(article);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO couple_wedding_rsv_board
                (couple_code, rsv_title, wedding_map_id, card_msg, card_type, dress_type,
                 male_hair_type, male_job_type, male_name, female_hair_type, female_job_type, female_name, latest_update_at)
            VALUES (@c, @title, 0, @msg, @card, @dress, @mh, @mj, @mn, @fh, @fj, @fn, @at)
            ON CONFLICT (couple_code) DO UPDATE SET
                rsv_title = EXCLUDED.rsv_title, wedding_map_id = EXCLUDED.wedding_map_id, card_msg = EXCLUDED.card_msg,
                card_type = EXCLUDED.card_type, dress_type = EXCLUDED.dress_type,
                male_hair_type = EXCLUDED.male_hair_type, male_job_type = EXCLUDED.male_job_type, male_name = EXCLUDED.male_name,
                female_hair_type = EXCLUDED.female_hair_type, female_job_type = EXCLUDED.female_job_type, female_name = EXCLUDED.female_name,
                latest_update_at = EXCLUDED.latest_update_at
            RETURNING (xmax = 0)
            """, conn);
        cmd.Parameters.AddWithValue("c", (long)article.CoupleCode);
        cmd.Parameters.AddWithValue("title", article.Title);
        cmd.Parameters.AddWithValue("msg", article.CardMessage);
        cmd.Parameters.AddWithValue("card", (short)article.CardType);
        cmd.Parameters.AddWithValue("dress", article.DressType);
        cmd.Parameters.AddWithValue("mh", (short)article.MaleHair);
        cmd.Parameters.AddWithValue("mj", (short)article.MaleJob);
        cmd.Parameters.AddWithValue("mn", article.MaleName);
        cmd.Parameters.AddWithValue("fh", (short)article.FemaleHair);
        cmd.Parameters.AddWithValue("fj", (short)article.FemaleJob);
        cmd.Parameters.AddWithValue("fn", article.FemaleName);
        cmd.Parameters.AddWithValue("at", at.ToUniversalTime());
        return cmd.ExecuteScalar() switch
        {
            true => WeddingArticleSave.Created,
            false => WeddingArticleSave.Updated,
            _ => WeddingArticleSave.Failed,
        };
    }

    public bool DeleteArticle(ulong coupleCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand("DELETE FROM couple_wedding_rsv_board WHERE couple_code = @c", conn);
        cmd.Parameters.AddWithValue("c", (long)coupleCode);
        return cmd.ExecuteNonQuery() > 0;
    }
}
