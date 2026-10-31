using System.Net;
using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresLaunchTicketStore : ILaunchTicketStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresLaunchTicketStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public void Issue(uint accountId, IReadOnlyCollection<IPAddress> clientIps, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(clientIps);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            WITH cleanup AS (DELETE FROM launch_tickets WHERE expires_at <= NOW() OR account_id = @id)
            INSERT INTO launch_tickets (account_id, client_ips, expires_at)
            VALUES (@id, @ips, NOW() + make_interval(secs => @secs))
            """,
            conn);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        cmd.Parameters.AddWithValue("ips", clientIps.Select(ip => LaunchTicket.Normalize(ip).ToString()).Distinct().ToArray());
        cmd.Parameters.AddWithValue("secs", lifetime.TotalSeconds);
        cmd.ExecuteNonQuery();
    }

    public bool Consume(uint accountId, IPAddress clientIp)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            UPDATE launch_tickets
            SET client_ips = ARRAY[@ip], expires_at = NOW() + make_interval(secs => @secs)
            WHERE ticket_id = (
                SELECT ticket_id FROM launch_tickets
                WHERE account_id = @id AND expires_at > NOW() AND @ip = ANY(client_ips)
                ORDER BY expires_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            """,
            conn);
        cmd.Parameters.AddWithValue("id", (long)accountId);
        cmd.Parameters.AddWithValue("ip", LaunchTicket.Normalize(clientIp).ToString());
        cmd.Parameters.AddWithValue("secs", LaunchTicket.SessionLifetime.TotalSeconds);
        return cmd.ExecuteNonQuery() == 1;
    }
}
