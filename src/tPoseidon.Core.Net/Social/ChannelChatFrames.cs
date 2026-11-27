using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Social;

public readonly record struct ChannelChatCreate(byte Kind, string Title, string Password, ushort MaxMembers);

public readonly record struct ChannelChatEnter(byte Kind, ushort RoomId, string Password);

public readonly record struct ChannelChatListRequest(byte Kind, byte Page);

public readonly record struct ChannelChatBlockRequest(ushort RoomId, string Name, bool Add);

public readonly record struct ChannelChatMessage(ushort RoomId, string Name, string Text);

public static class ChannelChatFrames
{
    public const byte FirstKind = 2;
    public const byte LastKind = 6;
    public const byte AllKind = 7;

    public const ushort List = 0x177A;
    public const ushort ListRefused = 0x177B;
    public const ushort ListEnd = 0x177C;
    public const ushort ListHeader = 0x17A6;
    public const ushort ListEntry = 0x17A7;
    public const ushort ListEntryChanges = 0x17B9;
    public const ushort Search = 0x17B3;
    public const ushort Reserved = 0x17B6;
    public const ushort Delegate = 0x17A9;
    public const ushort DelegateDone = 0x17AA;
    public const ushort DelegateRefused = 0x17AB;
    public const ushort Delegated = 0x17A8;
    public const ushort Block = 0x17AC;
    public const ushort BlockDone = 0x17AD;
    public const ushort BlockRefused = 0x17AE;
    public const ushort BlockChanged = 0x17B2;
    public const ushort BlockList = 0x17AF;
    public const ushort BlockListed = 0x17B0;
    public const ushort BlockListMissing = 0x17B1;
    public const ushort Message = 0x17BA;
    public const ushort Messaged = 0x17BB;

    public const byte ListKindUnknown = 0x0E;
    public const byte ListEmpty = 0x0F;
    public const byte ListPastEnd = 0x10;
    public const byte BlockExists = 0x0D;
    public const byte BlockFull = 0x12;
    public const byte Blocked = 0x11;

    public static bool IsRoomKind(byte kind) => kind is >= FirstKind and <= LastKind;

    public static bool IsListKind(byte kind) => kind is >= FirstKind and <= AllKind;

    public static bool TryReadCreate(ReadOnlySpan<byte> body, Encoding text, out ChannelChatCreate create)
    {
        create = default;
        try
        {
            var r = new PacketReader(body);
            var kind = r.ReadByte();
            var title = r.ReadNulTerminated(text);
            var password = r.ReadNulTerminated(text);
            create = new ChannelChatCreate(kind, InstantChatFrames.Limit(title, InstantChatFrames.TitleBytes, text),
                InstantChatFrames.Limit(password, InstantChatFrames.PasswordBytes, text), r.ReadUInt16());
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadEnter(ReadOnlySpan<byte> body, Encoding text, out ChannelChatEnter enter)
    {
        enter = default;
        try
        {
            var r = new PacketReader(body);
            var kind = r.ReadByte();
            var roomId = r.ReadUInt16();
            enter = new ChannelChatEnter(kind, roomId, r.ReadNulTerminated(text));
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadKind(ReadOnlySpan<byte> body, out byte kind)
    {
        kind = 0;
        if (body.Length < 1) return false;
        kind = body[0];
        return true;
    }

    public static bool TryReadList(ReadOnlySpan<byte> body, out ChannelChatListRequest request)
    {
        request = default;
        if (body.Length < 2) return false;
        request = new ChannelChatListRequest(body[0], body[1]);
        return true;
    }

    public static bool TryReadBlock(ReadOnlySpan<byte> body, Encoding text, out ChannelChatBlockRequest request)
    {
        request = default;
        try
        {
            var r = new PacketReader(body);
            var roomId = r.ReadUInt16();
            var name = r.ReadNulTerminated(text);
            request = new ChannelChatBlockRequest(roomId, name, r.ReadByte() != 0);
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadMessage(ReadOnlySpan<byte> body, Encoding text, out ChannelChatMessage message)
    {
        message = default;
        try
        {
            var r = new PacketReader(body);
            var roomId = r.ReadUInt16();
            var name = r.ReadNulTerminated(text);
            message = new ChannelChatMessage(roomId, name, r.ReadNulTerminated(text));
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static byte[] CreatedBody(byte kind, ushort roomId, string title, bool hasPassword, ushort maxMembers, Encoding text)
    {
        var w = new PacketWriter(16 + title.Length * 2);
        w.WriteByte(kind);
        w.WriteUInt16(roomId);
        w.WriteNulTerminated(title, text);
        w.WriteByte(hasPassword ? (byte)1 : (byte)0);
        w.WriteUInt16(maxMembers);
        return w.ToArray();
    }

    public static byte[] KindAndCode(byte kind, byte code) => new[] { kind, code };

    public static byte[] EnteredBody(byte kind, ushort roomId, string title, bool hasPassword, ushort maxMembers,
        IReadOnlyList<(InstantChatMember Member, bool Master)> members, Encoding text)
    {
        var w = new PacketWriter(32 + title.Length * 2 + members.Count * 40);
        w.WriteByte(kind);
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

    public static byte[] ListHeaderBody(byte kind, byte page, byte pages, byte entries) => new[] { kind, page, pages, entries };

    public static byte[] ListEntryBody(byte roomKind, ushort number, ushort roomId, string title, byte members, byte maxMembers, bool hasPassword, Encoding text)
    {
        var w = new PacketWriter(16 + title.Length * 2);
        w.WriteByte(roomKind);
        w.WriteUInt16(number);
        w.WriteUInt16(roomId);
        w.WriteNulTerminated(title, text);
        w.WriteByte(members);
        w.WriteByte(maxMembers);
        w.WriteByte(hasPassword ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] ListEntryChangesBody(byte roomKind, ushort number, ushort roomId, byte mask, string? title,
        byte? members, byte? maxMembers, bool? hasPassword, Encoding text)
    {
        var w = new PacketWriter(16 + (title?.Length ?? 0) * 2);
        w.WriteByte(roomKind);
        w.WriteUInt16(number);
        w.WriteUInt16(roomId);
        w.WriteByte(mask);
        if (title is not null) w.WriteNulTerminated(title, text);
        if (members is { } memberCount) w.WriteByte(memberCount);
        if (maxMembers is { } max) w.WriteByte(max);
        if (hasPassword is { } password) w.WriteByte(password ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] KindPageAndCode(byte kind, byte page, byte code) => new[] { kind, page, code };

    public static byte[] KindAndPage(byte kind, byte page) => new[] { kind, page };

    public static byte[] RoomAndMemberAndCode(ushort roomId, ushort member, byte code)
    {
        var w = new PacketWriter(5);
        w.WriteUInt16(roomId);
        w.WriteUInt16(member);
        w.WriteByte(code);
        return w.ToArray();
    }

    public static byte[] BlockBody(ushort roomId, string name, bool add, Encoding text)
    {
        var w = new PacketWriter(8 + name.Length * 2);
        w.WriteUInt16(roomId);
        w.WriteNulTerminated(name, text);
        w.WriteByte(add ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] BlockRefusedBody(ushort roomId, string name, bool add, byte code, Encoding text)
    {
        var w = new PacketWriter(8 + name.Length * 2);
        w.WriteUInt16(roomId);
        w.WriteNulTerminated(name, text);
        w.WriteByte(add ? (byte)1 : (byte)0);
        w.WriteByte(code);
        return w.ToArray();
    }

    public static byte[] BlockListBody(ushort roomId, IReadOnlyList<string> names, Encoding text)
    {
        var w = new PacketWriter(8 + names.Count * 24);
        w.WriteUInt16(roomId);
        w.WriteByte((byte)names.Count);
        foreach (var name in names) w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] MessagedBody(ushort roomId, string said, string senderName, Encoding text)
    {
        var w = new PacketWriter(8 + said.Length * 2 + senderName.Length * 2);
        w.WriteUInt16(roomId);
        w.WriteNulTerminated(said, text);
        w.WriteNulTerminated(senderName, text);
        return w.ToArray();
    }

    private static void WriteMember(PacketWriter w, InstantChatMember member, bool master, Encoding text)
    {
        w.WriteUInt16(member.ConnectionId);
        w.WriteNulTerminated(member.Name, text);
        w.WriteUInt16(member.Value);
        w.WriteUInt16(master ? (ushort)1 : (ushort)0);
    }
}
