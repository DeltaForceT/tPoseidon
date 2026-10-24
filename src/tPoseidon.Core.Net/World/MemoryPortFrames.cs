using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.World;

public readonly record struct MemoryPortSave(ulong WireItemUid, ushort Slot, string Description);

public readonly record struct ItemUseWithData(ulong WireItemUid, byte[] Data);

public static class MemoryPortFrames
{
    public const int RecordSize = 34;
    public const int DescriptionBytes = 24;
    public const int DescriptionTextBytes = DescriptionBytes - 1;
    public const int OpenRequestSize = sizeof(ulong);
    public const int SlotRequestSize = sizeof(ulong) + sizeof(ushort);
    public const int MaxUseData = 0x100;

    public static bool TryReadOpen(ReadOnlySpan<byte> payload, out ulong wireItemUid)
    {
        wireItemUid = 0;
        if (payload.Length < OpenRequestSize) return false;
        wireItemUid = new PacketReader(payload).ReadUInt64();
        return true;
    }

    public static bool TryReadSave(ReadOnlySpan<byte> payload, Encoding encoding, out MemoryPortSave save)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        save = default;
        if (payload.Length < SlotRequestSize) return false;
        var r = new PacketReader(payload);
        var uid = r.ReadUInt64();
        var slot = r.ReadUInt16();
        var text = payload[SlotRequestSize..];
        var end = text.IndexOf((byte)0);
        save = new MemoryPortSave(uid, slot, encoding.GetString(end < 0 ? text : text[..end]));
        return true;
    }

    public static bool TryReadSlot(ReadOnlySpan<byte> payload, out ulong wireItemUid, out ushort slot)
    {
        wireItemUid = 0;
        slot = 0;
        if (payload.Length < SlotRequestSize) return false;
        var r = new PacketReader(payload);
        wireItemUid = r.ReadUInt64();
        slot = r.ReadUInt16();
        return true;
    }

    public static bool TryReadUseWithData(ReadOnlySpan<byte> payload, out ItemUseWithData use)
    {
        use = default;
        if (payload.Length < SlotRequestSize) return false;
        var r = new PacketReader(payload);
        var uid = r.ReadUInt64();
        var length = r.ReadUInt16();
        if (length > MaxUseData || r.Remaining < length) return false;
        use = new ItemUseWithData(uid, r.ReadBytes(length));
        return true;
    }

    public static string StoredDescription(string description, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(encoding);
        var bytes = encoding.GetBytes(description);
        return bytes.Length <= DescriptionTextBytes ? description : encoding.GetString(bytes, 0, DescriptionTextBytes);
    }

    public static byte[] Record(MemoryPort slot, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(RecordSize);
        WriteRecord(w, slot, encoding);
        return w.ToArray();
    }

    public static byte[] List(ulong wireItemUid, IReadOnlyList<MemoryPort> slots, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(9 + slots.Count * RecordSize);
        w.WriteUInt64(wireItemUid);
        w.WriteByte((byte)slots.Count);
        foreach (var slot in slots) WriteRecord(w, slot, encoding);
        return w.ToArray();
    }

    public static byte[] OpenFailed(uint reason)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(reason);
        return w.ToArray();
    }

    public static byte[] Deleted(ushort slot)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(slot);
        return w.ToArray();
    }

    public static byte[] WarpRefused(uint reason)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(reason);
        return w.ToArray();
    }

    private static void WriteRecord(PacketWriter w, MemoryPort slot, Encoding encoding)
    {
        w.WriteUInt16(slot.SlotNum);
        w.WriteUInt32((uint)(ushort)slot.Zone);
        w.WriteUInt16((ushort)Math.Clamp(slot.PositionX, 0, ushort.MaxValue));
        w.WriteUInt16((ushort)Math.Clamp(slot.PositionY, 0, ushort.MaxValue));
        var text = new byte[DescriptionBytes];
        var bytes = encoding.GetBytes(slot.Description ?? string.Empty);
        Array.Copy(bytes, text, Math.Min(bytes.Length, DescriptionTextBytes));
        w.WriteBytes(text);
    }
}
