using System.Buffers.Binary;

namespace tPoseidon.Core.Game.Warp;

public static class ZoneRequestRules
{
    public const ushort Opcode = 0x0012;
    public const int PayloadLength = 3;
    public const ushort FirstZone = 1;
    public const ushort LastZone = 999;

    public static bool TryParse(ReadOnlySpan<byte> payload, out ushort zone, out byte changeType)
    {
        zone = 0;
        changeType = 0;
        if (payload.Length < PayloadLength) return false;
        zone = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        changeType = payload[2];
        return zone >= FirstZone && zone <= LastZone;
    }
}
