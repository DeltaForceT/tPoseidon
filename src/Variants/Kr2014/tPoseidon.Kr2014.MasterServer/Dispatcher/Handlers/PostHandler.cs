using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class PostHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.PcmdPostList,
        InterServerOpcode.PcmdPostSync,
        InterServerOpcode.PcmdPostDetail,
        InterServerOpcode.PcmdSendPost,
        InterServerOpcode.PcmdRecvPost,
        InterServerOpcode.PcmdDeletePost,
        InterServerOpcode.PcmdPostCount,
        InterServerOpcode.PcmdSendGmGift,
    };

    private readonly IPostStore _store;

    public PostHandler(IPostStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var charUid = frame.Header.CharUid;
        (InterServerOpcode Opcode, byte[] Body)? reply;
        try
        {
            reply = Execute(frame.Opcode, charUid, frame.Body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"post 0x{(ushort)frame.Opcode:X4} char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            reply = CancelOf(frame.Opcode) is { } cancel ? (cancel, PostMessages.EncodeError(PostRules.Failed)) : null;
        }
        if (reply is not { } answer)
        {
            ctx.Log($"post 0x{(ushort)frame.Opcode:X4} char 0x{charUid:X16}: no reply");
            return;
        }
        if (answer.Opcode == CancelOf(frame.Opcode))
        {
            ctx.Log($"post 0x{(ushort)frame.Opcode:X4} char 0x{charUid:X16}: error {PostMessages.DecodeError(answer.Body)}");
        }
        await ReplyAsync(session, frame, answer.Opcode, answer.Body, ct).ConfigureAwait(false);
    }

    private (InterServerOpcode, byte[])? Execute(InterServerOpcode opcode, ulong charUid, byte[] body)
    {
        switch (opcode)
        {
            case InterServerOpcode.PcmdPostList:
            {
                var request = PostMessages.DecodeList(body);
                var result = _store.List(request.ListType, request.AccountId, charUid, request.Cursor, request.Rows);
                return result.Succeeded
                    ? (InterServerOpcode.PcmdPostListAckOk, PostMessages.EncodeRows(result.Value!))
                    : (InterServerOpcode.PcmdPostListAckCancel, PostMessages.EncodeError(result.Error));
            }
            case InterServerOpcode.PcmdPostSync:
            {
                var request = PostMessages.DecodeSync(body);
                var result = _store.Sync(request.AccountId, charUid, request.Cursor);
                return result.Succeeded ? (InterServerOpcode.PcmdPostSyncAckOk, PostMessages.EncodeSyncResult(result.Value!)) : null;
            }
            case InterServerOpcode.PcmdSendGmGift:
            {
                var result = _store.SendGmGift(PostMessages.DecodeGmGift(charUid, body));
                return result.Succeeded
                    ? (InterServerOpcode.PcmdSendGmGiftAckOk, PostMessages.EncodeSent(result.Value!))
                    : (InterServerOpcode.PcmdSendGmGiftAckCancel, PostMessages.EncodeError(result.Error));
            }
            case InterServerOpcode.PcmdPostCount:
            {
                var result = _store.Count(charUid);
                return result.Succeeded ? (InterServerOpcode.PcmdPostCountAckOk, PostMessages.EncodeCounts(result.Value!)) : null;
            }
            case InterServerOpcode.PcmdPostDetail when PostMessages.DecodeDetail(body).PostType == PostRules.GiftBox:
            {
                var request = PostMessages.DecodeDetail(body);
                var result = _store.GiftDetail(request.PostUid, request.AccountId, charUid);
                return result.Succeeded
                    ? (InterServerOpcode.PcmdPostDetailAckOk, PostMessages.EncodeGiftDetail(result.Value!))
                    : (InterServerOpcode.PcmdPostDetailAckCancel, PostMessages.EncodeError(result.Error));
            }
            case InterServerOpcode.PcmdRecvPost when PostMessages.ReceiveKind(body) == PostMessages.GiftKind:
            {
                var result = _store.ReceiveGift(PostMessages.DecodeReceiveGift(charUid, body));
                return result.Succeeded
                    ? (InterServerOpcode.PcmdPostItemAckOk, PostMessages.EncodeGiftReceived(result.Value!))
                    : (InterServerOpcode.PcmdRecvPostAckCancel, PostMessages.EncodeError(result.Error));
            }
            case InterServerOpcode.PcmdPostDetail:
            {
                var request = PostMessages.DecodeDetail(body);
                var result = _store.Detail(request.PostUid, request.AccountId, charUid);
                return result.Succeeded
                    ? (InterServerOpcode.PcmdPostDetailAckOk, PostMessages.EncodeDetailResult(result.Value!))
                    : (InterServerOpcode.PcmdPostDetailAckCancel, PostMessages.EncodeError(result.Error));
            }
            case InterServerOpcode.PcmdSendPost:
            {
                var result = _store.Send(PostMessages.DecodeSend(charUid, body));
                return result.Succeeded
                    ? (InterServerOpcode.PcmdPostItemAckOk, PostMessages.EncodeSent(result.Value!))
                    : (InterServerOpcode.PcmdSendPostAckCancel, PostMessages.EncodeError(result.Error));
            }
            case InterServerOpcode.PcmdRecvPost:
            {
                var result = _store.Receive(PostMessages.DecodeReceive(charUid, body));
                return result.Succeeded
                    ? (InterServerOpcode.PcmdPostItemAckOk, PostMessages.EncodeReceived(result.Value!))
                    : (InterServerOpcode.PcmdRecvPostAckCancel, PostMessages.EncodeError(result.Error));
            }
            default:
            {
                var request = PostMessages.DecodeDelete(body);
                var result = _store.Delete(request.PostUid, request.AccountId, charUid, request.LastDateSeq);
                return result.Succeeded
                    ? (InterServerOpcode.PcmdDeletePostAckOk, PostMessages.EncodeDeleted(result.Value))
                    : (InterServerOpcode.PcmdDeletePostAckCancel, PostMessages.EncodeError(result.Error));
            }
        }
    }

    private static InterServerOpcode? CancelOf(InterServerOpcode opcode) => opcode switch
    {
        InterServerOpcode.PcmdPostList => InterServerOpcode.PcmdPostListAckCancel,
        InterServerOpcode.PcmdSendGmGift => InterServerOpcode.PcmdSendGmGiftAckCancel,
        InterServerOpcode.PcmdPostDetail => InterServerOpcode.PcmdPostDetailAckCancel,
        InterServerOpcode.PcmdSendPost => InterServerOpcode.PcmdSendPostAckCancel,
        InterServerOpcode.PcmdRecvPost => InterServerOpcode.PcmdRecvPostAckCancel,
        InterServerOpcode.PcmdDeletePost => InterServerOpcode.PcmdDeletePostAckCancel,
        _ => null,
    };

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
