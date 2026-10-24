using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class SessionInitPacket
{
    public const int PayloadSize = 12;

    public uint CharacterId { get; init; }

        public uint Reserved4 { get; init; }

        public uint SessionToken { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt32(CharacterId);
        w.WriteUInt32(Reserved4);
        w.WriteUInt32(SessionToken);
        return w.ToArray();
    }

    public static SessionInitPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"SessionInit payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new SessionInitPacket
        {
            CharacterId = r.ReadUInt32(),
            Reserved4 = r.ReadUInt32(),
            SessionToken = r.ReadUInt32(),
        };
    }
}
