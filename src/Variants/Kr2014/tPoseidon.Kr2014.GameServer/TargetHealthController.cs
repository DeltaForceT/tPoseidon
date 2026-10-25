using tPoseidon.Core.Common;
using tPoseidon.Core.Net.World;

internal sealed class TargetHealthController
{
    private readonly Func<ushort, ushort, InGameSessionState?> _playerOnMap;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public TargetHealthController(Func<ushort, ushort, InGameSessionState?> playerOnMap, string logTag)
    {
        _playerOnMap = playerOnMap ?? throw new ArgumentNullException(nameof(playerOnMap));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public Task QueryAsync(InGameSessionState asker, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(asker);
        if (!TargetHealthFrames.TryReadRequest(payload.Span, out var entityId))
        {
            Console.WriteLine($"{_logTag} [target health] request of {payload.Length} bytes not read");
            return Task.CompletedTask;
        }
        if (_playerOnMap(asker.MapId, entityId) is not { } target)
        {
            return SendAsync(asker, Opcode.S2C_TARGET_HEALTH_UNKNOWN, Array.Empty<byte>());
        }
        return SendAsync(asker, Opcode.S2C_TARGET_HEALTH,
            TargetHealthFrames.Health(target.PlayerEntityId, target.PlayerMaxHp, target.Stats?.CurrentHp ?? 0));
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
