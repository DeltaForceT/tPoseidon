using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class ZoneUserCountHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.McmdRequestZoneUserCount,
        InterServerOpcode.McmdRequestZoneUserCountAckOk,
        InterServerOpcode.McmdRequestZoneUserCountAckCancel,
    };

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        if (frame.Opcode != InterServerOpcode.McmdRequestZoneUserCount)
        {
            if (ctx.Host.TryGetSibling(frame.Header.DestServerIndex, out var origin)) await origin.SendAsync(frame, ct).ConfigureAwait(false);
            else ctx.Log($"zone user count answer for idx={frame.Header.DestServerIndex}: not registered; dropped");
            return;
        }
        ushort zone;
        try
        {
            zone = ZoneUserCountMessages.DecodeRequest(frame.Body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"bad zone user count request from idx={frame.Header.SourceServerIndex}: {ex.Message}");
            return;
        }
        var owner = ctx.ResolveGameServer(zone);
        if (!ctx.Host.TryGetSibling(owner.ServerIndex, out var ownerSession)
            && !ctx.Host.TryGetSibling(ServerIdentity.Game1.ServerIndex, out ownerSession))
        {
            ctx.Log($"zone user count of zone {zone}: {owner.Name} not registered");
            await session.SendAsync(new InterServerFrame
            {
                Opcode = InterServerOpcode.McmdRequestZoneUserCountAckCancel,
                Header = new InterServerHeader
                {
                    SourceServerIndex = 0,
                    DestServerIndex = frame.Header.SourceServerIndex,
                    CharUid = frame.Header.CharUid,
                    RequestId = frame.Header.RequestId,
                },
                Body = Array.Empty<byte>(),
            }, ct).ConfigureAwait(false);
            return;
        }
        await ownerSession.SendAsync(new InterServerFrame
        {
            Opcode = InterServerOpcode.McmdRequestZoneUserCount,
            Header = new InterServerHeader
            {
                SourceServerIndex = frame.Header.SourceServerIndex,
                DestServerIndex = owner.ServerIndex,
                CharUid = frame.Header.CharUid,
                RequestId = 0,
            },
            Body = ZoneUserCountMessages.EncodeRouted(zone, frame.Header.RequestId),
        }, ct).ConfigureAwait(false);
        ctx.Log($"zone user count of zone {zone} for idx={frame.Header.SourceServerIndex} routed to {owner.Name}");
    }
}
