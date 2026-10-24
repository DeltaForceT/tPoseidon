using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class PostFramesTests
{
    private static readonly DateTime Occur = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    private static PostListRow Row(uint uid) =>
        new(41, uid, PostRules.MailBox, PostRules.GiftLetter, PostRules.Unread, PostRules.ReceiverSide, "Tata", "hi", Occur, Occur.AddDays(7));

    [Fact]
    public void TheSendFormIsReadAtTheKrHeaderOffsets()
    {
        var body = new byte[PostFrames.SendHeaderSize + 1 + 12];
        body[0] = 0;
        body[1] = PostRules.GiftLetter;
        Encoding.Latin1.GetBytes("title").CopyTo(body, 0x02);
        Encoding.Latin1.GetBytes("letter").CopyTo(body, 0x23);
        BitConverter.GetBytes(1234).CopyTo(body, 0x154);
        Encoding.Latin1.GetBytes("Beta").CopyTo(body, 0x18A);
        body[PostFrames.SendHeaderSize] = 1;
        BitConverter.GetBytes(213UL).CopyTo(body, PostFrames.SendHeaderSize + 1);
        BitConverter.GetBytes(4u).CopyTo(body, PostFrames.SendHeaderSize + 9);

        Assert.True(PostFrames.TryReadSend(body, Encoding.Latin1, out var form));

        Assert.Equal(PostRules.GiftLetter, form!.PostSubType);
        Assert.Equal("title", form.Title);
        Assert.Equal("letter", form.Content);
        Assert.Equal(1234, form.Money);
        Assert.Equal("Beta", form.ToCharName);
        Assert.Equal(new PostSendItem(213, 4), Assert.Single(form.Items));
    }

    [Fact]
    public void AShortSendFormIsRefused()
    {
        Assert.False(PostFrames.TryReadSend(new byte[PostFrames.SendHeaderSize], Encoding.Latin1, out _));
    }

    [Fact]
    public void AListRowFollowsTheMasterRowLayout()
    {
        var body = PostFrames.ListBody(PostRules.ReceivedList, new[] { Row(7) }, Encoding.Latin1);

        Assert.Equal(2 + 1 + 1 + 8 + 4 + 1 + 1 + 8 + 8 + 5 + 3, body.Length);
        Assert.Equal(PostRules.ReceivedList, body[0]);
        Assert.Equal(1, body[1]);
        Assert.Equal(PostRules.MailBox, body[2]);
        Assert.Equal(PostRules.GiftLetter, body[3]);
        Assert.Equal(41L, BitConverter.ToInt64(body, 4));
        Assert.Equal(7u, BitConverter.ToUInt32(body, 12));
    }

    [Fact]
    public void ACommonItemInTheDetailHasNoRefineBytes()
    {
        var detail = new PostDetail(9, "c", 0,
            new[]
            {
                new PostItemRow(1, 100, 1001, 1, 4, 0, 0, 0, 0),
                new PostItemRow(2, 213, 8000, 2, 600, 0, 3, 1, 2),
            },
            Array.Empty<PostItemOption>());

        var body = PostFrames.DetailBody(detail, Encoding.Latin1);

        Assert.Equal(4 + 2 + 4 + 2 + (9 + 14) + (9 + 18) + 2, body.Length);
    }

    [Fact]
    public void LongBodiesAreSentInChunksAndTheLastIsMarked()
    {
        var chunks = PostFrames.Chunks(new byte[PostFrames.ChunkSize + 10]);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(0, chunks[0][0]);
        Assert.Equal(PostFrames.ChunkSize + 1, chunks[0].Length);
        Assert.Equal(1, chunks[1][0]);
        Assert.Equal(11, chunks[1].Length);
    }

    [Fact]
    public void AnEmptyBodyIsOneLastChunk()
    {
        Assert.Equal(new byte[] { 1 }, Assert.Single(PostFrames.Chunks(Array.Empty<byte>())));
    }

    [Fact]
    public void SendRequestsSurviveTheMasterHop()
    {
        var request = new PostSendRequest(PostRules.PaymentRequest, "t", "c", 5, 500, 1000, 7, "Tata", 13, "Beta", new[] { new PostSendItem(213, 1) });

        var back = PostMessages.DecodeSend(7, PostMessages.EncodeSend(request));

        Assert.Equal(request with { Items = back.Items }, back);
        Assert.Equal(request.Items, back.Items);
    }

    [Fact]
    public void SyncResultsSurviveTheMasterHop()
    {
        var sync = new PostSyncResult(new PostCounts(2, 1), new[] { Row(1), Row(2) }, Array.Empty<PostListRow>());

        var back = PostMessages.DecodeSyncResult(PostMessages.EncodeSyncResult(sync));

        Assert.Equal(sync.Counts, back.Counts);
        Assert.Equal(sync.Mail, back.Mail);
        Assert.Empty(back.Gifts);
    }

    [Fact]
    public void ReceivedItemsSurviveTheMasterHop()
    {
        var received = new PostReceived(PostRules.PaymentRequest, -500,
            new[] { new PostReceivedItem(PostItemChange.Stacked, 3, 100, 1001, 1, 4, 7, 0, 0, 0, 0) },
            new[] { new PostItemOption(3, 100, 10, 10000, 5, 0) },
            1000, 7);

        var back = PostMessages.DecodeReceived(PostMessages.EncodeReceived(received));

        Assert.Equal(received.MoneyChange, back.MoneyChange);
        Assert.Equal(received.Items, back.Items);
        Assert.Equal(received.Options, back.Options);
        Assert.Equal(7UL, back.PaymentToCharUid);
    }

    [Fact]
    public void GiftRequestsAndRepliesSurviveTheMasterHop()
    {
        var request = new GiftReceiveRequest(9, 1000, 7, 15, new[] { new GiftItemOption(3, 10000, 12, 0) });
        var body = PostMessages.EncodeReceiveGift(request);
        Assert.Equal(PostMessages.GiftKind, PostMessages.ReceiveKind(body));
        var back = PostMessages.DecodeReceiveGift(7, body);
        Assert.Equal(request.Options, back.Options);
        Assert.Equal(request with { Options = back.Options }, back);

        var detail = new GiftDetail(9, "congrats", new[] { new GiftDetailItem(3, 7969, 1, 1, 0, 2) });
        var detailBack = PostMessages.DecodeGiftDetail(PostMessages.EncodeGiftDetail(detail));
        Assert.Equal(detail.Items, detailBack.Items);

        var mail = PostMessages.EncodeReceive(new PostReceiveRequest(9, 0, 1000, 7, 15, "Tata", "", ""));
        Assert.Equal(PostMessages.MailKind, PostMessages.ReceiveKind(mail));
        Assert.Equal(9u, PostMessages.DecodeReceive(7, mail).PostUid);
    }

    [Fact]
    public void TheTakenReplyHasAnEmptyTimerAfterEachItem()
    {
        var body = PostFrames.TakenBody(9, new[] { new BagOperation(QuestResultFrames.Created, 1, 7969, 0x1_0000_0005, 1) });

        Assert.Equal(4 + 1 + 16 + 8, body.Length);
        Assert.Equal(1, body[4]);
        Assert.Equal(0UL, BitConverter.ToUInt64(body, body.Length - 8));
    }
}
