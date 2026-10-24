namespace tPoseidon.Core.Net.World;

public static class EntityMotionFrames
{
    public const int PayloadSize = 3;

    public static byte[] Emote(ushort entityId, byte code) => Encode(entityId, code);

    public static byte[] Posture(ushort entityId, byte code) => Encode(entityId, code);

    private static byte[] Encode(ushort entityId, byte code)
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(entityId);
        w.WriteByte(code);
        return w.ToArray();
    }
}
