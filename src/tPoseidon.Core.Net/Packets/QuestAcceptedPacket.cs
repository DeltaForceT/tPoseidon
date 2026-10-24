using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_QUEST_ACCEPTED)]
public sealed class QuestAcceptedPacket
{
        public const int HeaderBytes = 6;

        public const int MinPaddedNameBytes = 61;

    public required int QuestId { get; init; }

        public ushort State { get; init; } = 0x0001;

    public required string QuestName { get; init; }

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(QuestName);
        var nameBytes = Encoding.ASCII.GetBytes(QuestName);

        var nameRegionSize = Math.Max(nameBytes.Length + 1, MinPaddedNameBytes);
        var totalSize = HeaderBytes + nameRegionSize;

        var w = new PacketWriter(totalSize);
        w.WriteUInt32((uint)QuestId);
        w.WriteUInt16(State);
        w.WriteBytes(nameBytes);

        

        w.WriteZero(nameRegionSize - nameBytes.Length);
        return w.ToArray();
    }

    public static QuestAcceptedPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderBytes + 1)
        {
            throw new InvalidPacketException(
                $"QuestAccepted payload must be at least {HeaderBytes + 1} bytes (header + 1B name+NUL), got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        var questId = (int)r.ReadUInt32();
        var state = r.ReadUInt16();

        var nameRegion = payload[HeaderBytes..];
        var nul = nameRegion.IndexOf((byte)0);
        var nameLen = nul < 0 ? nameRegion.Length : nul;
        var name = nameLen == 0 ? string.Empty : Encoding.ASCII.GetString(nameRegion[..nameLen]);

        return new QuestAcceptedPacket
        {
            QuestId = questId,
            State = state,
            QuestName = name,
        };
    }
}
