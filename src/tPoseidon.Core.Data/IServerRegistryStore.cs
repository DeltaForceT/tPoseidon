namespace tPoseidon.Core.Data;

public interface IServerRegistryStore
{
    ServerRegistryEntry? GetWorldSelect(int gameCode, int worldCode, int islandCode);

    ServerProcessParm? GetServerParm(int svrNo);
}

public sealed record ServerRegistryEntry
{
    public required int GameCode { get; init; }
    public required int WorldCode { get; init; }
    public required int IslandCode { get; init; }
    public required string WorldName { get; init; }
    public required string IslandName { get; init; }
    public required string IslandIp { get; init; }
    public required int IslandPort { get; init; }
    public int UserMax { get; init; }
    public int UserCur { get; init; }
}

public sealed record ServerProcessParm
{
    public required int SvrNo { get; init; }
    public int? SvrType { get; init; }
    public required string MajorIp { get; init; }
    public string? MinorIp { get; init; }
    public int? WorldNo { get; init; }
    public required int TcpPort { get; init; }
    public int UdpPort { get; init; }
    public int SessionCnt { get; init; }
    public string Desc { get; init; } = string.Empty;
}
