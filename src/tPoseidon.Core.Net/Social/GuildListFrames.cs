using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public readonly record struct GuildListRequest(ushort Page, byte Sort, bool Natural);

public static class GuildListFrames
{
    public const ushort Request = (ushort)Opcode.C2S_GUILD_LIST;
    public const ushort Reply = (ushort)Opcode.S2C_GUILD_LIST;
    public const int RequestLength = 4;
    public const int PageSize = 10;
    public const int NameLength = 25;
    public const int MasterNameLength = 20;
    public const int DateLength = 9;
    public const int EntryLength = 84;
    public const byte SortCreated = 0;
    public const byte SortPoint = 1;
    public const byte SortName = 2;
    public const byte SortMembers = 3;
    public const byte SortRanking = 4;
    public const byte SortIncreaseFirst = 5;
    public const byte SortIncreaseSecond = 6;

    public static GuildListRequest? ReadRequest(ReadOnlySpan<byte> payload) =>
        payload.Length < RequestLength
            ? null
            : new GuildListRequest(BinaryPrimitives.ReadUInt16LittleEndian(payload), payload[2], payload[3] != 0);

    public static IReadOnlyList<GuildListEntry> Sorted(IReadOnlyList<GuildListEntry> guilds, byte sort, bool natural)
    {
        IOrderedEnumerable<GuildListEntry> ordered = sort switch
        {
            SortPoint => guilds.OrderByDescending(e => e.Guild.GuildPoint),
            SortName => guilds.OrderBy(e => e.Guild.GuildName, StringComparer.Ordinal),
            SortMembers => guilds.OrderByDescending(e => e.Guild.GuildMembers),
            SortRanking => guilds.OrderBy(e => e.Guild.GuildRanking),
            SortIncreaseFirst or SortIncreaseSecond => guilds.OrderByDescending(e => e.Guild.GuildPointIncrease),
            _ => guilds.OrderByDescending(e => e.Guild.GuildCode),
        };
        var list = ordered.ThenBy(e => e.Guild.GuildCode).ToList();
        if (!natural) list.Reverse();
        return list;
    }

    public static byte[] ReplyFor(IReadOnlyList<GuildListEntry> guilds, GuildListRequest request, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(guilds);
        ArgumentNullException.ThrowIfNull(text);
        var sorted = Sorted(guilds, request.Sort, request.Natural);
        var skip = Math.Min(request.Page * PageSize, sorted.Count);
        var page = sorted.Skip(skip).Take(PageSize).ToArray();
        var w = new PacketWriter(3 + page.Length * EntryLength);
        w.WriteUInt16((ushort)Math.Min(sorted.Count, ushort.MaxValue));
        w.WriteByte((byte)page.Length);
        foreach (var entry in page) WriteEntry(w, entry, text);
        return w.ToArray();
    }

    private static void WriteEntry(PacketWriter w, GuildListEntry entry, Encoding text)
    {
        var guild = entry.Guild;
        w.WriteUInt32((uint)guild.GuildCode);
        w.WriteInt32(guild.GuildPoint);
        w.WriteInt32(guild.GuildPointIncrease);
        w.WriteInt32(guild.GuildRanking);
        w.WriteInt32(guild.GuildRankingPoint);
        WriteText(w, guild.GuildName, NameLength, text);
        WriteText(w, entry.MasterName, MasterNameLength, text);
        w.WriteUInt16((ushort)Math.Clamp(entry.MasterLevel, 0, ushort.MaxValue));
        WriteText(w, guild.GuildCreatedAt.UtcDateTime.ToString("yy-MM-dd", CultureInfo.InvariantCulture), DateLength, text);
        w.WriteByte((byte)Math.Clamp((int)guild.GuildMembers, 0, byte.MaxValue));
        w.WriteByte(entry.Emblem.Template);
        w.WriteByte(entry.Emblem.Colour);
        w.WriteUInt32(entry.Emblem.Icon);
        w.WriteByte(0);
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
