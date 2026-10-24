using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class ChangeOccupationHandler : IInterServerHandler
{
    private readonly IOccupationStore _store;

    public ChangeOccupationHandler(IOccupationStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var characterId = frame.Header.CharUid;
        if (frame.Opcode == InterServerOpcode.CcmdUpdateOccupationIndex)
        {
            await SetJobIndexAsync(session, frame, ctx, characterId, ct).ConfigureAwait(false);
            return;
        }
        if (frame.Opcode == InterServerOpcode.TpJobQuestState)
        {
            await SetQuestStateAsync(session, frame, ctx, characterId, ct).ConfigureAwait(false);
            return;
        }
        var change = OccupationMessages.DecodeChange(frame.Body);
        bool changed;
        if (change.Tier is not (OccupationRules.SecondJobTier or OccupationRules.ThirdJobTier))
        {
            ctx.Log($"job-change char 0x{characterId:X16}: unknown job index {change.Tier}");
            changed = false;
        }
        else
        {
            try
            {
                changed = _store.ChangeOccupation(characterId, change.Tier, change.Type, change.Job, change.Hair);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.Log($"job-change char 0x{characterId:X16}: store threw {ex.GetType().Name}: {ex.Message}");
                changed = false;
            }
        }
        await session.SendAsync(new InterServerFrame
        {
            Opcode = changed ? InterServerOpcode.CcmdChangeOccupationAckOk : InterServerOpcode.CcmdChangeOccupationAckCancel,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = characterId,
                RequestId = frame.Header.RequestId,
            },
            Body = changed
                ? OccupationMessages.EncodeChanged(new OccupationChanged(change.Type, change.Job, change.Tier))
                : Array.Empty<byte>(),
        }, ct).ConfigureAwait(false);
        ctx.Log($"job-change char 0x{characterId:X16}: type {change.Type}, job {change.Job}, index {change.Tier} -> {(changed ? "changed" : "refused")}");
    }

    private async Task SetJobIndexAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, ulong characterId, CancellationToken ct)
    {
        var index = frame.Body.Length > 0 ? frame.Body[0] : (byte)0;
        bool stored;
        try
        {
            stored = _store.SetJobState(characterId, index);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"occupation index char 0x{characterId:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            stored = false;
        }
        await session.SendAsync(new InterServerFrame
        {
            Opcode = stored ? InterServerOpcode.CcmdUpdateOccupationIndexAckOk : InterServerOpcode.CcmdUpdateOccupationIndexAckCancel,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = characterId,
                RequestId = frame.Header.RequestId,
            },
            Body = new[] { index },
        }, ct).ConfigureAwait(false);
        ctx.Log($"occupation index char 0x{characterId:X16}: {index} -> {(stored ? "stored" : "refused")}");
    }

    private async Task SetQuestStateAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, ulong characterId, CancellationToken ct)
    {
        var state = frame.Body.Length > 0 ? frame.Body[0] : (byte)0;
        bool stored;
        try
        {
            stored = _store.SetChangeJobQuestState(characterId, state);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"job-quest state char 0x{characterId:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            stored = false;
        }
        await session.SendAsync(new InterServerFrame
        {
            Opcode = stored ? InterServerOpcode.TpJobQuestStateAckOk : InterServerOpcode.TpJobQuestStateAckCancel,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = characterId,
                RequestId = frame.Header.RequestId,
            },
            Body = Array.Empty<byte>(),
        }, ct).ConfigureAwait(false);
        ctx.Log($"job-quest state char 0x{characterId:X16}: {state} -> {(stored ? "stored" : "refused (third job held)")}");
    }
}
