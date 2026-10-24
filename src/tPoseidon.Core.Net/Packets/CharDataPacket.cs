using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.CHAR_DATA)]
public sealed class CharDataPacket
{
    public const int PayloadSize = 63;

        public static readonly CharDataPacket SnapshotDefault = new()
    {
        RecordHeader = 0x0201,
        Unknown2 = 0x0072,
        Reserved4 = 0x00,
        LoginCounter = 0x40,
        Unknown6 = 0x002C,
        Reserved8 = 0,
        Reserved16 = 0,
        Unknown24 = 0x00CFFAE4,
        Unknown28 = 0x42CFFAE4,
        Unknown32 = 0x0066E513,
        ServerTimestamp1 = 0x31C4E750,
        ServerTimestamp2 = 0x31C4E76C,
        Unknown44 = 0x27574008,
        Unknown48 = 0x06000000,
        Reserved52 = 0,
        Unknown56A = 0x5D,
        Unknown56B = 0x8C,
        Unknown56C = 0xE2,
        Reserved59 = 0,
    };

        public ushort RecordHeader { get; init; } = 0x0201;

        public ushort Unknown2 { get; init; }

        public byte Reserved4 { get; init; }

        public byte LoginCounter { get; init; }

        public ushort Unknown6 { get; init; }

        public ulong Reserved8 { get; init; }
    public ulong Reserved16 { get; init; }

        public uint Unknown24 { get; init; }

        public uint Unknown28 { get; init; }

        public uint Unknown32 { get; init; }

        public uint ServerTimestamp1 { get; init; }

        public uint ServerTimestamp2 { get; init; }

        public uint Unknown44 { get; init; }

        public uint Unknown48 { get; init; }

        public uint Reserved52 { get; init; }

        public byte Unknown56A { get; init; }
    public byte Unknown56B { get; init; }
    public byte Unknown56C { get; init; }

        public uint Reserved59 { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(RecordHeader);
        w.WriteUInt16(Unknown2);
        w.WriteByte(Reserved4);
        w.WriteByte(LoginCounter);
        w.WriteUInt16(Unknown6);
        w.WriteUInt64(Reserved8);
        w.WriteUInt64(Reserved16);
        w.WriteUInt32(Unknown24);
        w.WriteUInt32(Unknown28);
        w.WriteUInt32(Unknown32);
        w.WriteUInt32(ServerTimestamp1);
        w.WriteUInt32(ServerTimestamp2);
        w.WriteUInt32(Unknown44);
        w.WriteUInt32(Unknown48);
        w.WriteUInt32(Reserved52);
        w.WriteByte(Unknown56A);
        w.WriteByte(Unknown56B);
        w.WriteByte(Unknown56C);
        w.WriteUInt32(Reserved59);
        return w.ToArray();
    }

    public static CharDataPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"CharData payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new CharDataPacket
        {
            RecordHeader = r.ReadUInt16(),
            Unknown2 = r.ReadUInt16(),
            Reserved4 = r.ReadByte(),
            LoginCounter = r.ReadByte(),
            Unknown6 = r.ReadUInt16(),
            Reserved8 = r.ReadUInt64(),
            Reserved16 = r.ReadUInt64(),
            Unknown24 = r.ReadUInt32(),
            Unknown28 = r.ReadUInt32(),
            Unknown32 = r.ReadUInt32(),
            ServerTimestamp1 = r.ReadUInt32(),
            ServerTimestamp2 = r.ReadUInt32(),
            Unknown44 = r.ReadUInt32(),
            Unknown48 = r.ReadUInt32(),
            Reserved52 = r.ReadUInt32(),
            Unknown56A = r.ReadByte(),
            Unknown56B = r.ReadByte(),
            Unknown56C = r.ReadByte(),
            Reserved59 = r.ReadUInt32(),
        };
    }
}
