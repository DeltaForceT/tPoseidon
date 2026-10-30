using tPoseidon.Core.Common;

namespace tPoseidon.Core.InterServer;

public enum ServerRole
{
    Login,
    Game,
    System,
}

public sealed record ServerIdentity
{
    public required string Name { get; init; }
    public required ServerRole Role { get; init; }
    public required ushort ServerIndex { get; init; }

    public static readonly ServerIdentity Login1 =
        new() { Name = "lserver1", Role = ServerRole.Login, ServerIndex = 1 };

    public static readonly ServerIdentity Game1 =
        new() { Name = "gserver1", Role = ServerRole.Game, ServerIndex = 2 };

    public static readonly ServerIdentity Game2 =
        new() { Name = "gserver2", Role = ServerRole.Game, ServerIndex = 3 };

    public static readonly IReadOnlyList<ServerIdentity> GameServers = [Game1, Game2];

    public static readonly ServerIdentity System1 =
        new() { Name = "sserver1", Role = ServerRole.System, ServerIndex = 4 };

    public ushort GameListenPort => ServerIndex switch
    {
        2 => ServerEndpointConfig.GamePort,
        3 => ServerEndpointConfig.GameAltPort,
        _ => throw new InvalidOperationException(
            $"{Name} (idx {ServerIndex}) is not a game server; it has no game listen port."),
    };
}
