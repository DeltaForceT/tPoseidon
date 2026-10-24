using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class CharSelectPacket
{
    public const int PayloadSize = 8;

    public uint CharacterId { get; init; }
    public uint Reserved4 { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt32(CharacterId);
        w.WriteUInt32(Reserved4);
        return w.ToArray();
    }

    public static CharSelectPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"CharSelect payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new CharSelectPacket
        {
            CharacterId = r.ReadUInt32(),
            Reserved4 = r.ReadUInt32(),
        };
    }
}
