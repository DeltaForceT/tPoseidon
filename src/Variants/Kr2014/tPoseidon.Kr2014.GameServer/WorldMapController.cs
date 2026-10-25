using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

internal sealed class WorldMapController
{
    public const byte InvalidZone = 0;
    public const byte InvalidState = 1;

    private readonly Func<WorldMapTables> _tables;
    private readonly string _logTag;

    public WorldMapController(Func<WorldMapTables> tables, string logTag)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public static bool Busy(InGameSessionState player) =>
        player.IsDead || player.Trade is not null || player.Stall is not null || player.VisitingKeeper is not null
        || player.CoupleMaking is not null || player.CoupleSelecting || player.MarriageExchange || player.CardIdentify || player.WorldMapOpen;

    public async Task OpenAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (Busy(player))
        {
            await Send(player, Opcode.S2C_WORLD_MAP_REFUSED, [InvalidState]).ConfigureAwait(false);
            return;
        }
        if (!_tables().Shows(player.MapId))
        {
            Console.WriteLine($"{_logTag} [worldmap] 0x{player.PlayerEntityId:X4} asks for the world map in zone {player.MapId}: not on the map");
            await Send(player, Opcode.S2C_WORLD_MAP_REFUSED, [InvalidZone]).ConfigureAwait(false);
            return;
        }
        player.WorldMapOpen = true;
        await Send(player, Opcode.S2C_WORLD_MAP_OPENED, Array.Empty<byte>()).ConfigureAwait(false);
    }

    public static Task CloseAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.WorldMapOpen = false;
        return Task.CompletedTask;
    }

    private static Task Send(InGameSessionState player, Opcode opcode, byte[] payload) =>
        player.SendFrameAsync(opcode, payload, (byte)Random.Shared.Next(1, 256), default);
}
