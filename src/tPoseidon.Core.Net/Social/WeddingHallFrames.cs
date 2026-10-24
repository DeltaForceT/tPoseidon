using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Social;

public sealed record WeddingHallMember(ulong CharacterId, string Name);

public sealed record WeddingHallDetail(ulong Wdid, uint MapId, ulong FirstPartnerId, ulong SecondPartnerId, IReadOnlyList<WeddingHallMember> Members);

public sealed record WeddingJoinOrder(ulong Wdid, ulong MasterId, uint MapId, uint PreviousZone, string Title, IReadOnlyList<ulong> Members);

public sealed record WeddingWarpOrder(uint Zone, string Note, IReadOnlyList<ulong> Members);

public sealed record WeddingEnterReport(uint Result, uint Zone, uint PreviousZone, string Note);

public sealed record WeddingDressOrder(ulong CharacterId, bool On, ushort DressItem);

public sealed record WeddingEnded(ulong Wdid, IReadOnlyList<ulong> Guests);

public static class WeddingHallError
{
    public const uint ClientMissing = 1;
    public const uint WrongPassword = 2;
    public const uint AlreadyInHall = 3;
    public const uint HallExists = 4;
    public const uint Full = 6;
    public const uint CoupleMissing = 7;
    public const uint NotMember = 8;
    public const uint HallMissing = 9;
    public const uint JoinHallMissing = 0x0A;
    public const uint ArticleMissing = 0x0B;
    public const uint NotMaster = 0x0C;
    public const uint LoverAbsent = 0x0D;
    public const uint EnterRequesterGone = 0x10;
    public const uint EnterCreateFailed = 0x0F;
    public const uint InvalidState = 0x11;
    public const uint KickMaster = 0x12;
    public const uint KickLover = 0x13;
    public const uint ArticlePending = 0x14;
    public const uint ArticleTextRefused = 0x16;
    public const uint Started = 0x17;

    public static uint FromEnterReport(uint result) => result switch
    {
        1 => ClientMissing,
        2 => LoverAbsent,
        3 => EnterRequesterGone,
        4 => Full,
        5 => EnterCreateFailed,
        6 => EnterRequesterGone,
        _ => result,
    };
}

public static class WeddingEnterResult
{
    public const uint Entered = 0;
    public const uint NotWeddingZone = 1;
    public const uint RequesterGone = 3;
    public const uint TooManyMembers = 4;
    public const uint CreateFailed = 5;
    public const uint MemberJoinFailed = 6;
}

public static class WeddingHallFrames
{
    public const ushort Error = 0x2E8D;
    public const ushort MakeHall = 0x2E8E;
    public const ushort HallMade = 0x2E8F;
    public const ushort JoinHall = 0x2E90;
    public const ushort HallJoined = 0x2E91;
    public const ushort ExitHall = 0x2E92;
    public const ushort HallExited = 0x2E93;
    public const ushort StartCeremony = 0x2E94;
    public const ushort KickFromHall = 0x2E98;
    public const ushort Kicked = 0x2E99;
    public const ushort HallDetailRequest = 0x2E9A;
    public const ushort HallDetail = 0x2E9B;
    public const ushort MemberJoined = 0x2E9C;
    public const ushort MemberExited = 0x2E9D;
    public const ushort MemberKicked = 0x2E9F;

    public const ushort OrderWarpMaster = 0xFC;
    public const ushort OrderJoinMember = 0xFD;
    public const ushort OrderWarpMember = 0xFE;
    public const ushort OrderDress = 0x101;
    public const ushort ReportEnterMember = 0xFB;
    public const ushort LinkEndWedding = 0x15CD;

    public const int TitleBytes = 0x40;
    public const int PasswordBytes = 0x1E;
    public const int ReportNoteBytes = 0x40;
    public const int MaxMembers = 0x5E;
    public const int MaxJoinOrderMembers = 0x5E;
    public const ushort WarpMemberZone = 0x22D;
    public const ushort FirstHallMap = 0x22E;
    public const ushort SecondHallMap = 0x22F;
    public const int MasterArrivalSeconds = 10;

    public static bool IsHallMap(ushort zone) => zone is FirstHallMap or SecondHallMap;

    public static bool TryReadMakeHall(ReadOnlySpan<byte> payload, Encoding text, out string title, out string password, out uint mapId)
    {
        ArgumentNullException.ThrowIfNull(text);
        title = password = string.Empty;
        mapId = 0;
        try
        {
            var r = new PacketReader(payload);
            title = Clip(r.ReadNulTerminated(text), TitleBytes - 1, text);
            password = Clip(r.ReadNulTerminated(text), PasswordBytes - 1, text);
            mapId = r.ReadUInt32();
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadJoin(ReadOnlySpan<byte> payload, Encoding text, out ulong wdid, out string password)
    {
        ArgumentNullException.ThrowIfNull(text);
        wdid = 0;
        password = string.Empty;
        try
        {
            var r = new PacketReader(payload);
            wdid = r.ReadUInt64();
            password = r.ReadNulTerminated(text);
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadHall(ReadOnlySpan<byte> payload, out ulong wdid)
    {
        wdid = 0;
        if (payload.Length < sizeof(ulong)) return false;
        wdid = new PacketReader(payload).ReadUInt64();
        return true;
    }

    public static bool TryReadKick(ReadOnlySpan<byte> payload, out ulong wdid, out IReadOnlyList<ulong> targets)
    {
        wdid = 0;
        targets = Array.Empty<ulong>();
        try
        {
            var r = new PacketReader(payload);
            wdid = r.ReadUInt64();
            var count = r.ReadUInt16();
            var list = new ulong[count];
            for (var i = 0; i < count; i++) list[i] = r.ReadUInt64();
            targets = list;
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static byte[] Code(uint code)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public static byte[] Hall(ulong wdid)
    {
        var w = new PacketWriter(8);
        w.WriteUInt64(wdid);
        return w.ToArray();
    }

    public static byte[] Detail(WeddingHallDetail detail, bool isMaster, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(32 + detail.Members.Count * 24);
        w.WriteUInt64(detail.Wdid);
        w.WriteUInt32(detail.MapId);
        w.WriteUInt64(detail.FirstPartnerId);
        w.WriteUInt64(detail.SecondPartnerId);
        w.WriteUInt16((ushort)detail.Members.Count);
        foreach (var member in detail.Members)
        {
            w.WriteUInt64(member.CharacterId);
            w.WriteNulTerminated(member.Name, text);
        }
        w.WriteByte(isMaster ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] Member(WeddingHallMember member, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(24);
        w.WriteUInt64(member.CharacterId);
        w.WriteNulTerminated(member.Name, text);
        return w.ToArray();
    }

    public static byte[] Character(ulong characterId)
    {
        var w = new PacketWriter(8);
        w.WriteUInt64(characterId);
        return w.ToArray();
    }

    public static byte[] WarpMaster(uint mapId)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(mapId);
        return w.ToArray();
    }

    public static bool TryReadWarpMaster(ReadOnlySpan<byte> data, out uint mapId)
    {
        mapId = 0;
        if (data.Length < sizeof(uint)) return false;
        mapId = new PacketReader(data).ReadUInt32();
        return true;
    }

    public static byte[] JoinMember(WeddingJoinOrder order, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(96 + order.Members.Count * 8);
        w.WriteUInt64(order.Wdid);
        w.WriteUInt64(order.MasterId);
        w.WriteUInt32(order.MapId);
        w.WriteUInt32(order.PreviousZone);
        w.WriteNulTerminated(order.Title, text);
        WriteMembers(w, order.Members);
        return w.ToArray();
    }

    public static WeddingJoinOrder? ReadJoinMember(ReadOnlySpan<byte> data, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var r = new PacketReader(data);
            var wdid = r.ReadUInt64();
            var master = r.ReadUInt64();
            var mapId = r.ReadUInt32();
            var previous = r.ReadUInt32();
            var title = r.ReadNulTerminated(text);
            return new WeddingJoinOrder(wdid, master, mapId, previous, title, ReadMembers(ref r));
        }
        catch (InvalidPacketException)
        {
            return null;
        }
    }

    public static byte[] WarpMember(WeddingWarpOrder order, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(72 + order.Members.Count * 8);
        w.WriteUInt32(order.Zone);
        w.WriteNulTerminated(order.Note, text);
        WriteMembers(w, order.Members);
        return w.ToArray();
    }

    public static WeddingWarpOrder? ReadWarpMember(ReadOnlySpan<byte> data, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var r = new PacketReader(data);
            var zone = r.ReadUInt32();
            var note = r.ReadNulTerminated(text);
            return new WeddingWarpOrder(zone, note, ReadMembers(ref r));
        }
        catch (InvalidPacketException)
        {
            return null;
        }
    }

    public static byte[] Dress(WeddingDressOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var w = new PacketWriter(11);
        w.WriteUInt64(order.CharacterId);
        w.WriteByte(order.On ? (byte)1 : (byte)0);
        w.WriteUInt16(order.DressItem);
        return w.ToArray();
    }

    public static WeddingDressOrder? ReadDress(ReadOnlySpan<byte> data)
    {
        if (data.Length < 11) return null;
        var r = new PacketReader(data);
        return new WeddingDressOrder(r.ReadUInt64(), r.ReadByte() != 0, r.ReadUInt16());
    }

    public static byte[] EnterReport(WeddingEnterReport report, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(12 + ReportNoteBytes);
        w.WriteUInt32(report.Result);
        w.WriteUInt32(report.Zone);
        w.WriteUInt32(report.PreviousZone);
        var note = text.GetBytes(Clip(report.Note, ReportNoteBytes - 1, text));
        w.WriteBytes(note);
        w.WriteZero(ReportNoteBytes - note.Length);
        return w.ToArray();
    }

    public static WeddingEnterReport? ReadEnterReport(ReadOnlySpan<byte> data, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (data.Length < 12 + ReportNoteBytes) return null;
        var r = new PacketReader(data);
        var result = r.ReadUInt32();
        var zone = r.ReadUInt32();
        var previous = r.ReadUInt32();
        var raw = data.Slice(12, ReportNoteBytes);
        var end = raw.IndexOf((byte)0);
        return new WeddingEnterReport(result, zone, previous, text.GetString(end < 0 ? raw : raw[..end]));
    }

    public static byte[] EndWedding(WeddingEnded ended)
    {
        ArgumentNullException.ThrowIfNull(ended);
        var w = new PacketWriter(10 + ended.Guests.Count * 8);
        w.WriteUInt64(ended.Wdid);
        WriteMembers(w, ended.Guests);
        return w.ToArray();
    }

    public static WeddingEnded? ReadEndWedding(ReadOnlySpan<byte> data)
    {
        try
        {
            var r = new PacketReader(data);
            var wdid = r.ReadUInt64();
            return new WeddingEnded(wdid, ReadMembers(ref r));
        }
        catch (InvalidPacketException)
        {
            return null;
        }
    }

    private static void WriteMembers(PacketWriter w, IReadOnlyList<ulong> members)
    {
        w.WriteUInt16((ushort)members.Count);
        foreach (var member in members) w.WriteUInt64(member);
    }

    private static ulong[] ReadMembers(ref PacketReader r)
    {
        var count = r.ReadUInt16();
        var members = new ulong[count];
        for (var i = 0; i < count; i++) members[i] = r.ReadUInt64();
        return members;
    }

    private static string Clip(string value, int maxBytes, Encoding text)
    {
        if (text.GetByteCount(value) <= maxBytes) return value;
        var length = value.Length;
        while (length > 0 && text.GetByteCount(value.AsSpan(0, length)) > maxBytes) length--;
        return value[..length];
    }
}
