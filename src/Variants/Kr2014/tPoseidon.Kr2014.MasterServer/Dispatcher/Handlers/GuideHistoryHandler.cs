using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class GuideHistoryHandler : IInterServerHandler
{
    private readonly IGuideHistoryStore _store;

    public GuideHistoryHandler(IGuideHistoryStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        var charUid = frame.Header.CharUid;
        if (charUid == 0)
        {
            await ReplyCancelAsync(session, frame, frame.Opcode, ct).ConfigureAwait(false);
            ctx.Log($"guide-history {frame.Opcode}: invalid chruid=0 from idx={frame.Header.SourceServerIndex}; replied CANCEL");
            return;
        }

        switch (frame.Opcode)
        {
            case InterServerOpcode.TpGuideHistoryLoad:
                await HandleLoadAsync(session, frame, charUid, ctx, ct).ConfigureAwait(false);
                return;
            case InterServerOpcode.TpGuideHistoryUpsertSystem:
                await HandleUpsertSystemAsync(session, frame, charUid, ctx, ct).ConfigureAwait(false);
                return;
            case InterServerOpcode.TpGuideHistoryUpsertTutorial:
                await HandleUpsertTutorialAsync(session, frame, charUid, ctx, ct).ConfigureAwait(false);
                return;
            default:
                ctx.Log($"guide-history: unexpected opcode 0x{(ushort)frame.Opcode:X4}");
                return;
        }
    }

    private async ValueTask HandleLoadAsync(MasterServerSession session, InterServerFrame frame, ulong charUid, MasterContext ctx, CancellationToken ct)
    {
        GuideHistory? history;
        try
        {
            history = _store.GetForCharacter(charUid);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"guide-history Load char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}; replying CANCEL");
            await ReplyCancelAsync(session, frame, InterServerOpcode.TpGuideHistoryLoadAckCancel, ct).ConfigureAwait(false);
            return;
        }

        var ack = new GuideHistoryLoadAckOk
        {
            SystemGuide = history?.SystemGuide ?? string.Empty,
            TutorialGuide = history?.TutorialGuide ?? 0UL,
        };
        await ReplyAsync(session, frame, InterServerOpcode.TpGuideHistoryLoadAckOk, ack.EncodeBody(), ct).ConfigureAwait(false);
        ctx.Log($"guide-history Load char 0x{charUid:X16}: replied OK ({(history is null ? "default" : "from db")})");
    }

    private async ValueTask HandleUpsertSystemAsync(MasterServerSession session, InterServerFrame frame, ulong charUid, MasterContext ctx, CancellationToken ct)
    {
        GuideHistoryUpsertSystemRequest req;
        try
        {
            req = GuideHistoryUpsertSystemRequest.DecodeBody(frame.Body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"guide-history UpsertSystem char 0x{charUid:X16}: bad body ({ex.Message}); replying CANCEL");
            await ReplyCancelAsync(session, frame, InterServerOpcode.TpGuideHistoryUpsertSystemAckCancel, ct).ConfigureAwait(false);
            return;
        }

        try
        {
            _store.UpsertSystemGuide(charUid, req.SystemGuide);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"guide-history UpsertSystem char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}; replying CANCEL");
            await ReplyCancelAsync(session, frame, InterServerOpcode.TpGuideHistoryUpsertSystemAckCancel, ct).ConfigureAwait(false);
            return;
        }

        await ReplyAsync(session, frame, InterServerOpcode.TpGuideHistoryUpsertSystemAckOk, Array.Empty<byte>(), ct).ConfigureAwait(false);
        ctx.Log($"guide-history UpsertSystem char 0x{charUid:X16}: replied OK ({req.SystemGuide.Length} chars)");
    }

    private async ValueTask HandleUpsertTutorialAsync(MasterServerSession session, InterServerFrame frame, ulong charUid, MasterContext ctx, CancellationToken ct)
    {
        GuideHistoryUpsertTutorialRequest req;
        try
        {
            req = GuideHistoryUpsertTutorialRequest.DecodeBody(frame.Body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"guide-history UpsertTutorial char 0x{charUid:X16}: bad body ({ex.Message}); replying CANCEL");
            await ReplyCancelAsync(session, frame, InterServerOpcode.TpGuideHistoryUpsertTutorialAckCancel, ct).ConfigureAwait(false);
            return;
        }

        try
        {
            _store.UpsertTutorialGuide(charUid, req.TutorialGuide);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"guide-history UpsertTutorial char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}; replying CANCEL");
            await ReplyCancelAsync(session, frame, InterServerOpcode.TpGuideHistoryUpsertTutorialAckCancel, ct).ConfigureAwait(false);
            return;
        }

        await ReplyAsync(session, frame, InterServerOpcode.TpGuideHistoryUpsertTutorialAckOk, Array.Empty<byte>(), ct).ConfigureAwait(false);
        ctx.Log($"guide-history UpsertTutorial char 0x{charUid:X16}: replied OK (value=0x{req.TutorialGuide:X16})");
    }

    private static Task ReplyAsync(MasterServerSession session, InterServerFrame request, InterServerOpcode opcode, byte[] body, CancellationToken ct)
    {
        var reply = new InterServerFrame
        {
            Opcode = opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = request.Header.SourceServerIndex,
                CharUid = request.Header.CharUid,
                RequestId = request.Header.RequestId,
            },
            Body = body,
        };
        return session.SendAsync(reply, ct);
    }

    private static Task ReplyCancelAsync(MasterServerSession session, InterServerFrame request, InterServerOpcode opcode, CancellationToken ct)
    {
        var cancelOpcode = opcode switch
        {
            InterServerOpcode.TpGuideHistoryLoad => InterServerOpcode.TpGuideHistoryLoadAckCancel,
            InterServerOpcode.TpGuideHistoryUpsertSystem => InterServerOpcode.TpGuideHistoryUpsertSystemAckCancel,
            InterServerOpcode.TpGuideHistoryUpsertTutorial => InterServerOpcode.TpGuideHistoryUpsertTutorialAckCancel,
            _ => opcode,
        };
        return ReplyAsync(session, request, cancelOpcode, Array.Empty<byte>(), ct);
    }
}
