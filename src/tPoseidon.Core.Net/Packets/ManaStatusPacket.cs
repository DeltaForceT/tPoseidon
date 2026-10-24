using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_MANA_STATUS)]
public sealed class ManaStatusPacket
{
    public const int PayloadSize = 4;

    public uint CurrentMp { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt32(CurrentMp);
        return w.ToArray();
    }

    public static ManaStatusPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"ManaStatus payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new ManaStatusPacket { CurrentMp = r.ReadUInt32() };
    }
}
