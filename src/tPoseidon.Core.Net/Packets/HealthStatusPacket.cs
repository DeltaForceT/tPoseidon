using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_HEALTH_STATUS)]
public sealed class HealthStatusPacket
{
    public const int PayloadSize = 4;

        public uint CurrentHp { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt32(CurrentHp);
        return w.ToArray();
    }

    public static HealthStatusPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"HealthStatus payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new HealthStatusPacket { CurrentHp = r.ReadUInt32() };
    }
}
