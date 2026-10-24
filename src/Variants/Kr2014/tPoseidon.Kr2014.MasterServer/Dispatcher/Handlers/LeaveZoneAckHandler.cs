using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class LeaveZoneAckHandler : IInterServerHandler
{
    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        LeaveZoneRequest leave;
        try
        {
            leave = LeaveZoneRequest.DecodeBody(frame.Body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"bad LeaveZone request body from idx={frame.Header.SourceServerIndex}: {ex.Message}");
            return;
        }

        var destOwner = ctx.ResolveGameServer(leave.ToZone);
        var leaveAck = new LeaveZoneAck
        {
            CharUid = leave.CharUid,
            ToZone = leave.ToZone,
            StartX = leave.StartX,
            StartY = leave.StartY,
            ChangeType = leave.ChangeType,
            DestServerIp = 0,
            DestServerPort = destOwner.GameListenPort,
        };
        var ackFrame = new InterServerFrame
        {
            Opcode = InterServerOpcode.McmdLeaveZoneAckOk,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = leave.CharUid,
                RequestId = frame.Header.RequestId,
            },
            Body = leaveAck.EncodeBody(),
        };
        await session.SendAsync(ackFrame, ct).ConfigureAwait(false);
        ctx.Log($"LeaveZone char 0x{leave.CharUid:X16} -> zone {leave.ToZone}; sent McmdLeaveZoneAckOk");

        var destTarget = ctx.ResolveGameServer(leave.ToZone);
        ushort destIdx = destTarget.ServerIndex;
        if (!ctx.Host.TryGetSibling(destIdx, out var destSession))
        {
            if (destIdx == ServerIdentity.Game2.ServerIndex &&
                ctx.Host.TryGetSibling(ServerIdentity.Game1.ServerIndex, out destSession))
            {
                destIdx = ServerIdentity.Game1.ServerIndex;
            }
            else
            {
                destSession = null;
            }
        }

        if (destSession is not null)
        {
            var prepare = new PrepareEnteringZone
            {
                FromZone = leave.FromZone,
                ToZone = leave.ToZone,
                ZoneType = 0,
                ChangeZoneType = leave.ChangeType,
                Spare = 0,
                CharUid = leave.CharUid,
                Token = leave.Token,
                StartX = leave.StartX,
                StartY = leave.StartY,
            };
            var prepFrame = new InterServerFrame
            {
                Opcode = InterServerOpcode.McmdPrepareEnteringZone,
                Header = new InterServerHeader
                {
                    SourceServerIndex = 0,
                    DestServerIndex = destIdx,
                    CharUid = leave.CharUid,
                    RequestId = frame.Header.RequestId,
                },
                Body = prepare.EncodeBody(),
            };
            await destSession.SendAsync(prepFrame, ct).ConfigureAwait(false);
            ctx.Log($"warp char 0x{leave.CharUid:X16} -> pre-registered brokered join on idx={destIdx} for zone {leave.ToZone}");
        }
    }
}
