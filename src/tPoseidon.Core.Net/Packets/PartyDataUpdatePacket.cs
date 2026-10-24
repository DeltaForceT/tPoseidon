using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_OP_1773_TBD)]
public sealed class PartyDataUpdatePacket
{
    public const int PayloadSize = 4;

        public static readonly PartyDataUpdatePacket NoParty = new()
    {
        Flags = 0xFF,
        Reserved1 = 0x00,
        State = 0x01,
        Reserved2 = 0x00,
    };

    public byte Flags { get; init; }
    public byte Reserved1 { get; init; }
    public byte State { get; init; }
    public byte Reserved2 { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteByte(Flags);
        w.WriteByte(Reserved1);
        w.WriteByte(State);
        w.WriteByte(Reserved2);
        return w.ToArray();
    }

    public static PartyDataUpdatePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"PartyDataUpdate payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new PartyDataUpdatePacket
        {
            Flags = r.ReadByte(),
            Reserved1 = r.ReadByte(),
            State = r.ReadByte(),
            Reserved2 = r.ReadByte(),
        };
    }
}
