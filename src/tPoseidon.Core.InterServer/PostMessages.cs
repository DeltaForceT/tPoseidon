using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed record PostListMessage(uint AccountId, byte ListType, long Cursor, byte Rows);

public sealed record PostSyncMessage(uint AccountId, long Cursor);

public sealed record PostDetailMessage(uint AccountId, uint PostUid, byte PostType);

public sealed record PostDeleteMessage(uint AccountId, uint PostUid, long LastDateSeq);

public static class PostMessages
{
    private static readonly Encoding Text = Encoding.UTF8;

    public static byte[] EncodeList(PostListMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var w = new PacketWriter(14);
        w.WriteUInt32(message.AccountId);
        w.WriteByte(message.ListType);
        w.WriteInt64(message.Cursor);
        w.WriteByte(message.Rows);
        return w.ToArray();
    }

    public static PostListMessage DecodeList(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new PostListMessage(r.ReadUInt32(), r.ReadByte(), r.ReadInt64(), r.ReadByte());
    }

    public static byte[] EncodeSync(PostSyncMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var w = new PacketWriter(12);
        w.WriteUInt32(message.AccountId);
        w.WriteInt64(message.Cursor);
        return w.ToArray();
    }

    public static PostSyncMessage DecodeSync(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new PostSyncMessage(r.ReadUInt32(), r.ReadInt64());
    }

    public static byte[] EncodeDetail(PostDetailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var w = new PacketWriter(9);
        w.WriteUInt32(message.AccountId);
        w.WriteUInt32(message.PostUid);
        w.WriteByte(message.PostType);
        return w.ToArray();
    }

    public static PostDetailMessage DecodeDetail(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new PostDetailMessage(r.ReadUInt32(), r.ReadUInt32(), r.ReadByte());
    }

    public static byte[] EncodeDelete(PostDeleteMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var w = new PacketWriter(16);
        w.WriteUInt32(message.AccountId);
        w.WriteUInt32(message.PostUid);
        w.WriteInt64(message.LastDateSeq);
        return w.ToArray();
    }

    public static PostDeleteMessage DecodeDelete(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new PostDeleteMessage(r.ReadUInt32(), r.ReadUInt32(), r.ReadInt64());
    }

    public static byte[] EncodeSend(PostSendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var w = new PacketWriter(128 + request.Items.Count * 10);
        w.WriteByte(request.PostSubType);
        w.WriteNulTerminated(request.Title, Text);
        w.WriteNulTerminated(request.Content, Text);
        w.WriteInt32(request.MoneyTax);
        w.WriteInt32(request.Money);
        w.WriteUInt32(request.FromAccountId);
        w.WriteNulTerminated(request.FromCharName, Text);
        w.WriteInt32(request.FromCharLevel);
        w.WriteNulTerminated(request.ToCharName, Text);
        w.WriteByte((byte)request.Items.Count);
        foreach (var item in request.Items)
        {
            w.WriteUInt64(item.ItemUid);
            w.WriteUInt16(item.Count);
        }
        return w.ToArray();
    }

    public static PostSendRequest DecodeSend(ulong fromCharUid, ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var subType = r.ReadByte();
        var title = r.ReadNulTerminated(Text);
        var content = r.ReadNulTerminated(Text);
        var tax = r.ReadInt32();
        var money = r.ReadInt32();
        var account = r.ReadUInt32();
        var fromName = r.ReadNulTerminated(Text);
        var level = r.ReadInt32();
        var toName = r.ReadNulTerminated(Text);
        var count = r.ReadByte();
        var items = new List<PostSendItem>(count);
        for (var i = 0; i < count; i++) items.Add(new PostSendItem(r.ReadUInt64(), r.ReadUInt16()));
        return new PostSendRequest(subType, title, content, tax, money, account, fromCharUid, fromName, level, toName, items);
    }

    public const byte MailKind = 0;
    public const byte GiftKind = 1;

    public static byte ReceiveKind(ReadOnlySpan<byte> body) => body.Length > 0 ? body[0] : MailKind;

    public static byte[] EncodeReceiveGift(GiftReceiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var w = new PacketWriter(16 + request.Options.Count * 18);
        w.WriteByte(GiftKind);
        w.WriteUInt32(request.PostUid);
        w.WriteUInt32(request.ToAccountId);
        w.WriteInt32(request.ToCharLevel);
        w.WriteUInt16((ushort)request.Options.Count);
        foreach (var option in request.Options)
        {
            w.WriteInt64(option.ItemSeq);
            w.WriteUInt16(option.OptionIndex);
            w.WriteInt32(option.OptionValue);
            w.WriteInt32(option.MixedItem);
        }
        return w.ToArray();
    }

    public static GiftReceiveRequest DecodeReceiveGift(ulong toCharUid, ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body[1..]);
        var uid = r.ReadUInt32();
        var account = r.ReadUInt32();
        var level = r.ReadInt32();
        var count = r.ReadUInt16();
        var options = new List<GiftItemOption>(count);
        for (var i = 0; i < count; i++) options.Add(new GiftItemOption(r.ReadInt64(), r.ReadUInt16(), r.ReadInt32(), r.ReadInt32()));
        return new GiftReceiveRequest(uid, account, toCharUid, level, options);
    }

    public static byte[] EncodeGiftDetail(GiftDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        var w = new PacketWriter(32 + detail.Items.Count * 24);
        w.WriteUInt32(detail.PostUid);
        w.WriteNulTerminated(detail.Content, Text);
        w.WriteUInt16((ushort)detail.Items.Count);
        foreach (var item in detail.Items)
        {
            w.WriteInt64(item.ItemSeq);
            w.WriteInt32(item.ItemIndex);
            w.WriteByte(item.ItemClass);
            w.WriteInt16(item.ItemAttr);
            w.WriteInt32(item.AddMinute);
            w.WriteByte(item.ItemFlag);
        }
        return w.ToArray();
    }

    public static GiftDetail DecodeGiftDetail(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var uid = r.ReadUInt32();
        var content = r.ReadNulTerminated(Text);
        var count = r.ReadUInt16();
        var items = new List<GiftDetailItem>(count);
        for (var i = 0; i < count; i++) items.Add(new GiftDetailItem(r.ReadInt64(), r.ReadInt32(), r.ReadByte(), r.ReadInt16(), r.ReadInt32(), r.ReadByte()));
        return new GiftDetail(uid, content, items);
    }

    public static byte[] EncodeGiftReceived(IReadOnlyList<GiftReceivedItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var w = new PacketWriter(2 + items.Count * 32);
        w.WriteUInt16((ushort)items.Count);
        foreach (var item in items)
        {
            w.WriteInt64(item.ItemSeq);
            w.WriteUInt64(item.ItemUid);
            w.WriteInt32(item.ItemIndex);
            w.WriteByte(item.ItemClass);
            w.WriteUInt16(item.Count);
            w.WriteInt64(item.BreaksAt.ToBinary());
        }
        return w.ToArray();
    }

    public static IReadOnlyList<GiftReceivedItem> DecodeGiftReceived(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var count = r.ReadUInt16();
        var items = new List<GiftReceivedItem>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(new GiftReceivedItem(r.ReadInt64(), r.ReadUInt64(), r.ReadInt32(), r.ReadByte(), r.ReadUInt16(), DateTime.FromBinary(r.ReadInt64())));
        }
        return items;
    }

    public static byte[] EncodeReceive(PostReceiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var w = new PacketWriter(64);
        w.WriteByte(MailKind);
        w.WriteUInt32(request.PostUid);
        w.WriteByte(request.PostState);
        w.WriteUInt32(request.ToAccountId);
        w.WriteInt32(request.ToCharLevel);
        w.WriteNulTerminated(request.ToCharName, Text);
        w.WriteNulTerminated(request.SysTitle, Text);
        w.WriteNulTerminated(request.SysContent, Text);
        return w.ToArray();
    }

    public static PostReceiveRequest DecodeReceive(ulong toCharUid, ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body[1..]);
        var uid = r.ReadUInt32();
        var state = r.ReadByte();
        var account = r.ReadUInt32();
        var level = r.ReadInt32();
        var name = r.ReadNulTerminated(Text);
        var title = r.ReadNulTerminated(Text);
        var content = r.ReadNulTerminated(Text);
        return new PostReceiveRequest(uid, state, account, toCharUid, level, name, title, content);
    }

    public static byte[] EncodeGmGift(GmGift gift)
    {
        ArgumentNullException.ThrowIfNull(gift);
        var w = new PacketWriter(128 + gift.Items.Count * 11);
        w.WriteNulTerminated(gift.Title, Text);
        w.WriteNulTerminated(gift.Content, Text);
        w.WriteNulTerminated(gift.GmName, Text);
        w.WriteByte((byte)gift.Items.Count);
        foreach (var item in gift.Items)
        {
            w.WriteInt32(item.ItemIndex);
            w.WriteByte(item.ItemClass);
            w.WriteInt16(item.ItemAttr);
            w.WriteInt32(item.AddMinute);
        }
        return w.ToArray();
    }

    public static GmGift DecodeGmGift(ulong toCharUid, ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var title = r.ReadNulTerminated(Text);
        var content = r.ReadNulTerminated(Text);
        var sender = r.ReadNulTerminated(Text);
        var count = r.ReadByte();
        var items = new List<GiftItem>(count);
        for (var i = 0; i < count; i++) items.Add(new GiftItem(r.ReadInt32(), r.ReadByte(), r.ReadInt16(), r.ReadInt32()));
        return new GmGift(title, content, toCharUid, sender, items);
    }

    public static byte[] EncodeError(int error)
    {
        var w = new PacketWriter(4);
        w.WriteInt32(error);
        return w.ToArray();
    }

    public static int DecodeError(ReadOnlySpan<byte> body) =>
        body.Length >= 4 ? new PacketReader(body).ReadInt32() : PostRules.Failed;

    public static byte[] EncodeCounts(PostCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var w = new PacketWriter(8);
        w.WriteInt32(counts.NewPosts);
        w.WriteInt32(counts.ReturnedPosts);
        return w.ToArray();
    }

    public static PostCounts DecodeCounts(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new PostCounts(r.ReadInt32(), r.ReadInt32());
    }

    public static byte[] EncodeRows(IReadOnlyList<PostListRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var w = new PacketWriter(2 + rows.Count * 64);
        WriteRows(w, rows);
        return w.ToArray();
    }

    public static IReadOnlyList<PostListRow> DecodeRows(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return ReadRows(ref r);
    }

    public static byte[] EncodeSyncResult(PostSyncResult sync)
    {
        ArgumentNullException.ThrowIfNull(sync);
        var w = new PacketWriter(12 + (sync.Mail.Count + sync.Gifts.Count) * 64);
        w.WriteInt32(sync.Counts.NewPosts);
        w.WriteInt32(sync.Counts.ReturnedPosts);
        WriteRows(w, sync.Mail);
        WriteRows(w, sync.Gifts);
        return w.ToArray();
    }

    public static PostSyncResult DecodeSyncResult(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var counts = new PostCounts(r.ReadInt32(), r.ReadInt32());
        var mail = ReadRows(ref r);
        var gifts = ReadRows(ref r);
        return new PostSyncResult(counts, mail, gifts);
    }

    public static byte[] EncodeDetailResult(PostDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        var w = new PacketWriter(64 + detail.Items.Count * 32 + detail.Options.Count * 32);
        w.WriteUInt32(detail.PostUid);
        w.WriteNulTerminated(detail.Content, Text);
        w.WriteInt32(detail.Money);
        w.WriteUInt16((ushort)detail.Items.Count);
        foreach (var item in detail.Items)
        {
            w.WriteInt64(item.ItemSeq);
            w.WriteUInt64(item.ItemUid);
            w.WriteInt32(item.ItemIndex);
            w.WriteByte(item.ItemClass);
            w.WriteInt16(item.ItemAttr);
            w.WriteByte(item.RefineType);
            w.WriteByte(item.RefineLevel);
            w.WriteByte(item.RefineState);
            w.WriteByte(item.ItemSlot);
        }
        WriteOptions(w, detail.Options);
        return w.ToArray();
    }

    public static PostDetail DecodeDetailResult(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var uid = r.ReadUInt32();
        var content = r.ReadNulTerminated(Text);
        var money = r.ReadInt32();
        var count = r.ReadUInt16();
        var items = new List<PostItemRow>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(new PostItemRow(r.ReadInt64(), r.ReadUInt64(), r.ReadInt32(), r.ReadByte(), r.ReadInt16(),
                r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte()));
        }
        return new PostDetail(uid, content, money, items, ReadOptions(ref r));
    }

    public static byte[] EncodeSent(PostSent sent)
    {
        ArgumentNullException.ThrowIfNull(sent);
        var w = new PacketWriter(40);
        w.WriteInt64(sent.DateSeq);
        w.WriteUInt32(sent.PostUid);
        w.WriteInt64(sent.OccurDate.ToBinary());
        w.WriteInt64(sent.ReturnDate.ToBinary());
        w.WriteUInt32(sent.ToAccountId);
        w.WriteUInt64(sent.ToCharUid);
        return w.ToArray();
    }

    public static PostSent DecodeSent(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new PostSent(r.ReadInt64(), r.ReadUInt32(), DateTime.FromBinary(r.ReadInt64()), DateTime.FromBinary(r.ReadInt64()), r.ReadUInt32(), r.ReadUInt64());
    }

    public static byte[] EncodeReceived(PostReceived received)
    {
        ArgumentNullException.ThrowIfNull(received);
        var w = new PacketWriter(32 + received.Items.Count * 40 + received.Options.Count * 32);
        w.WriteByte(received.PostSubType);
        w.WriteInt64(received.MoneyChange);
        w.WriteUInt16((ushort)received.Items.Count);
        foreach (var item in received.Items)
        {
            w.WriteByte(item.Change);
            w.WriteInt64(item.ItemSeq);
            w.WriteUInt64(item.ItemUid);
            w.WriteInt32(item.ItemIndex);
            w.WriteByte(item.ItemClass);
            w.WriteUInt16(item.Count);
            w.WriteUInt16(item.Total);
            w.WriteByte(item.RefineType);
            w.WriteByte(item.RefineLevel);
            w.WriteByte(item.RefineState);
            w.WriteByte(item.ItemSlot);
        }
        WriteOptions(w, received.Options);
        w.WriteByte(received.PaymentToCharUid is null ? (byte)0 : (byte)1);
        w.WriteUInt32(received.PaymentToAccountId ?? 0);
        w.WriteUInt64(received.PaymentToCharUid ?? 0);
        return w.ToArray();
    }

    public static PostReceived DecodeReceived(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var subType = r.ReadByte();
        var money = r.ReadInt64();
        var count = r.ReadUInt16();
        var items = new List<PostReceivedItem>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(new PostReceivedItem(r.ReadByte(), r.ReadInt64(), r.ReadUInt64(), r.ReadInt32(), r.ReadByte(), r.ReadUInt16(), r.ReadUInt16(),
                r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte()));
        }
        var options = ReadOptions(ref r);
        var hasPayment = r.ReadByte() == 1;
        var account = r.ReadUInt32();
        var character = r.ReadUInt64();
        return new PostReceived(subType, money, items, options, hasPayment ? account : null, hasPayment ? character : null);
    }

    public static byte[] EncodeDeleted(PostListRow? next)
    {
        var w = new PacketWriter(72);
        w.WriteByte(next is null ? (byte)0 : (byte)1);
        if (next is not null) WriteRow(w, next);
        return w.ToArray();
    }

    public static PostListRow? DecodeDeleted(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return r.ReadByte() == 1 ? ReadRow(ref r) : null;
    }

    private static void WriteRows(PacketWriter w, IReadOnlyList<PostListRow> rows)
    {
        w.WriteUInt16((ushort)rows.Count);
        foreach (var row in rows) WriteRow(w, row);
    }

    private static List<PostListRow> ReadRows(ref PacketReader r)
    {
        var count = r.ReadUInt16();
        var rows = new List<PostListRow>(count);
        for (var i = 0; i < count; i++) rows.Add(ReadRow(ref r));
        return rows;
    }

    private static void WriteRow(PacketWriter w, PostListRow row)
    {
        w.WriteInt64(row.DateSeq);
        w.WriteUInt32(row.PostUid);
        w.WriteByte(row.PostType);
        w.WriteByte(row.PostSubType);
        w.WriteByte(row.PostState);
        w.WriteByte(row.UserType);
        w.WriteNulTerminated(row.CharName, Text);
        w.WriteNulTerminated(row.Title, Text);
        w.WriteInt64(row.OccurDate.ToBinary());
        w.WriteInt64(row.ReturnDate.ToBinary());
    }

    private static PostListRow ReadRow(ref PacketReader r) => new(
        r.ReadInt64(),
        r.ReadUInt32(),
        r.ReadByte(),
        r.ReadByte(),
        r.ReadByte(),
        r.ReadByte(),
        r.ReadNulTerminated(Text),
        r.ReadNulTerminated(Text),
        DateTime.FromBinary(r.ReadInt64()),
        DateTime.FromBinary(r.ReadInt64()));

    private static void WriteOptions(PacketWriter w, IReadOnlyList<PostItemOption> options)
    {
        w.WriteUInt16((ushort)options.Count);
        foreach (var option in options)
        {
            w.WriteInt64(option.ItemSeq);
            w.WriteUInt64(option.ItemUid);
            w.WriteByte(option.OptionType);
            w.WriteUInt16(option.OptionIndex);
            w.WriteInt16(option.OptionValue);
            w.WriteUInt32(option.MixedItem);
        }
    }

    private static List<PostItemOption> ReadOptions(ref PacketReader r)
    {
        var count = r.ReadUInt16();
        var options = new List<PostItemOption>(count);
        for (var i = 0; i < count; i++)
        {
            options.Add(new PostItemOption(r.ReadInt64(), r.ReadUInt64(), r.ReadByte(), r.ReadUInt16(), r.ReadInt16(), r.ReadUInt32()));
        }
        return options;
    }
}
