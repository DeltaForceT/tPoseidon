namespace tPoseidon.Core.Net.World;

public static class AutoDrillFrames
{
    public static byte[] Player(ushort entityId)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(entityId);
        return w.ToArray();
    }

    public static byte[] Refused(ushort error)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(error);
        return w.ToArray();
    }
}
