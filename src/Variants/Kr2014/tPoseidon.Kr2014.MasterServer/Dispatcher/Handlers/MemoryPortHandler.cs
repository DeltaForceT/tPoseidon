using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class MemoryPortHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.McmdMemoryPortOpen,
        InterServerOpcode.McmdMemoryPortUse,
        InterServerOpcode.McmdMemoryPortAdd,
        InterServerOpcode.McmdMemoryPortDelete,
    };

    private readonly IMemoryPortStore _store;

    public MemoryPortHandler(IMemoryPortStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var charUid = frame.Header.CharUid;
        (InterServerOpcode Opcode, byte[] Body) reply;
        try
        {
            reply = frame.Opcode switch
            {
                InterServerOpcode.McmdMemoryPortOpen => Open(charUid, MemoryPortMessages.DecodeType(frame.Body)),
                InterServerOpcode.McmdMemoryPortUse => Use(charUid, MemoryPortMessages.DecodeSlotRequest(frame.Body)),
                InterServerOpcode.McmdMemoryPortAdd => Add(MemoryPortMessages.DecodeSlot(charUid, frame.Body)),
                _ => Delete(charUid, MemoryPortMessages.DecodeSlotRequest(frame.Body)),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"memory port 0x{(ushort)frame.Opcode:X4} char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            reply = (CancelFor(frame.Opcode), frame.Opcode is InterServerOpcode.McmdMemoryPortOpen or InterServerOpcode.McmdMemoryPortUse
                ? MemoryPortMessages.EncodeCode(MemoryPortRules.UndeterminedErrorCode)
                : []);
        }
        ctx.Log($"memory port 0x{(ushort)frame.Opcode:X4} char 0x{charUid:X16} -> 0x{(ushort)reply.Opcode:X4}");
        await session.SendAsync(new InterServerFrame
        {
            Opcode = reply.Opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = charUid,
                RequestId = frame.Header.RequestId,
            },
            Body = reply.Body,
        }, ct).ConfigureAwait(false);
    }

    private (InterServerOpcode, byte[]) Open(ulong charUid, byte portType) =>
        (InterServerOpcode.McmdMemoryPortOpenAckOk, MemoryPortMessages.EncodeSlots(_store.GetForCharacterByType(charUid, portType)));

    private (InterServerOpcode, byte[]) Use(ulong charUid, MemoryPortSlotRequest request) =>
        request.Slot <= byte.MaxValue && _store.Find(charUid, request.PortType, (byte)request.Slot) is { } slot
            ? (InterServerOpcode.McmdMemoryPortUseAckOk, MemoryPortMessages.EncodeSlot(slot))
            : (InterServerOpcode.McmdMemoryPortUseAckCancel, MemoryPortMessages.EncodeCode(MemoryPortRules.NoSuchSlotError));

    private (InterServerOpcode, byte[]) Add(MemoryPort slot) =>
        _store.TryInsert(slot)
            ? (InterServerOpcode.McmdMemoryPortAddAckOk, [])
            : (InterServerOpcode.McmdMemoryPortAddAckCancel, []);

    private (InterServerOpcode, byte[]) Delete(ulong charUid, MemoryPortSlotRequest request) =>
        request.Slot <= byte.MaxValue && _store.Delete(charUid, request.PortType, (byte)request.Slot)
            ? (InterServerOpcode.McmdMemoryPortDeleteAckOk, [])
            : (InterServerOpcode.McmdMemoryPortDeleteAckCancel, []);

    private static InterServerOpcode CancelFor(InterServerOpcode request) => request switch
    {
        InterServerOpcode.McmdMemoryPortOpen => InterServerOpcode.McmdMemoryPortOpenAckCancel,
        InterServerOpcode.McmdMemoryPortUse => InterServerOpcode.McmdMemoryPortUseAckCancel,
        InterServerOpcode.McmdMemoryPortAdd => InterServerOpcode.McmdMemoryPortAddAckCancel,
        _ => InterServerOpcode.McmdMemoryPortDeleteAckCancel,
    };
}
