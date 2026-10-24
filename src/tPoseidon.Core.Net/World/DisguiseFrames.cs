namespace tPoseidon.Core.Net.World;

public static class DisguiseFrames
{
    public static byte[] Disguised(ushort entityId, int shownType)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(entityId);
        w.WriteUInt32((uint)shownType);
        return w.ToArray();
    }

    public static byte[] Removed(ushort entityId, byte effect)
    {
        var w = new PacketWriter(3);
        w.WriteUInt16(entityId);
        w.WriteByte(effect);
        return w.ToArray();
    }
}
