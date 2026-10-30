using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Social;

public static class GameMasterFlightFrames
{
    public const int RequestSize = 1;
    public const int ChangedSize = 3;

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out bool flying)
    {
        flying = false;
        if (payload.Length != RequestSize) return false;
        flying = payload[0] == 1;
        return true;
    }

    public static byte[] Changed(ushort entityId, bool flying)
    {
        var w = new PacketWriter(ChangedSize);
        w.WriteUInt16(entityId);
        w.WriteByte(flying ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static bool TryReadChanged(ReadOnlySpan<byte> payload, out ushort entityId, out bool flying)
    {
        entityId = 0;
        flying = false;
        if (payload.Length != ChangedSize) return false;
        entityId = (ushort)(payload[0] | (payload[1] << 8));
        flying = payload[2] == 1;
        return true;
    }
}
