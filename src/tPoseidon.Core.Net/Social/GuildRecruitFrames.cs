using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public readonly record struct GuildRecruitListRequest(ushort Page, byte Sort, bool Natural);

public readonly record struct GuildRecruitText(string Title, string Content);

public static class GuildRecruitFrames
{
    public const ushort ListRequest = (ushort)Opcode.C2S_GUILD_RECRUIT_LIST;
    public const ushort ListReply = (ushort)Opcode.S2C_GUILD_RECRUIT_LIST;
    public const ushort PostRequest = (ushort)Opcode.C2S_GUILD_RECRUIT_POST;
    public const ushort PostReply = (ushort)Opcode.S2C_GUILD_RECRUIT_POST;
    public const ushort UpdateRequest = (ushort)Opcode.C2S_GUILD_RECRUIT_UPDATE;
    public const ushort UpdateReply = (ushort)Opcode.S2C_GUILD_RECRUIT_UPDATE;
    public const ushort DeleteRequest = (ushort)Opcode.C2S_GUILD_RECRUIT_DELETE;
    public const ushort DeleteReply = (ushort)Opcode.S2C_GUILD_RECRUIT_DELETE;
    public const ushort ContentRequest = (ushort)Opcode.C2S_GUILD_RECRUIT_CONTENT;
    public const ushort ContentReply = (ushort)Opcode.S2C_GUILD_RECRUIT_CONTENT;
    public const int ListRequestLength = 4;
    public const int PostRequestLength = 0xF2;
    public const int UpdateRequestLength = 0x100;
    public const int ContentRequestLength = 4;
    public const int PageSize = 10;
    public const int EntryLength = 0x6D;
    public const int GuildNameLength = 25;
    public const int MasterNameLength = 20;
    public const int TitleLength = 41;
    public const int ContentLength = 201;
    public const int DateLength = 9;
    public const int UpdateReplyLength = 0x102;
    public const int ContentReplyLength = 0x13C;
    public const byte SortUpdated = 0;
    public const byte SortPoint = 1;
    public const byte SortGuildName = 2;
    public const byte SortMasterName = 3;
    public const byte SortTitle = 4;
    public const byte Failed = 1;
    public const byte Done = 0;
    public const byte PostExistence = 0;
    public const byte Inactive = 1;
    public const byte NotMaster = 2;
    public const byte Database = 3;

    public static GuildRecruitListRequest? ReadList(ReadOnlySpan<byte> payload) =>
        payload.Length < ListRequestLength
            ? null
            : new GuildRecruitListRequest(BinaryPrimitives.ReadUInt16LittleEndian(payload), payload[2], payload[3] != 0);

    public static GuildRecruitText? ReadText(ReadOnlySpan<byte> payload, int length, Encoding text)
    {
        if (payload.Length < length) return null;
        return new GuildRecruitText(Text(payload[..TitleLength], text), Text(payload.Slice(TitleLength, ContentLength), text));
    }

    public static uint? ReadContent(ReadOnlySpan<byte> payload) =>
        payload.Length < ContentRequestLength ? null : BinaryPrimitives.ReadUInt32LittleEndian(payload);

    public static IReadOnlyList<GuildRecruitView> Sorted(IReadOnlyList<GuildRecruitView> posts, byte sort, bool natural)
    {
        IOrderedEnumerable<GuildRecruitView> ordered = sort switch
        {
            SortPoint => posts.OrderByDescending(p => p.Guild.GuildPoint),
            SortGuildName => posts.OrderBy(p => p.Guild.GuildName, StringComparer.Ordinal),
            SortMasterName => posts.OrderBy(p => p.MasterName, StringComparer.Ordinal),
            SortTitle => posts.OrderBy(p => p.Post.RecruitTitle, StringComparer.Ordinal),
            _ => posts.OrderByDescending(p => p.Post.RecruitUpdatedAt),
        };
        var list = ordered.ThenBy(p => p.Guild.GuildCode).ToList();
        if (!natural) list.Reverse();
        return list;
    }

    public static byte[] ListFor(IReadOnlyList<GuildRecruitView> posts, GuildRecruitListRequest request, bool ownPostExists, Encoding text, Func<uint, GuildEmblem>? emblemOf = null)
    {
        ArgumentNullException.ThrowIfNull(posts);
        ArgumentNullException.ThrowIfNull(text);
        var sorted = Sorted(posts, request.Sort, request.Natural);
        var page = sorted.Skip(Math.Min(request.Page * PageSize, sorted.Count)).Take(PageSize).ToArray();
        var w = new PacketWriter(4 + page.Length * EntryLength);
        w.WriteByte(ownPostExists ? (byte)1 : (byte)0);
        w.WriteUInt16((ushort)Math.Min(sorted.Count, ushort.MaxValue));
        w.WriteByte((byte)page.Length);
        foreach (var post in page)
        {
            w.WriteUInt32((uint)post.Guild.GuildCode);
            w.WriteInt32(post.Guild.GuildPoint);
            WriteText(w, post.Guild.GuildName, GuildNameLength, text);
            WriteText(w, post.MasterName, MasterNameLength, text);
            WriteText(w, post.Post.RecruitTitle, TitleLength, text);
            WriteText(w, Date(post.Post.RecruitUpdatedAt), DateLength, text);
            var emblem = emblemOf?.Invoke((uint)post.Guild.GuildCode) ?? default;
            w.WriteByte(emblem.Template);
            w.WriteByte(emblem.Colour);
            w.WriteUInt32(emblem.Icon);
        }
        return w.ToArray();
    }

    public static byte[] Result(byte status, byte code) => [status, code];

    public static byte[] UpdateResult(byte status, byte code)
    {
        var body = new byte[UpdateReplyLength];
        body[0] = status;
        body[1] = code;
        return body;
    }

    public static byte[] ContentFor(GuildRecruitView? view, Encoding text)
    {
        var w = new PacketWriter(ContentReplyLength);
        if (view is null)
        {
            w.WriteByte(Failed);
            w.WriteZero(ContentReplyLength - 1);
            return w.ToArray();
        }
        w.WriteByte(Done);
        w.WriteByte(0);
        w.WriteUInt32((uint)view.Guild.GuildCode);
        w.WriteInt32(view.Guild.GuildPoint);
        w.WriteByte((byte)Math.Clamp((int)view.Guild.GuildMembers, 0, byte.MaxValue));
        WriteText(w, view.Post.RecruitTitle, TitleLength, text);
        WriteText(w, Date(view.Post.RecruitUpdatedAt), DateLength, text);
        WriteText(w, view.Post.RecruitContent, ContentLength, text);
        WriteText(w, view.Guild.GuildName, GuildNameLength, text);
        WriteText(w, view.MasterName, MasterNameLength, text);
        WriteText(w, Date(view.Guild.GuildCreatedAt), DateLength, text);
        return w.ToArray();
    }

    private static string Date(DateTimeOffset at) => at.UtcDateTime.ToString("yy-MM-dd", CultureInfo.InvariantCulture);

    private static string Text(ReadOnlySpan<byte> field, Encoding text)
    {
        var end = field.IndexOf((byte)0);
        return text.GetString(end < 0 ? field : field[..end]);
    }

    private static void WriteText(PacketWriter w, string value, int field, Encoding text)
    {
        var length = value.Length;
        while (length > 0 && text.GetByteCount(value.AsSpan(0, length)) > field - 1) length--;
        var bytes = text.GetBytes(value[..length]);
        w.WriteBytes(bytes);
        w.WriteZero(field - bytes.Length);
    }
}
