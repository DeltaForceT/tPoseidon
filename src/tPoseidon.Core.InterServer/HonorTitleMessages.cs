using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed record HonorTitleAdd(ushort ZoneId, ushort EntityId, IReadOnlyList<HonorTitleRecord> Titles);

public sealed record HonorTitleAdded(ushort ZoneId, ushort EntityId, IReadOnlyList<ushort> TitleIds);

public static class HonorTitleMessages
{
    public const int EntrySize = 11;

    public static byte[] EncodeLogin(HonorTitleLogin login)
    {
        ArgumentNullException.ThrowIfNull(login);
        var w = new PacketWriter(8 + login.Titles.Count * sizeof(ushort));
        w.WriteByte((byte)login.Titles.Count);
        foreach (var title in login.Titles) w.WriteUInt16(title);
        w.WriteUInt16(login.ProfileTitleType);
        w.WriteUInt32(login.ProfileTitleId);
        return w.ToArray();
    }

    public static HonorTitleLogin DecodeLogin(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var count = r.ReadByte();
        var titles = new ushort[count];
        for (var i = 0; i < count; i++) titles[i] = r.ReadUInt16();
        return new HonorTitleLogin(titles, r.ReadUInt16(), r.ReadUInt32());
    }

    public static byte[] EncodeAdd(HonorTitleAdd add)
    {
        ArgumentNullException.ThrowIfNull(add);
        var w = new PacketWriter(5 + add.Titles.Count * EntrySize);
        w.WriteUInt16(add.ZoneId);
        w.WriteUInt16(add.EntityId);
        w.WriteByte((byte)add.Titles.Count);
        foreach (var title in add.Titles)
        {
            w.WriteUInt16(title.TitleId);
            w.WriteUInt32(title.QuestId);
            w.WriteByte(title.QuestType);
            w.WriteUInt16(title.ClearTime);
            w.WriteUInt16(title.HunterCount);
        }
        return w.ToArray();
    }

    public static HonorTitleAdd DecodeAdd(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var zone = r.ReadUInt16();
        var entity = r.ReadUInt16();
        var count = r.ReadByte();
        var titles = new HonorTitleRecord[count];
        for (var i = 0; i < count; i++)
        {
            titles[i] = new HonorTitleRecord(r.ReadUInt16(), r.ReadUInt32(), r.ReadByte(), r.ReadUInt16(), r.ReadUInt16());
        }
        return new HonorTitleAdd(zone, entity, titles);
    }

    public static byte[] EncodeAdded(HonorTitleAdded added)
    {
        ArgumentNullException.ThrowIfNull(added);
        var w = new PacketWriter(5 + added.TitleIds.Count * sizeof(ushort));
        w.WriteUInt16(added.ZoneId);
        w.WriteUInt16(added.EntityId);
        w.WriteByte((byte)added.TitleIds.Count);
        foreach (var id in added.TitleIds) w.WriteUInt16(id);
        return w.ToArray();
    }

    public static HonorTitleAdded DecodeAdded(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var zone = r.ReadUInt16();
        var entity = r.ReadUInt16();
        var count = r.ReadByte();
        var ids = new ushort[count];
        for (var i = 0; i < count; i++) ids[i] = r.ReadUInt16();
        return new HonorTitleAdded(zone, entity, ids);
    }

    public static byte[] EncodeProfileTitle(ushort titleType, ulong titleId)
    {
        var w = new PacketWriter(10);
        w.WriteUInt16(titleType);
        w.WriteUInt64(titleId);
        return w.ToArray();
    }

    public static (ushort TitleType, ulong TitleId) DecodeProfileTitle(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt16(), r.ReadUInt64());
    }
}
