using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresReportStore : IReportStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresReportStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public ReportedCharacter? FindCharacter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            "SELECT account_id, character_id, name FROM characters WHERE LOWER(name) = LOWER(@n) LIMIT 2", conn);
        cmd.Parameters.AddWithValue("n", name);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var found = new ReportedCharacter((uint)reader.GetInt64(0), unchecked((ulong)reader.GetInt64(1)), reader.GetString(2));
        return reader.Read() ? null : found;
    }

    public int Insert(PlayerReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        int code;
        using (var cmd = new NpgsqlCommand("""
            INSERT INTO tellongm (tellongm_island, tellongm_created, send_char, receive_char, tellongm_title, tellongm_desc,
                                  tellongm_kind, handling_type, is_still, handling_gm, handling_date, handling_desc)
            VALUES (@island, NOW(), @send, @receive, @title, @desc, @kind, 1, @still, NULL, NULL, NULL)
            RETURNING tellongm_code
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("island", (short)report.Island);
            cmd.Parameters.AddWithValue("send", unchecked((long)report.PlaintiffCharUid));
            cmd.Parameters.AddWithValue("receive", unchecked((long)report.DefendantCharUid));
            cmd.Parameters.AddWithValue("title", Truncate(report.Title, ReportRules.TitleLength));
            cmd.Parameters.AddWithValue("desc", Truncate(report.Description, ReportRules.DescriptionLength));
            cmd.Parameters.AddWithValue("kind", (short)(report.NotifyKind + 1));
            cmd.Parameters.AddWithValue("still", report.Title.Contains(ReportRules.StealWord, StringComparison.OrdinalIgnoreCase));
            code = (int)cmd.ExecuteScalar()!;
        }
        for (var i = 0; i < report.Chatting.Count; i++)
        {
            using var chat = new NpgsqlCommand(
                "INSERT INTO tellongm_chatting (tellongm_code, chatting_no, chatting) VALUES (@code, @no, @text)", conn, tx);
            chat.Parameters.AddWithValue("code", code);
            chat.Parameters.AddWithValue("no", (short)i);
            chat.Parameters.AddWithValue("text", report.Chatting[i]);
            chat.ExecuteNonQuery();
        }
        tx.Commit();
        return code;
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];
}
