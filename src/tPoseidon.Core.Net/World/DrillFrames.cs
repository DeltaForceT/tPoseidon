using System.Buffers.Binary;
using tPoseidon.Core.Game.Drilling;

namespace tPoseidon.Core.Net.World;

public readonly record struct DrillSpot(ushort X, ushort Y, int MapDepth, int LaidDepth, int Nature);

public static class DrillFrames
{
    public const int StartRequestSize = 5;
    public const int CancelRequestSize = 4;
    public const int EndRequestSize = 8;
    public const byte DepthFlag = 0;
    public const ushort CurrentLife = ushort.MaxValue;

    public static bool TryReadStart(ReadOnlySpan<byte> payload, out ushort x, out ushort y, out byte moveType)
    {
        x = 0;
        y = 0;
        moveType = 0;
        if (payload.Length < StartRequestSize) return false;
        x = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        y = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]);
        moveType = payload[4];
        return true;
    }

    public static int CancelPresses(ReadOnlySpan<byte> payload) =>
        payload.Length < CancelRequestSize ? 0 : BinaryPrimitives.ReadUInt16LittleEndian(payload);

    public static bool TryReadEnd(ReadOnlySpan<byte> payload, out int presses, out int depth, out ushort check)
    {
        presses = 0;
        depth = 0;
        check = 0;
        if (payload.Length < EndRequestSize) return false;
        presses = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        depth = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]);
        check = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]);
        return true;
    }

    public static byte[] Spot(ushort playerEntityId, DrillSpot spot, ushort token, bool lucky, int gaugePercent)
    {
        var w = new PacketWriter(13);
        WriteSpot(w, playerEntityId, spot);
        w.WriteUInt16((ushort)(token | (lucky ? 1 : 0)));
        w.WriteByte((byte)gaugePercent);
        return w.ToArray();
    }

    public static byte[] SeenStart(ushort playerEntityId, DrillSpot spot, bool invincible)
    {
        var w = new PacketWriter(11);
        WriteSpot(w, playerEntityId, spot);
        w.WriteByte(invincible ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] Result(byte result, int life, int itemId)
    {
        var w = new PacketWriter(7);
        w.WriteByte(result);
        w.WriteUInt16((ushort)Math.Clamp(life, 0, ushort.MaxValue));
        w.WriteUInt32((uint)itemId);
        return w.ToArray();
    }

    public static byte[] SeenResult(ushort playerEntityId, byte result)
    {
        var w = new PacketWriter(3);
        w.WriteUInt16(playerEntityId);
        w.WriteByte(result);
        return w.ToArray();
    }

    private static void WriteSpot(PacketWriter w, ushort playerEntityId, DrillSpot spot)
    {
        w.WriteUInt16(playerEntityId);
        w.WriteUInt16(spot.X);
        w.WriteUInt16(spot.Y);
        w.WriteByte(DepthFlag);
        w.WriteByte((byte)(spot.MapDepth / DrillRules.DepthStep));
        w.WriteByte((byte)(spot.LaidDepth / DrillRules.DepthStep));
        w.WriteByte((byte)spot.Nature);
    }
}
