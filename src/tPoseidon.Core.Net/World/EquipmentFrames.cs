namespace tPoseidon.Core.Net.World;

public static class EquipmentFrames
{
    public const int RequestSize = 9;

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
