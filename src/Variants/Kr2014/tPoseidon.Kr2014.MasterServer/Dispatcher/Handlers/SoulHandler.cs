using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class SoulHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.TpSoulLoad,
        InterServerOpcode.TpSoulSave,
        InterServerOpcode.TpSoulGuardianSave,
    };

    private readonly ISoulStore _store;

    public SoulHandler(ISoulStore store)
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
                InterServerOpcode.TpSoulLoad => (InterServerOpcode.TpSoulLoadAckOk, SoulMessages.EncodeAccount(_store.Load(charUid))),
                InterServerOpcode.TpSoulSave => SaveSoul(charUid, SoulMessages.DecodeSoul(frame.Body)),
                _ => SaveGuardian(charUid, SoulMessages.DecodeGuardian(frame.Body), ctx),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"soul 0x{(ushort)frame.Opcode:X4} char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            reply = frame.Opcode == InterServerOpcode.TpSoulLoad ? (InterServerOpcode.TpSoulLoadAckCancel, []) : null;
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

    private (InterServerOpcode, byte[])? SaveSoul(ulong charUid, uint soul)
    {
        _store.SaveSoul(charUid, soul);
        return null;
    }

    private (InterServerOpcode, byte[])? SaveGuardian(ulong charUid, GuardianSoulRow row, MasterContext ctx)
    {
        if (!_store.SaveGuardian(charUid, row)) ctx.Log($"soul guardian save char 0x{charUid:X16} guardian {row.GuardianUid}: not owned");
        return null;
    }
}
