namespace tPoseidon.Core.Net.World;

public static class EntityMotionFrames
{
    public const int PayloadSize = 3;

    public static byte[] Emote(ushort entityId, byte code) => Encode(entityId, code);

    public static byte[] Posture(ushort entityId, byte code) => Encode(entityId, code);

    public const int FaceRequestSize = 4;

    public static bool TryReadFaceRequest(ReadOnlySpan<byte> payload, out ushort x, out ushort y)
    {
        x = 0;
        y = 0;
        if (payload.Length != FaceRequestSize)
        {
            return false;
        }

        x = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload);
        y = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]);
        return true;
    }

    public static byte[] Face(ushort entityId, ushort x, ushort y)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(entityId);
        w.WriteUInt16(x);
        w.WriteUInt16(y);
        return w.ToArray();
    }

    private static byte[] Encode(ushort entityId, byte code)
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(entityId);
        w.WriteByte(code);
        return w.ToArray();
    }
}
