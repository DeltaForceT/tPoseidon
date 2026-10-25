using System.Buffers.Binary;
using tPoseidon.Core.Game.Warp;

namespace tPoseidon.Core.Net.World;

public readonly record struct PortalRegionRequest(PortalRegionClick Click, ushort X, ushort Y, byte MoveType);

public static class PortalClickFrames
{
    public const int RequestSize = 21;

    public static bool TryRead(ReadOnlySpan<byte> payload, out PortalRegionRequest request)
    {
        request = default;
        if (payload.Length < RequestSize) return false;
        request = new PortalRegionRequest(
            new PortalRegionClick(
                BinaryPrimitives.ReadInt32LittleEndian(payload),
                BinaryPrimitives.ReadInt32LittleEndian(payload[4..]),
                BinaryPrimitives.ReadInt32LittleEndian(payload[8..]),
                BinaryPrimitives.ReadInt32LittleEndian(payload[12..])),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[16..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[18..]),
            payload[20]);
        return true;
    }

    public static byte[] Result(int code, int region)
    {
        var w = new PacketWriter(3 * sizeof(int));
        w.WriteUInt32((uint)code);
        w.WriteUInt32((uint)region);
        w.WriteUInt32(0);
        return w.ToArray();
    }
}
