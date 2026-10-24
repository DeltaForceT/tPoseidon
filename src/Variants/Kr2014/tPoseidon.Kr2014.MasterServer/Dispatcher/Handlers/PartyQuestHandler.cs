using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class PartyQuestHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.QcmdAcceptPartyQuest,
        InterServerOpcode.QcmdInsertPartyQuest,
        InterServerOpcode.QcmdSelectPartyQuest,
        InterServerOpcode.QcmdCompletePartyQuest,
    };

    private readonly IPartyQuestStore _store;

    public PartyQuestHandler(IPartyQuestStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var (ok, cancel) = frame.Opcode switch
        {
            InterServerOpcode.QcmdAcceptPartyQuest => (InterServerOpcode.QcmdAcceptPartyQuestAckOk, InterServerOpcode.QcmdAcceptPartyQuestAckCancel),
            InterServerOpcode.QcmdInsertPartyQuest => (InterServerOpcode.QcmdInsertPartyQuestAckOk, InterServerOpcode.QcmdInsertPartyQuestAckCancel),
            InterServerOpcode.QcmdSelectPartyQuest => (InterServerOpcode.QcmdSelectPartyQuestAckOk, InterServerOpcode.QcmdSelectPartyQuestAckCancel),
            _ => (InterServerOpcode.QcmdCompletePartyQuestAckOk, InterServerOpcode.QcmdCompletePartyQuestAckCancel),
        };
        var characterId = frame.Header.CharUid;
        byte[]? reply;
        try
        {
            reply = Execute(frame.Opcode, characterId, frame.Body, ctx);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"party-quest 0x{(ushort)frame.Opcode:X4} char 0x{characterId:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            reply = null;
        }
        await ReplyAsync(session, frame, reply is null ? cancel : ok, reply ?? Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private byte[]? Execute(InterServerOpcode opcode, ulong characterId, byte[] body, MasterContext ctx)
    {
        switch (opcode)
        {
            case InterServerOpcode.QcmdAcceptPartyQuest:
            {
                var request = PartyQuestMessages.DecodeAccept(body);
                var partyUid = _store.Create((ushort)request.QuestId, request.MonsterId, request.TargetCount);
                var counts = _store.CompletionCounts((ushort)request.QuestId, request.Members.ToArray());
                return PartyQuestMessages.EncodeAccepted(new PartyQuestAccepted(partyUid, counts));
            }
            case InterServerOpcode.QcmdInsertPartyQuest:
            {
                var request = PartyQuestMessages.DecodeComplete(body);
                var outcome = _store.Complete(characterId, request.PartyUid, (ushort)request.QuestId, request.KillCount, request.RecordTime, request.MemberCount);
                if (!outcome.Succeeded) ctx.Log($"party-quest complete char 0x{characterId:X16} quest {request.QuestId}: {outcome.Error}");
                return outcome.Succeeded ? PartyQuestMessages.EncodeCompletionCount(outcome.ComCount) : null;
            }
            case InterServerOpcode.QcmdSelectPartyQuest:
                return PartyQuestMessages.EncodeList(_store.ListUnrewarded(characterId));
            default:
            {
                var request = PartyQuestMessages.DecodeReward(body);
                var outcome = _store.Reward(characterId, (ushort)request.QuestId, request.PartyUid);
                if (!outcome.Succeeded) ctx.Log($"party-quest reward char 0x{characterId:X16} quest {request.QuestId}: {outcome.Error}");
                return outcome.Succeeded ? PartyQuestMessages.EncodeCompletionCount(outcome.ComCount) : null;
            }
        }
    }

    private static Task ReplyAsync(MasterServerSession session, InterServerFrame request, InterServerOpcode opcode, byte[] body, CancellationToken ct) =>
        session.SendAsync(new InterServerFrame
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
        }, ct);
}
