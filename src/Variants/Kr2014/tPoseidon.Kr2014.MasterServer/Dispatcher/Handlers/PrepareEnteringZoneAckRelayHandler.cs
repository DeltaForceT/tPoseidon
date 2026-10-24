using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class PrepareEnteringZoneAckRelayHandler : IInterServerHandler
{
    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        if (!ctx.Host.TryGetSibling(frame.Header.DestServerIndex, out var loginSession))
        {
            ctx.Log($"prepare-ack 0x{(ushort)frame.Opcode:X4}: dest idx={frame.Header.DestServerIndex} not registered; dropping");
            return;
        }
        await loginSession.SendAsync(frame, ct).ConfigureAwait(false);
        ctx.Log($"relayed {frame.Opcode} char 0x{frame.Header.CharUid:X16} -> idx={frame.Header.DestServerIndex}");
    }
}
