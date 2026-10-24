using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresServerRegistryStore : IServerRegistryStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresServerRegistryStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public ServerRegistryEntry? GetWorldSelect(int gameCode, int worldCode, int islandCode)
    {
        try
        {
            using var conn = _dataSource.OpenConnectionForActiveSchema();
            using var cmd = new NpgsqlCommand(
                """
                SELECT W.world_name,
                       I.island_name,
                       TRIM(I.island_ls_ip) AS island_ls_ip,
                       I.island_ls_port,
                       I.island_user_max,
                       I.island_user_cur
                FROM tbl_island AS I
                INNER JOIN tbl_world AS W
                    ON W.game_code = I.game_code AND W.world_code = I.world_code
                WHERE I.game_code = @g
                  AND I.world_code = @w
                  AND I.island_code = @i
                  AND W.world_enable  = TRUE
                  AND I.island_enable = TRUE
                LIMIT 1
                """,
                conn);
            cmd.Parameters.AddWithValue("g", gameCode);
            cmd.Parameters.AddWithValue("w", worldCode);
            cmd.Parameters.AddWithValue("i", islandCode);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            var worldName = reader.GetString(0);
            var islandName = reader.GetString(1);
            var islandIp = reader.GetString(2);
            var islandPort = reader.GetInt32(3);
            if (string.IsNullOrWhiteSpace(islandIp) || islandPort is <= 0 or > 65535)
            {
                return null;
            }
            return new ServerRegistryEntry
            {
                GameCode = gameCode,
                WorldCode = worldCode,
                IslandCode = islandCode,
                WorldName = worldName,
                IslandName = islandName,
                IslandIp = islandIp,
                IslandPort = islandPort,
                UserMax = reader.GetInt32(4),
                UserCur = reader.GetInt32(5),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    public ServerProcessParm? GetServerParm(int svrNo)
    {
        try
        {
            using var conn = _dataSource.OpenConnectionForActiveSchema();
            using var cmd = new NpgsqlCommand(
                """
                SELECT svr_no, svr_type, TRIM(major_ip), minor_ip, world_no,
                       tcp_port, udp_port, session_cnt, svr_desc
                FROM tbl_parm_server
                WHERE is_valid = TRUE AND svr_no = @n
                LIMIT 1
                """,
                conn);
            cmd.Parameters.AddWithValue("n", svrNo);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            var majorIp = reader.GetString(2);
            var tcpPort = reader.GetInt32(5);
            if (string.IsNullOrWhiteSpace(majorIp) || tcpPort is <= 0 or > 65535)
            {
                return null;
            }
            return new ServerProcessParm
            {
                SvrNo = reader.GetInt32(0),
                SvrType = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                MajorIp = majorIp,
                MinorIp = reader.IsDBNull(3) ? null : reader.GetString(3),
                WorldNo = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                TcpPort = tcpPort,
                UdpPort = reader.GetInt32(6),
                SessionCnt = reader.GetInt32(7),
                Desc = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
