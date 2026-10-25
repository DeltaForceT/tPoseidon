namespace tPoseidon.Core.Net.World;

public static class TargetHealthFrames
{
    public const int RequestSize = sizeof(ushort);
    public const int HealthSize = sizeof(ushort) + 2 * sizeof(uint);

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out ushort entityId)
    {
        entityId = 0;
        if (payload.Length < RequestSize) return false;
        entityId = new PacketReader(payload).ReadUInt16();
        return true;
    }

    public static byte[] Health(ushort entityId, long maxHp, long hp)
    {
        var w = new PacketWriter(HealthSize);
        w.WriteUInt16(entityId);
        w.WriteUInt32(Wire(maxHp));
        w.WriteUInt32(Wire(hp));
        return w.ToArray();
    }

    private static uint Wire(long value) => (uint)Math.Clamp(value, 0, int.MaxValue);
}
