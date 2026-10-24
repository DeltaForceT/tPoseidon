using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher;

public sealed class InterServerDispatcher
{
    private readonly Dictionary<InterServerOpcode, IInterServerHandler> _handlers = new();
    private readonly MasterContext _ctx;

    public InterServerDispatcher(MasterContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _ctx = ctx;
    }

    public InterServerDispatcher Register(InterServerOpcode opcode, IInterServerHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (_handlers.TryGetValue(opcode, out var existing))
        {
            throw new InvalidOperationException($"opcode 0x{(ushort)opcode:X4} already registered to {existing.GetType().Name}");
        }
        _handlers[opcode] = handler;
        return this;
    }

    public InterServerDispatcher Register(IEnumerable<InterServerOpcode> opcodes, IInterServerHandler handler)
    {
        foreach (var op in opcodes)
        {
            Register(op, handler);
        }
        return this;
    }

    public async Task HandleAsync(MasterServerSession session, InterServerFrame frame, CancellationToken ct)
    {
        if (_handlers.TryGetValue(frame.Opcode, out var handler))
        {
            await handler.HandleAsync(session, frame, _ctx, ct).ConfigureAwait(false);
            return;
        }

        if (BrokerAckFamily.TryResolveOkReply(frame.Opcode, out var okReply))
        {
            var ackFrame = new InterServerFrame
            {
                Opcode = okReply,
                Header = new InterServerHeader
                {
                    SourceServerIndex = 0,
                    DestServerIndex = frame.Header.SourceServerIndex,
                    CharUid = frame.Header.CharUid,
                    RequestId = frame.Header.RequestId,
                },
                Body = frame.Body,
            };
            await session.SendAsync(ackFrame, ct).ConfigureAwait(false);
            _ctx.Log($"brokered {frame.Opcode} char 0x{frame.Header.CharUid:X16} -> {okReply}");
            return;
        }

        _ctx.Log($"unhandled inter-server frame 0x{(ushort)frame.Opcode:X4} from idx={frame.Header.SourceServerIndex} (not yet brokered)");
    }
}
