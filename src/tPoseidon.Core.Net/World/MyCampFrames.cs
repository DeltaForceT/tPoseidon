using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Game.MyCamp;

namespace tPoseidon.Core.Net.World;

public readonly record struct MyCampChatInfo(string Title, byte MaxGuests, bool HasPassword, string Password);

public readonly record struct MyCampOpenRequest(bool WithChat, MyCampChatInfo Chat);

public readonly record struct MyCampEnterRequest(ushort HouseId, string Password);

public readonly record struct MyCampModeRequest(ushort ObjectId, byte Mode, MyCampChatInfo? Chat);

public static class MyCampFrames
{
    public const byte HouseKind = 0x0C;
    public const int SystemTimeSize = 16;
    public const byte GuestView = 1;

    public static byte[] OwnerEnter(MyCampShell shell, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(shell);
        var w = new PacketWriter(96);
        w.WriteByte(shell.Mode);
        w.WriteNulTerminated(shell.SlotName, text);
        w.WriteNulTerminated(shell.Title, text);
        w.WriteNulTerminated(shell.OwnerName, text);
        WriteSystemTime(w, shell.OpenedAt);
        w.WriteUInt16(0);
        return w.ToArray();
    }

    public static byte[] GuestEnter(MyCampShell shell, ushort x, ushort y, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(shell);
        var w = new PacketWriter(96);
        w.WriteUInt16(x);
        w.WriteUInt16(y);
        w.WriteNulTerminated(shell.SlotName, text);
        w.WriteNulTerminated(shell.Title, text);
        w.WriteNulTerminated(shell.OwnerName, text);
        w.WriteByte(GuestView);
        WriteSystemTime(w, shell.OpenedAt);
        w.WriteUInt16(0);
        return w.ToArray();
    }

    public static byte[] House(MyCampShell shell, ulong ownerUid, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(shell);
        var w = new PacketWriter(64);
        w.WriteUInt16(shell.HouseId);
        w.WriteByte(HouseKind);
        w.WriteUInt16(shell.Model);
        w.WriteByte(shell.HasPassword ? (byte)1 : (byte)0);
        w.WriteByte(shell.MaxGuests);
        w.WriteByte((byte)shell.Guests.Count);
        w.WriteUInt64(ownerUid);
        w.WriteByte(shell.Mode);
        w.WriteUInt16(shell.HouseX);
        w.WriteUInt16(shell.HouseY);
        w.WriteNulTerminated(shell.Title, text);
        w.WriteNulTerminated(shell.OwnerName, text);
        return w.ToArray();
    }

    public static byte[] HouseUpdate(MyCampShell shell, byte flags, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(shell);
        var w = new PacketWriter(48);
        w.WriteUInt16(shell.HouseId);
        w.WriteByte(flags);
        if ((flags & MyCampRules.HouseFlagState) != 0) w.WriteByte(shell.Mode);
        if ((flags & MyCampRules.HouseFlagChat) != 0)
        {
            w.WriteByte(shell.HasPassword ? (byte)1 : (byte)0);
            w.WriteByte(shell.MaxGuests);
            w.WriteByte((byte)shell.Guests.Count);
            w.WriteNulTerminated(shell.Title, text);
        }
        if ((flags & MyCampRules.HouseFlagModel) != 0) w.WriteUInt16(shell.Model);
        return w.ToArray();
    }

    public static byte[] GuestCount(MyCampShell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        var w = new PacketWriter(3);
        w.WriteUInt16(shell.HouseId);
        w.WriteByte((byte)shell.Guests.Count);
        return w.ToArray();
    }

    public static byte[] ChatInfo(MyCampShell shell, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(shell);
        var w = new PacketWriter(40);
        w.WriteByte(shell.MaxGuests);
        w.WriteNulTerminated(shell.Title, text);
        w.WriteByte(shell.HasPassword ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] ItemCount(int count)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16((ushort)Math.Clamp(count, 0, ushort.MaxValue));
        return w.ToArray();
    }

    public static byte[] Items(IReadOnlyList<MyCampItemRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var w = new PacketWriter(records.Sum(record => record.WireSize));
        foreach (var record in records)
        {
            w.WriteUInt64(record.ItemUid);
            w.WriteUInt64(record.ParentsUid);
            w.WriteUInt16(record.ItemIndex);
            w.WriteUInt64(record.LimitDate);
            w.WriteByte(record.State);
            if (record.State != MyCampRules.StatePlaced) continue;
            w.WriteInt16(record.X);
            w.WriteInt16(record.Y);
            w.WriteByte(record.Z);
            w.WriteByte(record.Direction);
        }
        return w.ToArray();
    }

    public static byte[] NewItemMoved(ulong itemUid, ushort itemIndex, ulong limitDate)
    {
        var w = new PacketWriter(18);
        w.WriteUInt64(itemUid);
        w.WriteUInt16(itemIndex);
        w.WriteUInt64(limitDate);
        return w.ToArray();
    }

    public static byte[] Return(ushort zone, ushort x, ushort y)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(zone);
        w.WriteUInt16(x);
        w.WriteUInt16(y);
        return w.ToArray();
    }

    public static byte[] Kicked(ushort zone, ushort x, ushort y, byte reason)
    {
        var w = new PacketWriter(7);
        w.WriteUInt16(zone);
        w.WriteUInt16(x);
        w.WriteUInt16(y);
        w.WriteByte(reason);
        return w.ToArray();
    }

    public static byte[] ObjectId(ushort id)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(id);
        return w.ToArray();
    }

    public static byte[] Text(string value, Encoding text)
    {
        var w = new PacketWriter(64);
        w.WriteNulTerminated(value, text);
        return w.ToArray();
    }

    public static bool TryReadText(ReadOnlySpan<byte> payload, int skip, Encoding text, out string value)
    {
        value = string.Empty;
        if (payload.Length <= skip) return false;
        try
        {
            var r = new PacketReader(payload[skip..]);
            value = r.ReadNulTerminated(text);
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static byte ReadTransferResult(ReadOnlySpan<byte> payload) =>
        payload.Length > 0 ? payload[0] : MyCampRules.TransferFailed;

    public static bool TryReadOpen(ReadOnlySpan<byte> payload, Encoding text, out MyCampOpenRequest request)
    {
        request = default;
        try
        {
            var r = new PacketReader(payload);
            var withChat = r.ReadByte() != 0;
            request = new MyCampOpenRequest(withChat, withChat ? ReadChat(ref r, text) : new MyCampChatInfo(string.Empty, MyCampRules.MaxGuests, false, string.Empty));
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadEnter(ReadOnlySpan<byte> payload, Encoding text, out MyCampEnterRequest request)
    {
        request = default;
        try
        {
            var r = new PacketReader(payload);
            var house = r.ReadUInt16();
            request = new MyCampEnterRequest(house, r.Remaining > 0 ? r.ReadNulTerminated(text) : string.Empty);
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadMode(ReadOnlySpan<byte> payload, Encoding text, out MyCampModeRequest request)
    {
        request = default;
        try
        {
            var r = new PacketReader(payload);
            var objectId = r.ReadUInt16();
            var mode = r.ReadByte();
            MyCampChatInfo? chat = null;
            if (mode == MyCampRules.Chat)
            {
                r.ReadInt16();
                r.ReadInt16();
                chat = ReadChat(ref r, text);
            }
            request = new MyCampModeRequest(objectId, mode, chat);
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadChatInfo(ReadOnlySpan<byte> payload, Encoding text, out MyCampChatInfo chat)
    {
        chat = default;
        try
        {
            var r = new PacketReader(payload);
            r.ReadUInt16();
            chat = ReadChat(ref r, text);
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static bool TryReadItems(ReadOnlySpan<byte> payload, out IReadOnlyList<MyCampItemRecord> records)
    {
        var list = new List<MyCampItemRecord>();
        records = list;
        try
        {
            var r = new PacketReader(payload);
            r.ReadUInt16();
            var count = r.ReadUInt16();
            for (var index = 0; index < count; index++)
            {
                var uid = r.ReadUInt64();
                var parents = r.ReadUInt64();
                var itemIndex = r.ReadUInt16();
                var limit = r.ReadUInt64();
                var state = r.ReadByte();
                list.Add(state == MyCampRules.StatePlaced
                    ? new MyCampItemRecord(uid, parents, itemIndex, limit, state, r.ReadInt16(), r.ReadInt16(), r.ReadByte(), r.ReadByte())
                    : new MyCampItemRecord(uid, parents, itemIndex, limit, state));
            }
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    private static MyCampChatInfo ReadChat(ref PacketReader r, Encoding text)
    {
        var title = r.ReadNulTerminated(text);
        var max = r.ReadByte();
        var hasPassword = r.ReadByte() == 1;
        var password = hasPassword ? r.ReadNulTerminated(text) : string.Empty;
        return new MyCampChatInfo(title, max, hasPassword, password);
    }

    private static void WriteSystemTime(PacketWriter w, DateTime at)
    {
        w.WriteUInt16((ushort)at.Year);
        w.WriteUInt16((ushort)at.Month);
        w.WriteUInt16((ushort)at.DayOfWeek);
        w.WriteUInt16((ushort)at.Day);
        w.WriteUInt16((ushort)at.Hour);
        w.WriteUInt16((ushort)at.Minute);
        w.WriteUInt16((ushort)at.Second);
        w.WriteUInt16((ushort)at.Millisecond);
    }
}
