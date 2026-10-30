using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class ShadowHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.McmdShadowResultInsert,
        InterServerOpcode.McmdShadowAreaReward,
    };

    private readonly IShadowBonusStore _store;

    public ShadowHandler(IShadowBonusStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        if (frame.Opcode == InterServerOpcode.McmdShadowResultInsert)
        {
            InsertResult(frame, ctx);
            return;
        }
        if (ShadowMessages.DecodeAreaReward(frame.Body) is null)
        {
            ctx.Log($"shadow area reward: {frame.Body.Length} B is not a valid request");
            return;
        }
        var targets = ServerIdentity.GameServers.Select(game => game.ServerIndex).Where(index => ctx.Host.TryGetSibling(index, out _));
        foreach (var relay in ShadowMessages.Relay(frame, targets))
        {
            if (ctx.Host.TryGetSibling(relay.Header.DestServerIndex, out var target)) await target.SendAsync(relay, ct).ConfigureAwait(false);
        }
    }

    private void InsertResult(InterServerFrame frame, MasterContext ctx)
    {
        try
        {
            if (!ShadowMessages.StoreResult(_store, frame))
            {
                ctx.Log($"shadow result: {frame.Body.Length} B is not a valid request");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"shadow result insert failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
