using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class LeaveZoneRequest
{
    public required ulong CharUid { get; init; }
    public required ushort FromZone { get; init; }
    public required ushort ToZone { get; init; }
    public required ushort StartX { get; init; }
    public required ushort StartY { get; init; }
    public required byte ChangeType { get; init; }
    public uint Token { get; init; }
    public ushort PlayerEntityId { get; init; }

    public byte[] EncodeBody()
    {
        var writer = new PacketWriter(24);
        writer.WriteUInt32((uint)(CharUid & 0xFFFFFFFF));
        writer.WriteUInt32((uint)(CharUid >> 32));
        writer.WriteUInt16(FromZone);
        writer.WriteUInt16(ToZone);
        writer.WriteUInt16(StartX);
        writer.WriteUInt16(StartY);
        writer.WriteByte(ChangeType);
        writer.WriteUInt32(Token);
        writer.WriteUInt16(PlayerEntityId);
        return writer.ToArray();
    }

    public static LeaveZoneRequest DecodeBody(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        ulong lo = reader.ReadUInt32();
        ulong hi = reader.ReadUInt32();
        var fromZone = reader.ReadUInt16();
        var toZone = reader.ReadUInt16();
        var x = reader.ReadUInt16();
        var y = reader.ReadUInt16();
        var changeType = reader.ReadByte();
        uint token = body.Length >= 22 ? reader.ReadUInt32() : 0;
        ushort eid = body.Length >= 24 ? reader.ReadUInt16() : (ushort)0;
        return new LeaveZoneRequest
        {
            CharUid = lo | (hi << 32),
            FromZone = fromZone,
            ToZone = toZone,
            StartX = x,
            StartY = y,
            ChangeType = changeType,
            Token = token,
            PlayerEntityId = eid,
        };
    }
}

public sealed class LeaveZoneAck
{
    public required ulong CharUid { get; init; }
    public required ushort ToZone { get; init; }
    public required ushort StartX { get; init; }
    public required ushort StartY { get; init; }
    public required byte ChangeType { get; init; }
    public uint DestServerIp { get; init; }
    public ushort DestServerPort { get; init; }
    public uint ReservedW2 { get; init; }
    public uint ReservedW3 { get; init; }

    public byte[] EncodeBody()
    {
        var writer = new PacketWriter(40);
        writer.WriteUInt32((uint)(CharUid & 0xFFFFFFFF));
        writer.WriteUInt32((uint)(CharUid >> 32));
        writer.WriteUInt32(DestServerIp);
        writer.WriteUInt32((uint)DestServerPort);
        writer.WriteUInt32(ReservedW2);
        writer.WriteUInt32(ReservedW3);
        writer.WriteUInt16(ToZone);
        writer.WriteUInt16(StartX);
        writer.WriteUInt16(StartY);
        writer.WriteByte(ChangeType);
        return writer.ToArray();
    }

    public static LeaveZoneAck DecodeBody(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        ulong lo = reader.ReadUInt32();
        ulong hi = reader.ReadUInt32();
        var destIp = reader.ReadUInt32();
        var destPort = (ushort)reader.ReadUInt32();
        var rw2 = reader.ReadUInt32();
        var rw3 = reader.ReadUInt32();
        var toZone = reader.ReadUInt16();
        var x = reader.ReadUInt16();
        var y = reader.ReadUInt16();
        var changeType = reader.ReadByte();
        return new LeaveZoneAck
        {
            CharUid = lo | (hi << 32),
            ToZone = toZone,
            StartX = x,
            StartY = y,
            ChangeType = changeType,
            DestServerIp = destIp,
            DestServerPort = destPort,
            ReservedW2 = rw2,
            ReservedW3 = rw3,
        };
    }
}
