using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class GmBlockHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.CcmdBlock,
        InterServerOpcode.CcmdUnblock,
    };

    private readonly IGmBlockStore _store;

    public GmBlockHandler(IGmBlockStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        InterServerFrame? reply;
        try
        {
            reply = GmBlockMessages.Answer(_store, frame);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"gm block 0x{(ushort)frame.Opcode:X4} char 0x{frame.Header.CharUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            reply = GmBlockMessages.Cancelled(frame);
        }
        if (reply is null)
        {
            ctx.Log($"gm block 0x{(ushort)frame.Opcode:X4} char 0x{frame.Header.CharUid:X16}: {frame.Body.Length} B is not a valid request");
            return;
        }
        ctx.Log($"gm block 0x{(ushort)frame.Opcode:X4} char 0x{frame.Header.CharUid:X16} -> 0x{(ushort)reply.Opcode:X4}");
        await session.SendAsync(reply, ct).ConfigureAwait(false);
    }
}
