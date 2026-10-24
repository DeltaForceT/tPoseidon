using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class EnterZoneDoneAckHandler : IInterServerHandler
{
    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        EnterZoneDone done;
        try
        {
            done = EnterZoneDone.DecodeBody(frame.Body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"bad EnterZoneDoneAck body from idx={frame.Header.SourceServerIndex}: {ex.Message}");
            return;
        }

        var ackOk = new InterServerFrame
        {
            Opcode = InterServerOpcode.McmdEnterZoneAckOk,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = done.CharUid,
                RequestId = frame.Header.RequestId,
            },
            Body = Array.Empty<byte>(),
        };
        await session.SendAsync(ackOk, ct).ConfigureAwait(false);
        ctx.Log($"EnterZoneDone char 0x{done.CharUid:X16} zone {done.ZoneId}; sent McmdEnterZoneAckOk");
    }
}
