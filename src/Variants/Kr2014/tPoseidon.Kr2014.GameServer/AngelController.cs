using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Angel;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.Social;

internal sealed class AngelController
{
    private readonly SystemServerLink _link;
    private readonly Func<ulong, InGameSessionState?> _playerById;
    private readonly IAngelStore _store;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly string _logTag;
    private readonly Random _rng = new();

    public AngelController(SystemServerLink link, Func<ulong, InGameSessionState?> playerById, IAngelStore store, Func<ushort, Opcode, byte[], Task> sendToMap, string logTag)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _playerById = playerById ?? throw new ArgumentNullException(nameof(playerById));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _link.OrderReceived += OrderAsync;
        _link.MessageReceived += MessageAsync;
    }

    private Task OrderAsync(OfficerOrder order, CancellationToken ct)
    {
        switch (order.OrderType)
        {
            case AngelFrames.OrderNewbiePosition when AngelFrames.TryReadPair(order.Data, out var newbieId, out var angelId):
                var newbie = _playerById(newbieId);
                var spot = newbie is null
                    ? new AngelSpot(angelId, 0, 0, 0)
                    : new AngelSpot(angelId, newbie.MapId, (short)newbie.PlayerX, (short)newbie.PlayerY);
                Console.WriteLine($"{_logTag} [angel] newbie 0x{newbieId:X} position for angel 0x{angelId:X}: zone {spot.Zone} ({spot.X},{spot.Y})");
                return _link.ReportAsync(order with { Data = AngelFrames.Spot(spot) }, ct);
            case AngelFrames.OrderAngelMove when AngelFrames.ReadSpot(order.Data) is { } move:
                return WarpAsync(move.AngelCharacterId, (ushort)move.Zone, (ushort)move.X, (ushort)move.Y, "to the newbie", ct);
            case AngelFrames.OrderAngelReward when AngelFrames.TryReadCharacter(order.Data, out var rewardedId):
                return RewardAsync(rewardedId);
            default:
                return Task.CompletedTask;
        }
    }

    public Task LoginAsync(InGameSessionState st)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (st.Character is not { } character) return Task.CompletedTask;
        var result = _store.Result(character.CharacterId);
        var wing = result is not null && AngelRules.WingActive(result.LatestGraduation, DateTimeOffset.UtcNow);
        var w = new tPoseidon.Core.Net.PacketWriter(3);
        w.WriteByte(wing ? (byte)1 : (byte)0);
        w.WriteUInt16((ushort)Math.Clamp(result?.GraduationCount ?? 0, 0, ushort.MaxValue));
        return SendAsync(st, Opcode.S2C_OP_2E29_TBD, w.ToArray());
    }

    private async Task RewardAsync(ulong angelId)
    {
        if (_playerById(angelId) is not { } angel) return;
        var count = _store.Result(angelId)?.GraduationCount ?? 0;
        var w = new tPoseidon.Core.Net.PacketWriter(4);
        w.WriteUInt16(angel.PlayerEntityId);
        w.WriteUInt16((ushort)Math.Clamp(count, 0, ushort.MaxValue));
        await _sendToMap(angel.MapId, Opcode.S2C_OP_2E2A_TBD, w.ToArray()).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [angel] 0x{angelId:X} rewarded: wing mark for 7 days, {count} graduations");
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }

    private Task MessageAsync(ushort opcode, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (opcode != AngelFrames.Portal || !AngelFrames.TryReadReturnMessage(payload.Span, out var angelId, out var x, out var y, out var zone))
        {
            return Task.CompletedTask;
        }
        return WarpAsync(angelId, zone, x, y, "back", ct);
    }

    private async Task WarpAsync(ulong angelId, ushort zone, ushort x, ushort y, string where, CancellationToken ct)
    {
        if (_playerById(angelId) is not { RequestZoneChange: { } requestZoneChange })
        {
            Console.WriteLine($"{_logTag} [angel] warp {where} for 0x{angelId:X}: not on this server");
            return;
        }
        Console.WriteLine($"{_logTag} [angel] 0x{angelId:X} warps {where}: zone {zone} ({x},{y})");
        await requestZoneChange(new ZoneChangeRequest(zone, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero, (x, y)), ct).ConfigureAwait(false);
    }
}
