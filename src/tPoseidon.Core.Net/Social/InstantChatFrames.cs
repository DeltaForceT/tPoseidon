using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Social;

public sealed record InstantChatMember(ulong CharacterId, ushort ConnectionId, string Name, ushort Value);

public readonly record struct InstantChatCreate(string Title, string Password, ushort MaxMembers);

public readonly record struct InstantChatEnter(ushort RoomId, string Password);

public readonly record struct InstantChatLine(ushort RoomId, string Text, ushort Style);

public static class InstantChatFrames
{
    public const byte Kind = 1;

    public const ushort Chat = 0x1774;
    public const ushort Create = 0x177D;
    public const ushort Created = 0x177E;
    public const ushort CreateRefused = 0x177F;
    public const ushort Close = 0x1780;
    public const ushort CloseRefused = 0x1781;
    public const ushort SetTitle = 0x1783;
    public const ushort SetTitleRefused = 0x1784;
    public const ushort TitleSet = 0x1785;
    public const ushort SetMaxMembers = 0x1786;
    public const ushort SetMaxMembersRefused = 0x1787;
    public const ushort MaxMembersSet = 0x1788;
    public const ushort SetPassword = 0x1789;
    public const ushort SetPasswordRefused = 0x178A;
    public const ushort PasswordSet = 0x178B;
    public const ushort Enter = 0x178C;
    public const ushort EnterRefused = 0x178D;
    public const ushort Entered = 0x178E;
    public const ushort Leave = 0x178F;
    public const ushort ChatLine = 0x1792;
    public const ushort TitleChanged = 0x1794;
    public const ushort MaxMembersChanged = 0x1795;
    public const ushort PasswordChanged = 0x1796;
    public const ushort MemberIn = 0x1797;
    public const ushort MemberOut = 0x1798;
    public const ushort Closed = 0x1799;
    public const ushort SignShown = 0x179D;
    public const ushort SignRemoved = 0x179E;
    public const ushort RoomInfoRequest = 0x179F;
    public const ushort RoomInfo = 0x17A0;
    public const ushort RoomMissing = 0x17A1;
    public const ushort Kick = 0x17A2;
    public const ushort KickDone = 0x17A3;
    public const ushort KickRefused = 0x17A4;
    public const ushort Kicked = 0x17A5;

    public const ushort OrderMyNotice = 0x1F;
    public const ushort ReportLevel = 0x1F;
    public const ushort OrderReportLevel = 0x20;
    public const ushort OrderNoticeState = 0x21;

    public const byte RoomNotMade = 1;
    public const byte NoRoom = 3;
    public const byte NotInRoom = 4;
    public const byte NotMaster = 5;
    public const byte SameMember = 6;
    public const byte RoomFull = 8;
    public const byte WrongPassword = 9;
    public const byte LevelTooLow = 10;
    public const byte AlreadyInRoom = 12;

    public const int TitleBytes = 0x100;
    public const int PasswordBytes = 0x10;

    public static bool TryReadCreate(ReadOnlySpan<byte> body, Encoding text, out InstantChatCreate create)
    {
        create = default;
        try
        {
            var r = new PacketReader(body);
            if (r.ReadByte() != Kind) return false;
            var title = r.ReadNulTerminated(text);
            var password = r.ReadNulTerminated(text);
            create = new InstantChatCreate(Limit(title, TitleBytes, text), Limit(password, PasswordBytes, text), r.ReadUInt16());
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadEnter(ReadOnlySpan<byte> body, Encoding text, out InstantChatEnter enter)
    {
        enter = default;
        try
        {
            var r = new PacketReader(body);
            if (r.ReadByte() != Kind) return false;
            enter = new InstantChatEnter(r.ReadUInt16(), r.ReadNulTerminated(text));
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadLine(ReadOnlySpan<byte> body, Encoding text, out InstantChatLine line)
    {
        line = default;
        try
        {
            var r = new PacketReader(body);
            var roomId = r.ReadUInt16();
            var said = r.ReadNulTerminated(text);
            line = new InstantChatLine(roomId, said, r.ReadUInt16());
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadRoomAndText(ReadOnlySpan<byte> body, Encoding text, out ushort roomId, out string value)
    {
        roomId = 0;
        value = string.Empty;
        try
        {
            var r = new PacketReader(body);
            roomId = r.ReadUInt16();
            value = r.ReadNulTerminated(text);
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadRoomAndValue(ReadOnlySpan<byte> body, out ushort roomId, out ushort value)
    {
        roomId = 0;
        value = 0;
        if (body.Length < 2 * sizeof(ushort)) return false;
        var r = new PacketReader(body);
        roomId = r.ReadUInt16();
        value = r.ReadUInt16();
        return true;
    }

    public static bool TryReadRoom(ReadOnlySpan<byte> body, out ushort roomId)
    {
        roomId = 0;
        if (body.Length < sizeof(ushort)) return false;
        roomId = new PacketReader(body).ReadUInt16();
        return true;
    }

    public static string Limit(string value, int bytes, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(text);
        var encoded = text.GetBytes(value);
        return encoded.Length < bytes ? value : text.GetString(encoded, 0, bytes - 1);
    }

    public static byte[] CreatedBody(ushort roomId, string title, bool hasPassword, ushort maxMembers, Encoding text)
    {
        var w = new PacketWriter(16 + title.Length * 2);
        w.WriteByte(Kind);
        w.WriteUInt16(roomId);
        w.WriteNulTerminated(title, text);
        w.WriteByte(hasPassword ? (byte)1 : (byte)0);
        w.WriteUInt16(maxMembers);
        return w.ToArray();
    }

    public static byte[] KindAndCode(byte code) => new[] { Kind, code };

    public static byte[] EnteredBody(ushort roomId, string title, bool hasPassword, ushort maxMembers, IReadOnlyList<(InstantChatMember Member, bool Master)> members, Encoding text)
    {
        var w = new PacketWriter(32 + title.Length * 2 + members.Count * 40);
        w.WriteByte(Kind);
        w.WriteUInt16(roomId);
        w.WriteNulTerminated(title, text);
        w.WriteByte(hasPassword ? (byte)1 : (byte)0);
        w.WriteUInt16((ushort)members.Count);
        w.WriteUInt16(maxMembers);
        foreach (var (member, master) in members) WriteMember(w, member, master, text);
        return w.ToArray();
    }

    public static byte[] MemberInBody(ushort roomId, InstantChatMember member, bool master, Encoding text)
    {
        var w = new PacketWriter(16 + member.Name.Length * 2);
        w.WriteUInt16(roomId);
        WriteMember(w, member, master, text);
        return w.ToArray();
    }

    public static byte[] ChatLineBody(ushort roomId, ushort senderConnectionId, string said, ushort style, Encoding text)
    {
        var w = new PacketWriter(8 + said.Length * 2);
        w.WriteUInt16(roomId);
        w.WriteUInt16(senderConnectionId);
        w.WriteNulTerminated(said, text);
        w.WriteUInt16(style);
        return w.ToArray();
    }

    public static byte[] RoomAndText(ushort roomId, string value, Encoding text)
    {
        var w = new PacketWriter(4 + value.Length * 2);
        w.WriteUInt16(roomId);
        w.WriteNulTerminated(value, text);
        return w.ToArray();
    }

    public static byte[] RoomAndByte(ushort roomId, byte value)
    {
        var w = new PacketWriter(3);
        w.WriteUInt16(roomId);
        w.WriteByte(value);
        return w.ToArray();
    }

    public static byte[] Room(ushort roomId)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(roomId);
        return w.ToArray();
    }

    public static byte[] RoomAndMember(ushort roomId, ushort connectionId)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(roomId);
        w.WriteUInt16(connectionId);
        return w.ToArray();
    }

    public static byte[] KickRefusedBody(ushort roomId, ushort target, byte code)
    {
        var w = new PacketWriter(5);
        w.WriteUInt16(roomId);
        w.WriteUInt16(target);
        w.WriteByte(code);
        return w.ToArray();
    }

    public static byte[] RoomInfoBody(ushort roomId, string title, bool hasPassword, Encoding text)
    {
        var w = new PacketWriter(8 + title.Length * 2);
        w.WriteUInt16(roomId);
        w.WriteByte(Kind);
        w.WriteNulTerminated(title, text);
        w.WriteByte(hasPassword ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] SignOrderData(ushort roomId) => Room(roomId);

    public static byte[] StateOrderData(bool master) => new[] { master ? (byte)1 : (byte)0 };

    public static byte[] LevelReportData(ushort level) => Room(level);

    public static bool TryReadLevelReport(ReadOnlySpan<byte> data, out ushort level) => TryReadRoom(data, out level);

    public static bool TryReadSignOrder(ReadOnlySpan<byte> data, out ushort roomId) => TryReadRoom(data, out roomId);

    public static byte[] SignShownBody(ushort entityId, ushort roomId) => RoomAndMember(entityId, roomId);

    public static byte[] SignRemovedBody(ushort entityId) => Room(entityId);

    private static void WriteMember(PacketWriter w, InstantChatMember member, bool master, Encoding text)
    {
        w.WriteUInt16(member.ConnectionId);
        w.WriteNulTerminated(member.Name, text);
        w.WriteUInt16(member.Value);
        w.WriteUInt16(master ? (ushort)1 : (ushort)0);
    }
}
