using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class GuardianHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.TpGuardianLoad,
        InterServerOpcode.CcmdGuardianEquip,
        InterServerOpcode.CcmdGuardianOrder,
        InterServerOpcode.CcmdGuardianStatus,
        InterServerOpcode.CcmdGuardianState,
    };

    private readonly IGuardianStateStore _store;

    public GuardianHandler(IGuardianStateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var charUid = frame.Header.CharUid;
        (InterServerOpcode Opcode, byte[] Body)? reply;
        try
        {
            reply = frame.Opcode switch
            {
                InterServerOpcode.TpGuardianLoad => Load(charUid),
                InterServerOpcode.CcmdGuardianEquip => Equip(charUid, GuardianMessages.DecodeUid(frame.Body), ctx),
                InterServerOpcode.CcmdGuardianOrder => Order(charUid, GuardianMessages.DecodeOrder(frame.Body), ctx),
                InterServerOpcode.CcmdGuardianStatus => Status(charUid, GuardianMessages.DecodeStatus(frame.Body), ctx),
                InterServerOpcode.CcmdGuardianState => State(GuardianMessages.DecodeStateUpdate(frame.Body)),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"guardian 0x{(ushort)frame.Opcode:X4} char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            reply = CancelFor(frame.Opcode) is { } cancel ? (cancel, []) : null;
        }
        if (reply is not { } answer) return;
        await session.SendAsync(new InterServerFrame
        {
            Opcode = answer.Opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = charUid,
                RequestId = frame.Header.RequestId,
            },
            Body = answer.Body,
        }, ct).ConfigureAwait(false);
    }

    private (InterServerOpcode, byte[]) Load(ulong charUid) =>
        (InterServerOpcode.TpGuardianLoadAckOk, GuardianMessages.EncodeLogin(_store.LoadLogin(charUid)));

    private (InterServerOpcode, byte[]) Equip(ulong charUid, ulong guardianUid, MasterContext ctx)
    {
        var state = _store.Equip(charUid, guardianUid) ? _store.Load(charUid, guardianUid) : null;
        ctx.Log($"guardian equip char 0x{charUid:X16} guardian {guardianUid}: {(state is null ? "refused" : "ok")}");
        return state is null
            ? (InterServerOpcode.CcmdGuardianEquipAckCancel, [])
            : (InterServerOpcode.CcmdGuardianEquipAckOk, GuardianMessages.EncodeState(state));
    }

    private (InterServerOpcode, byte[]) Order(ulong charUid, GuardianOrderChange change, MasterContext ctx)
    {
        var ok = _store.Load(charUid, change.GuardianUid) is not null && _store.SetOrder(change.GuardianUid, change.SkillId, change.Slot, change.Remove);
        ctx.Log($"guardian order char 0x{charUid:X16} guardian {change.GuardianUid}: skill {change.SkillId} slot {change.Slot} {(change.Remove ? "remove" : "add")} {(ok ? "ok" : "refused")}");
        return ok
            ? (InterServerOpcode.CcmdGuardianOrderAckOk, GuardianMessages.EncodeOrder(change))
            : (InterServerOpcode.CcmdGuardianOrderAckCancel, []);
    }

    private (InterServerOpcode, byte[])? Status(ulong charUid, GuardianStatusUpdate update, MasterContext ctx)
    {
        if (_store.Load(charUid, update.GuardianUid) is null || !_store.SaveLevelUp(update.GuardianUid, update.Level, update.StatPoints, update.SkillPoint))
        {
            ctx.Log($"guardian status char 0x{charUid:X16} guardian {update.GuardianUid}: not saved");
        }
        return null;
    }

    private (InterServerOpcode, byte[])? State(GuardianStateUpdate update)
    {
        _store.SaveExp(update.GuardianUid, update.Exp, update.Gauge);
        return null;
    }

    private static InterServerOpcode? CancelFor(InterServerOpcode request) => request switch
    {
        InterServerOpcode.TpGuardianLoad => InterServerOpcode.TpGuardianLoadAckCancel,
        InterServerOpcode.CcmdGuardianEquip => InterServerOpcode.CcmdGuardianEquipAckCancel,
        InterServerOpcode.CcmdGuardianOrder => InterServerOpcode.CcmdGuardianOrderAckCancel,
        _ => null,
    };
}
