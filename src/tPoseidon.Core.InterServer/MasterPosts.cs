using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterPosts
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;

    public MasterPosts(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<PostCounts?> CountAsync(ulong charUid, CancellationToken ct)
    {
        var reply = await CallAsync(charUid, InterServerOpcode.PcmdPostCount, InterServerOpcode.PcmdPostCountAckOk, null, Array.Empty<byte>(), ct).ConfigureAwait(false);
        return reply is { Ok: true } ok ? PostMessages.DecodeCounts(ok.Body) : null;
    }

    public async Task<PostSyncResult?> SyncAsync(ulong charUid, PostSyncMessage message, CancellationToken ct)
    {
        var reply = await CallAsync(charUid, InterServerOpcode.PcmdPostSync, InterServerOpcode.PcmdPostSyncAckOk, null, PostMessages.EncodeSync(message), ct).ConfigureAwait(false);
        return reply is { Ok: true } ok ? PostMessages.DecodeSyncResult(ok.Body) : null;
    }

    public Task<PostResult<IReadOnlyList<PostListRow>>> ListAsync(ulong charUid, PostListMessage message, CancellationToken ct) =>
        CallForAsync(charUid, InterServerOpcode.PcmdPostList, InterServerOpcode.PcmdPostListAckOk, InterServerOpcode.PcmdPostListAckCancel,
            PostMessages.EncodeList(message), body => PostMessages.DecodeRows(body), ct);

    public Task<PostResult<PostSent>> SendGmGiftAsync(GmGift gift, CancellationToken ct) =>
        CallForAsync(gift.ToCharUid, InterServerOpcode.PcmdSendGmGift, InterServerOpcode.PcmdSendGmGiftAckOk, InterServerOpcode.PcmdSendGmGiftAckCancel,
            PostMessages.EncodeGmGift(gift), body => PostMessages.DecodeSent(body), ct);

    public Task<PostResult<PostDetail>> DetailAsync(ulong charUid, PostDetailMessage message, CancellationToken ct) =>
        CallForAsync(charUid, InterServerOpcode.PcmdPostDetail, InterServerOpcode.PcmdPostDetailAckOk, InterServerOpcode.PcmdPostDetailAckCancel,
            PostMessages.EncodeDetail(message), body => PostMessages.DecodeDetailResult(body), ct);

    public Task<PostResult<GiftDetail>> GiftDetailAsync(ulong charUid, PostDetailMessage message, CancellationToken ct) =>
        CallForAsync(charUid, InterServerOpcode.PcmdPostDetail, InterServerOpcode.PcmdPostDetailAckOk, InterServerOpcode.PcmdPostDetailAckCancel,
            PostMessages.EncodeDetail(message), body => PostMessages.DecodeGiftDetail(body), ct);

    public Task<PostResult<PostSent>> SendAsync(PostSendRequest request, CancellationToken ct) =>
        CallForAsync(request.FromCharUid, InterServerOpcode.PcmdSendPost, InterServerOpcode.PcmdPostItemAckOk, InterServerOpcode.PcmdSendPostAckCancel,
            PostMessages.EncodeSend(request), body => PostMessages.DecodeSent(body), ct);

    public Task<PostResult<PostReceived>> ReceiveAsync(PostReceiveRequest request, CancellationToken ct) =>
        CallForAsync(request.ToCharUid, InterServerOpcode.PcmdRecvPost, InterServerOpcode.PcmdPostItemAckOk, InterServerOpcode.PcmdRecvPostAckCancel,
            PostMessages.EncodeReceive(request), body => PostMessages.DecodeReceived(body), ct);

    public Task<PostResult<IReadOnlyList<GiftReceivedItem>>> ReceiveGiftAsync(GiftReceiveRequest request, CancellationToken ct) =>
        CallForAsync(request.ToCharUid, InterServerOpcode.PcmdRecvPost, InterServerOpcode.PcmdPostItemAckOk, InterServerOpcode.PcmdRecvPostAckCancel,
            PostMessages.EncodeReceiveGift(request), body => PostMessages.DecodeGiftReceived(body), ct);

    public Task<PostResult<PostListRow?>> DeleteAsync(ulong charUid, PostDeleteMessage message, CancellationToken ct) =>
        CallForAsync(charUid, InterServerOpcode.PcmdDeletePost, InterServerOpcode.PcmdDeletePostAckOk, InterServerOpcode.PcmdDeletePostAckCancel,
            PostMessages.EncodeDelete(message), body => PostMessages.DecodeDeleted(body), ct);

    private delegate T Decoder<out T>(ReadOnlySpan<byte> body);

    private async Task<PostResult<T>> CallForAsync<T>(ulong charUid, InterServerOpcode request, InterServerOpcode ok, InterServerOpcode cancel,
        byte[] body, Decoder<T> decode, CancellationToken ct)
    {
        var reply = await CallAsync(charUid, request, ok, cancel, body, ct).ConfigureAwait(false);
        if (reply is not { } answer) return PostResult.Fail<T>(PostRules.Failed);
        return answer.Ok ? PostResult.Ok(decode(answer.Body)) : PostResult.Fail<T>(PostMessages.DecodeError(answer.Body));
    }

    private async Task<(bool Ok, byte[] Body)?> CallAsync(ulong charUid, InterServerOpcode request, InterServerOpcode ok, InterServerOpcode? cancel, byte[] body, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[post] Master not connected; 0x{(ushort)request:X4} for 0x{charUid:X} not sent");
            return null;
        }
        var expected = new HashSet<InterServerOpcode> { ok };
        if (cancel is { } refused) expected.Add(refused);
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(new InterServerFrame
            {
                Opcode = request,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = charUid,
                    RequestId = client.AllocateRequestId(),
                },
                Body = body,
            }, expected, Timeout, ct).ConfigureAwait(false);
            return (reply.Opcode == ok, reply.Body);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[post] Master did not answer 0x{(ushort)request:X4} for 0x{charUid:X}");
            return null;
        }
    }
}
