using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class PrepareEnteringZoneHandler : IInterServerHandler
{
    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        PrepareEnteringZone prepare;
        try
        {
            prepare = PrepareEnteringZone.DecodeBody(frame.Body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"bad McmdPrepareEnteringZone body from idx={frame.Header.SourceServerIndex}: {ex.Message}");
            return;
        }

        var target = prepare.FromZone == 0
            ? ServerIdentity.Game1
            : ctx.ResolveGameServer(prepare.ToZone);
        if (!ctx.Host.TryGetSibling(target.ServerIndex, out var gameSession))
        {
            if (target.ServerIndex == ServerIdentity.Game2.ServerIndex &&
                ctx.Host.TryGetSibling(ServerIdentity.Game1.ServerIndex, out gameSession))
            {
                ctx.Log($"zone {prepare.ToZone} -> {target.Name} not separately registered; routing to gserver1 (collapsed deployment serves all zones)");
                target = ServerIdentity.Game1;
            }
            else
            {
                ctx.Log($"prepare-enter zone {prepare.ToZone}: target {target.Name} not registered; cannot broker char 0x{prepare.CharUid:X16}");
                return;
            }
        }

        var routed = new InterServerFrame
        {
            Opcode = InterServerOpcode.McmdPrepareEnteringZone,
            Header = new InterServerHeader
            {
                SourceServerIndex = frame.Header.SourceServerIndex,
                DestServerIndex = target.ServerIndex,
                CharUid = prepare.CharUid,
                RequestId = frame.Header.RequestId,
            },
            Body = frame.Body,
        };
        await gameSession.SendAsync(routed, ct).ConfigureAwait(false);
        ctx.Log($"routed PrepareEnteringZone char 0x{prepare.CharUid:X16} zone {prepare.FromZone}->{prepare.ToZone} to {target.Name}");
    }
}
