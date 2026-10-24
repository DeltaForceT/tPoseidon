namespace tPoseidon.Core.Net.World;

public readonly record struct ReleaseEntry(byte ItemClass, ulong WireItemUid, byte Slot);

public static class EquipmentFrames
{
    public const int RequestSize = 9;
    public const int ReleaseEntrySize = 10;
    public const int ReleaseListMost = 30;
    public const byte CommonItemClass = 1;
    public const byte RareItemClass = 2;

    public static bool TryReadReleaseList(ReadOnlySpan<byte> payload, out IReadOnlyList<ReleaseEntry> entries)
    {
        entries = Array.Empty<ReleaseEntry>();
        if (payload.Length < sizeof(ushort)) return false;
        var count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (count > ReleaseListMost || payload.Length < sizeof(ushort) + count * ReleaseEntrySize) return false;
        var read = new List<ReleaseEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = payload.Slice(sizeof(ushort) + i * ReleaseEntrySize, ReleaseEntrySize);
            read.Add(new ReleaseEntry(entry[0], System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(entry[1..]), entry[9]));
        }
        entries = read;
        return true;
    }

    public static byte[] ReleaseList(IReadOnlyList<ReleaseEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var w = new PacketWriter(sizeof(ushort) + entries.Count * ReleaseEntrySize);
        w.WriteUInt16((ushort)entries.Count);
        foreach (var entry in entries)
        {
            w.WriteByte(entry.ItemClass);
            w.WriteUInt64(entry.WireItemUid);
            w.WriteByte(entry.Slot);
        }
        return w.ToArray();
    }

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out ulong wireItemUid, out byte slot)
    {
        wireItemUid = 0;
        slot = 0;
        if (payload.Length < RequestSize) return false;
        wireItemUid = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(payload);
        slot = payload[8];
        return true;
    }

    public static byte[] ItemInSlot(ulong wireItemUid, byte slot)
    {
        var w = new PacketWriter(RequestSize);
        w.WriteUInt64(wireItemUid);
        w.WriteByte(slot);
        return w.ToArray();
    }

    public static byte[] Refused(ulong wireItemUid)
    {
        var w = new PacketWriter(8);
        w.WriteUInt64(wireItemUid);
        return w.ToArray();
    }

    public static byte[] LookAdded(ushort playerEntityId, uint itemId, uint lookItemId)
    {
        var w = new PacketWriter(10);
        w.WriteUInt16(playerEntityId);
        w.WriteUInt32(itemId);
        w.WriteUInt32(lookItemId);
        return w.ToArray();
    }

    public static byte[] LookRemoved(ushort playerEntityId, uint itemId)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(playerEntityId);
        w.WriteUInt32(itemId);
        return w.ToArray();
    }

    public static byte[] SetChanged(ushort playerEntityId, int setId, bool on)
    {
        var w = new PacketWriter(9);
        w.WriteUInt32(playerEntityId);
        w.WriteUInt32((uint)setId);
        w.WriteByte(on ? (byte)1 : (byte)0);
        return w.ToArray();
    }
}
