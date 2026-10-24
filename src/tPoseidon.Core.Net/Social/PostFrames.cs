using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Social;

public sealed record PostSendForm(
    byte PostType,
    byte PostSubType,
    string Title,
    string Content,
    int Money,
    string ToCharName,
    IReadOnlyList<PostSendItem> Items);

public static class PostFrames
{
    public const ushort OpenList = 0x04CE;
    public const ushort OpenPost = 0x04D1;
    public const ushort TakePost = 0x04D4;
    public const ushort SendPost = 0x04D7;
    public const ushort DeletePost = 0x04DA;

    public const ushort Count = 0x04CC;
    public const ushort Synced = 0x04CD;
    public const ushort Listed = 0x04CF;
    public const ushort ListFailed = 0x04D0;
    public const ushort Opened = 0x04D2;
    public const ushort OpenFailed = 0x04D3;
    public const ushort Taken = 0x04D5;
    public const ushort TakeFailed = 0x04D6;
    public const ushort Sent = 0x04D8;
    public const ushort SendFailed = 0x04D9;
    public const ushort Deleted = 0x04DB;
    public const ushort DeleteFailed = 0x04DC;

    public const int ChunkSize = 0x780;
    public const int SendHeaderSize = 0x1AA;
    public const int MaxAttachments = 8;

    private const int HeaderTitle = 0x02;
    private const int HeaderContent = 0x23;
    private const int HeaderMoney = 0x154;
    private const int HeaderRecipient = 0x18A;

    private const byte CommonClass = 1;

    public static bool TryReadListType(ReadOnlySpan<byte> body, out byte listType)
    {
        listType = 0;
        if (body.Length < 1) return false;
        listType = body[0];
        return true;
    }

    public static bool TryReadOpen(ReadOnlySpan<byte> body, out byte postType, out uint postUid)
    {
        postType = 0;
        postUid = 0;
        if (body.Length < 5) return false;
        var r = new PacketReader(body);
        postType = r.ReadByte();
        postUid = r.ReadUInt32();
        return true;
    }

    public static bool TryReadPostUid(ReadOnlySpan<byte> body, out uint postUid)
    {
        postUid = 0;
        if (body.Length < 4) return false;
        postUid = new PacketReader(body).ReadUInt32();
        return true;
    }

    public static bool TryReadSend(ReadOnlySpan<byte> body, Encoding encoding, out PostSendForm? form)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        form = null;
        if (body.Length < SendHeaderSize + 1) return false;
        var header = body[..SendHeaderSize];
        var r = new PacketReader(body[SendHeaderSize..]);
        var count = r.ReadByte();
        if (body.Length < SendHeaderSize + 1 + count * 12) return false;
        var items = new List<PostSendItem>(count);
        for (var i = 0; i < count; i++)
        {
            var uid = r.ReadUInt64();
            var amount = r.ReadUInt32();
            items.Add(new PostSendItem(uid, (ushort)Math.Min(amount, ushort.MaxValue)));
        }
        form = new PostSendForm(
            header[0],
            header[1],
            FieldText(header[HeaderTitle..HeaderContent], encoding),
            FieldText(header[HeaderContent..HeaderMoney], encoding),
            new PacketReader(header[HeaderMoney..]).ReadInt32(),
            FieldText(header[HeaderRecipient..SendHeaderSize], encoding),
            items);
        return true;
    }

    public static int FieldLength(ReadOnlySpan<byte> field)
    {
        var end = field.IndexOf((byte)0);
        return end < 0 ? field.Length : end;
    }

    private static string FieldText(ReadOnlySpan<byte> field, Encoding encoding) =>
        encoding.GetString(field[..FieldLength(field)]);

    public static byte[] Error(uint code)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public static byte[] Counts(PostCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var w = new PacketWriter(4);
        w.WriteUInt16(Clamp16(counts.NewPosts));
        w.WriteUInt16(Clamp16(counts.ReturnedPosts));
        return w.ToArray();
    }

    public static byte[] SyncBody(PostSyncResult sync, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(sync);
        var rows = sync.Mail.Concat(sync.Gifts).ToList();
        var w = new PacketWriter(6 + rows.Count * 64);
        w.WriteUInt16(Clamp16(sync.Counts.NewPosts));
        w.WriteUInt16(Clamp16(sync.Counts.ReturnedPosts));
        w.WriteUInt16(Clamp16(rows.Count));
        foreach (var row in rows) WriteRow(w, row, encoding);
        return w.ToArray();
    }

    public static byte[] ListBody(byte listType, IReadOnlyList<PostListRow> rows, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var w = new PacketWriter(2 + rows.Count * 64);
        w.WriteByte(listType);
        w.WriteByte((byte)Math.Min(rows.Count, byte.MaxValue));
        foreach (var row in rows.Take(byte.MaxValue)) WriteRow(w, row, encoding);
        return w.ToArray();
    }

    public static byte[] DeletedBody(uint postUid, PostListRow? next, Encoding encoding)
    {
        var w = new PacketWriter(72);
        w.WriteUInt32(postUid);
        w.WriteByte(next is null ? (byte)0 : (byte)1);
        if (next is not null) WriteRow(w, next, encoding);
        return w.ToArray();
    }

    public static byte[] DetailBody(PostDetail detail, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(64 + detail.Items.Count * 32 + detail.Options.Count * 32);
        w.WriteUInt32(detail.PostUid);
        w.WriteNulTerminated(detail.Content, encoding);
        w.WriteInt32(detail.Money);
        w.WriteUInt16(Clamp16(detail.Items.Count));
        foreach (var item in detail.Items)
        {
            w.WriteInt64(item.ItemSeq);
            w.WriteByte(item.ItemClass);
            w.WriteInt32(item.ItemIndex);
            w.WriteUInt64(item.ItemUid);
            w.WriteInt16(item.ItemAttr);
            if (item.ItemClass == CommonClass) continue;
            w.WriteByte(0);
            w.WriteByte(item.RefineLevel);
            w.WriteByte(item.RefineState);
            w.WriteByte(item.ItemSlot);
        }
        w.WriteUInt16(Clamp16(detail.Options.Count));
        foreach (var option in detail.Options)
        {
            w.WriteInt64(option.ItemSeq);
            w.WriteUInt64(option.ItemUid);
            w.WriteByte(option.OptionType);
            w.WriteUInt16(option.OptionIndex);
            w.WriteInt16(option.OptionValue);
            w.WriteUInt32(option.MixedItem);
        }
        return w.ToArray();
    }

    public static byte[] SentBody(IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(2 + operations.Count * 32);
        w.WriteUInt16((ushort)operations.Count);
        foreach (var operation in operations) w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }

    public static byte[] TakenBody(uint postUid, IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(5 + operations.Count * 40);
        w.WriteUInt32(postUid);
        w.WriteByte((byte)operations.Count);
        foreach (var operation in operations)
        {
            w.WriteBytes(QuestResultFrames.ItemOperation(operation));
            w.WriteUInt64(0);
        }
        return w.ToArray();
    }

    public static byte[] GiftDetailBody(GiftDetail detail, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(32 + detail.Items.Count * 32);
        w.WriteUInt32(detail.PostUid);
        w.WriteNulTerminated(detail.Content, encoding);
        w.WriteUInt16(Clamp16(detail.Items.Count));
        foreach (var item in detail.Items)
        {
            w.WriteInt64(item.ItemSeq);
            w.WriteByte(item.ItemClass);
            w.WriteInt32(item.ItemIndex);
            w.WriteUInt64(0);
            w.WriteInt16(item.ItemAttr);
            if (item.ItemClass != CommonClass) w.WriteZero(4);
            w.WriteInt32(item.AddMinute);
            w.WriteByte(item.ItemFlag);
        }
        return w.ToArray();
    }

    public static IReadOnlyList<byte[]> Chunks(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var chunks = new List<byte[]>();
        var offset = 0;
        do
        {
            var size = Math.Min(ChunkSize, body.Length - offset);
            var chunk = new byte[size + 1];
            chunk[0] = size < ChunkSize ? (byte)1 : (byte)0;
            Array.Copy(body, offset, chunk, 1, size);
            chunks.Add(chunk);
            offset += size;
        }
        while (offset < body.Length);
        return chunks;
    }

    private static void WriteRow(PacketWriter w, PostListRow row, Encoding encoding)
    {
        w.WriteByte(row.PostType);
        w.WriteByte(row.PostSubType);
        w.WriteInt64(row.DateSeq);
        w.WriteUInt32(row.PostUid);
        w.WriteByte(row.PostState);
        w.WriteByte(row.UserType);
        w.WriteUInt64(ClientClock.FileTime(row.ReturnDate));
        w.WriteUInt64(ClientClock.FileTime(row.OccurDate));
        w.WriteNulTerminated(row.CharName, encoding);
        w.WriteNulTerminated(row.Title, encoding);
    }

    private static ushort Clamp16(int value) => (ushort)Math.Clamp(value, 0, ushort.MaxValue);
}
