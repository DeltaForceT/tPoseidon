using tPoseidon.Core.Common;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class MegaphoneHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.CcmdMegaphone,
        InterServerOpcode.SysCmdMegaphoneMessageAckOk,
        InterServerOpcode.SysCmdCheckValidityAckOk,
    };

    private readonly MegaphonePacer _pacer;

    public MegaphoneHandler(MegaphonePacer pacer)
    {
        _pacer = pacer ?? throw new ArgumentNullException(nameof(pacer));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        if (frame.Opcode == InterServerOpcode.SysCmdCheckValidityAckOk)
        {
            _pacer.ValidityAnswered(frame.Header.SourceServerIndex);
            return;
        }
        if (frame.Opcode == InterServerOpcode.SysCmdMegaphoneMessageAckOk)
        {
            _pacer.Acknowledge(frame.Header.SourceServerIndex, MegaphoneMessages.DecodeAck(frame.Body));
            return;
        }
        var request = MegaphoneMessages.DecodeRequest(frame.Body, ClientText.Encoding);
        var result = _pacer.Enqueue(frame.Header.CharUid, request.Message, request.Refund);
        await session.SendAsync(new InterServerFrame
        {
            Opcode = result.Accepted ? InterServerOpcode.CcmdMegaphoneAckOk : InterServerOpcode.CcmdMegaphoneAckCancel,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = frame.Header.CharUid,
                RequestId = frame.Header.RequestId,
            },
            Body = MegaphoneMessages.EncodeValue(result.Value),
        }, ct).ConfigureAwait(false);
    }
}
