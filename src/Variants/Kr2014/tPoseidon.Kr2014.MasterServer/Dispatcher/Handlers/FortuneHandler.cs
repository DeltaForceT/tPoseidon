using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class FortuneHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.TpFortuneLoad,
        InterServerOpcode.McmdFortuneUpdate,
    };

    private readonly IFortuneStore _store;

    public FortuneHandler(IFortuneStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var charUid = frame.Header.CharUid;
        if (frame.Opcode == InterServerOpcode.McmdFortuneUpdate)
        {
            try
            {
                if (FortuneMessages.DecodeInfo(charUid, frame.Body) is { } info) _store.Update(info);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.Log($"fortune update char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            }
            return;
        }
        (InterServerOpcode Opcode, byte[] Body) reply;
        try
        {
            reply = (InterServerOpcode.TpFortuneLoadAckOk, FortuneMessages.EncodeInfo(_store.Find(charUid)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"fortune load char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            reply = (InterServerOpcode.TpFortuneLoadAckCancel, []);
        }
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
}
