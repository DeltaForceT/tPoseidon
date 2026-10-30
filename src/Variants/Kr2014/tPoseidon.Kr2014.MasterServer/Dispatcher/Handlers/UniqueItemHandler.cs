using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class UniqueItemHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.TpUniqueItemPickup,
        InterServerOpcode.IcmdUniqueItemWithdraw,
        InterServerOpcode.CcmdSaveBossKillInfo,
        InterServerOpcode.McmdUniqueItemCount,
        InterServerOpcode.CcmdGetLatestBossKill,
    };

    public const uint SharedPoolKey = 0;

    private readonly IUniqueItemStore _store;

    public UniqueItemHandler(IUniqueItemStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var charUid = frame.Header.CharUid;
        if (frame.Opcode == InterServerOpcode.McmdUniqueItemCount)
        {
            await CountAsync(session, frame, ctx, ct).ConfigureAwait(false);
            return;
        }
        if (frame.Opcode == InterServerOpcode.CcmdGetLatestBossKill)
        {
            await LatestBossKillsAsync(session, frame, ctx, ct).ConfigureAwait(false);
            return;
        }
        if (frame.Opcode == InterServerOpcode.CcmdSaveBossKillInfo)
        {
            try
            {
                var (killer, monsterId) = UniqueItemMessages.DecodeBossKill(frame.Body);
                var credited = _store.BossKilled(killer, monsterId, DateTimeOffset.UtcNow);
                ctx.Log($"boss {monsterId} killed by 0x{killer:X16}: boss seal {(credited ? "credited" : "not credited")}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.Log($"boss kill char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            }
            return;
        }
        if (frame.Opcode == InterServerOpcode.TpUniqueItemPickup)
        {
            try
            {
                var (itemUid, itemIndex) = UniqueItemMessages.DecodePickup(frame.Body);
                if (_store.PickedUp(charUid, itemUid, itemIndex, DateTimeOffset.UtcNow) is { } pickup)
                {
                    ctx.Log($"unique item {itemIndex} (type {pickup.UniqueType}) picked up by 0x{charUid:X16}{(pickup.DutyEnd is { } end ? $", duty until {end:u}" : string.Empty)}");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.Log($"unique pickup char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            }
            return;
        }
        InterServerOpcode opcode;
        byte[] body;
        try
        {
            var request = UniqueItemMessages.DecodeWithdraw(frame.Body);
            var taken = _store.DepriveSingle(request.CharUid, request.Items);
            (opcode, body) = taken.Count > 0
                ? (InterServerOpcode.IcmdUniqueItemWithdrawAckOk, UniqueItemMessages.EncodeWithdrawn(taken))
                : (InterServerOpcode.IcmdUniqueItemWithdrawAckCancel, BitConverter.GetBytes(UniqueItemRules.NotHeld));
            ctx.Log($"unique withdraw 0x{request.CharUid:X16}: {taken.Count} of {request.Items.Count} items deprived");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"unique withdraw char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            (opcode, body) = (InterServerOpcode.IcmdUniqueItemWithdrawAckCancel, Array.Empty<byte>());
        }
        await session.SendAsync(new InterServerFrame
        {
            Opcode = opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = charUid,
                RequestId = frame.Header.RequestId,
            },
            Body = body,
        }, ct).ConfigureAwait(false);
    }

    private async Task LatestBossKillsAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        InterServerOpcode opcode;
        byte[] body;
        try
        {
            var kills = _store.LatestBossKills(frame.Header.CharUid);
            (opcode, body) = (InterServerOpcode.CcmdGetLatestBossKillAckOk, UniqueItemMessages.EncodeLatestBossKills(kills));
            ctx.Log($"latest boss kills of 0x{frame.Header.CharUid:X16}: {kills.Count}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"latest boss kills of 0x{frame.Header.CharUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            (opcode, body) = (InterServerOpcode.CcmdGetLatestBossKillAckCancel, Array.Empty<byte>());
        }
        await session.SendAsync(new InterServerFrame
        {
            Opcode = opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = frame.Header.CharUid,
                RequestId = frame.Header.RequestId,
            },
            Body = body,
        }, ct).ConfigureAwait(false);
    }

    private async Task CountAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        bool issued;
        uint key;
        uint units;
        try
        {
            (key, units) = UniqueItemMessages.DecodeCount(frame.Body);
            if (key == SharedPoolKey)
            {
                ctx.Log($"unique count {units} for the shared pool: item choice not determined, not counted");
                return;
            }
            issued = _store.AddCount((int)key, (int)units, DateTimeOffset.UtcNow);
            ctx.Log($"unique item {key}: {units} counted{(issued ? ", one issued" : string.Empty)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"unique count from server {frame.Header.SourceServerIndex}: store threw {ex.GetType().Name}: {ex.Message}");
            return;
        }
        if (!issued) return;
        await session.SendAsync(new InterServerFrame
        {
            Opcode = InterServerOpcode.McmdUniqueItemIssue,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = frame.Header.CharUid,
                RequestId = 0,
            },
            Body = UniqueItemMessages.EncodeIssue(key, [(int)key]),
        }, ct).ConfigureAwait(false);
    }
}
