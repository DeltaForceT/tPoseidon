using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_QUEST_COMPLETED)]
public sealed class QuestCompletedPacket
{
    public const int PayloadSize = 4;

    public required int QuestId { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt32((uint)QuestId);
        return w.ToArray();
    }

    public static QuestCompletedPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"QuestCompleted payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new QuestCompletedPacket { QuestId = (int)r.ReadUInt32() };
    }
}
