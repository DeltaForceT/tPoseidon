using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet01CFTbd
{
    public const int PayloadSize = 6;

    public ushort EntityId { get; init; }

    public byte Action { get; init; }

    public ushort AniTimeSeconds { get; init; }

    public byte Loop { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(EntityId);
        w.WriteByte(Action);
        w.WriteUInt16(AniTimeSeconds);
        w.WriteByte(Loop);
        return w.ToArray();
    }

    public static Packet01CFTbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet01CFTbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new Packet01CFTbd
        {
            EntityId = r.ReadUInt16(),
            Action = r.ReadByte(),
            AniTimeSeconds = r.ReadUInt16(),
            Loop = r.ReadByte(),
        };
    }
}
