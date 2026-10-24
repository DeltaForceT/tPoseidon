using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class C2SMovementPacket
{
    public const int PayloadSize = 9;

    public ushort FromX { get; init; }
    public ushort FromY { get; init; }
    public ushort ToX { get; init; }
    public ushort ToY { get; init; }

        public byte Flag { get; init; } = 0x02;

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(FromX);
        w.WriteUInt16(FromY);
        w.WriteUInt16(ToX);
        w.WriteUInt16(ToY);
        w.WriteByte(Flag);
        return w.ToArray();
    }

    public static C2SMovementPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"C2SMovement payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new C2SMovementPacket
        {
            FromX = r.ReadUInt16(),
            FromY = r.ReadUInt16(),
            ToX = r.ReadUInt16(),
            ToY = r.ReadUInt16(),
            Flag = r.ReadByte(),
        };
    }
}
