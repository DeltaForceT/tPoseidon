using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.SERVER_LIST)]
public sealed class ServerListPacket
{
    public const int PayloadSize = 1043;
    public const int UrlFieldBytes = 32;
    public const int ReservedRegionOffset = 0x22;
    public const int OccupiedEntryOffset = 0x402;
    public const int ReservedRegionBytes = OccupiedEntryOffset - ReservedRegionOffset; 
    public const int ServerEntryBytes = PayloadSize - OccupiedEntryOffset; 

        public string PublisherUrl { get; init; } = string.Empty;

        public ServerEntry OccupiedEntry { get; init; } = ServerEntry.Empty;

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(PublisherUrl);

        var urlBytes = Encoding.ASCII.GetBytes(PublisherUrl);
        if (urlBytes.Length > UrlFieldBytes)
        {
            throw new InvalidOperationException(
                $"PublisherUrl is {urlBytes.Length} bytes; max is {UrlFieldBytes}.");
        }

        var w = new PacketWriter(PayloadSize);

        w.WriteUInt16(1);

        w.WriteBytes(urlBytes);
        w.WriteZero(UrlFieldBytes - urlBytes.Length);

        w.WriteZero(ReservedRegionBytes);

        OccupiedEntry.WriteTo(w);

        var bytes = w.ToArray();
        if (bytes.Length != PayloadSize)
        {
            throw new InvalidOperationException(
                $"Encoded SERVER_LIST is {bytes.Length} bytes; expected {PayloadSize}.");
        }
        return bytes;
    }

    public static ServerListPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"SERVER_LIST payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        var serverCount = r.ReadUInt16();
        if (serverCount != 1)
        {

            
            throw new InvalidPacketException(
                $"SERVER_LIST: only count=1 is currently understood, got {serverCount}.");
        }
        var url = r.ReadFixedString(UrlFieldBytes);

        for (var i = 0; i < ReservedRegionBytes; i++) r.ReadByte();
        var entry = ServerEntry.ReadFrom(r);
        return new ServerListPacket
        {
            PublisherUrl = url,
            OccupiedEntry = entry,
        };
    }
}

public sealed class ServerEntry
{
    public const int Bytes = 17;

        public static readonly ServerEntry Empty = new()
    {
        ServerId = 0x01,
        FieldA = 0x07D6,
        Status = 1,
        FieldB = 0x0018,
        FieldC = 0x0015,
        FieldD = 0x001A,
        FieldE = 0,
    };

    public byte ServerId { get; init; }
    public ushort FieldA { get; init; }
    public uint Status { get; init; }
    public ushort FieldB { get; init; }
    public ushort FieldC { get; init; }
    public ushort FieldD { get; init; }
    public uint FieldE { get; init; }

    public void WriteTo(PacketWriter w)
    {
        w.WriteByte(ServerId);
        w.WriteUInt16(FieldA);
        w.WriteUInt32(Status);
        w.WriteUInt16(FieldB);
        w.WriteUInt16(FieldC);
        w.WriteUInt16(FieldD);
        w.WriteUInt32(FieldE);
    }

    public static ServerEntry ReadFrom(PacketReader r) =>
        new()
        {
            ServerId = r.ReadByte(),
            FieldA = r.ReadUInt16(),
            Status = r.ReadUInt32(),
            FieldB = r.ReadUInt16(),
            FieldC = r.ReadUInt16(),
            FieldD = r.ReadUInt16(),
            FieldE = r.ReadUInt32(),
        };
}
