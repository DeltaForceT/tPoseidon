using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class PrepareEnteringZone
{
    public required ushort FromZone { get; init; }
    public required ushort ToZone { get; init; }
    public required ushort ZoneType { get; init; }
    public required byte ChangeZoneType { get; init; }
    public byte Spare { get; init; }
    public required ulong CharUid { get; init; }
    public uint Token { get; init; }
    public ushort PlayerEntityId { get; init; }
    public ushort StartX { get; init; }
    public ushort StartY { get; init; }

    public byte[] EncodeBody()
    {
        var writer = new PacketWriter(26);
        writer.WriteUInt16(FromZone);
        writer.WriteUInt16(ToZone);
        writer.WriteUInt16(ZoneType);
        writer.WriteByte(ChangeZoneType);
        writer.WriteByte(Spare);
        writer.WriteUInt32((uint)(CharUid & 0xFFFFFFFF));
        writer.WriteUInt32((uint)(CharUid >> 32));
        writer.WriteUInt32(Token);
        writer.WriteUInt16(PlayerEntityId);
        writer.WriteUInt16(StartX);
        writer.WriteUInt16(StartY);
        return writer.ToArray();
    }

    public static PrepareEnteringZone DecodeBody(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var from = reader.ReadUInt16();
        var to = reader.ReadUInt16();
        var type = reader.ReadUInt16();
        var changeType = reader.ReadByte();
        var spare = reader.ReadByte();
        ulong lo = reader.ReadUInt32();
        ulong hi = reader.ReadUInt32();
        uint token = body.Length >= 20 ? reader.ReadUInt32() : 0;
        ushort eid = body.Length >= 22 ? reader.ReadUInt16() : (ushort)0;
        ushort startX = body.Length >= 24 ? reader.ReadUInt16() : (ushort)0;
        ushort startY = body.Length >= 26 ? reader.ReadUInt16() : (ushort)0;
        return new PrepareEnteringZone
        {
            FromZone = from,
            ToZone = to,
            ZoneType = type,
            ChangeZoneType = changeType,
            Spare = spare,
            CharUid = lo | (hi << 32),
            Token = token,
            PlayerEntityId = eid,
            StartX = startX,
            StartY = startY,
        };
    }
}

public sealed class EnterZoneDone
{
    public required ushort EntryIndex { get; init; }
    public required ulong CharUid { get; init; }
    public required ushort ZoneId { get; init; }
    public required uint SessionRandom { get; init; }
    public byte Tail { get; init; }

    public byte[] EncodeBody()
    {
        var writer = new PacketWriter(17);
        writer.WriteUInt16(EntryIndex);
        writer.WriteUInt32((uint)(CharUid & 0xFFFFFFFF));
        writer.WriteUInt32((uint)(CharUid >> 32));
        writer.WriteUInt16(ZoneId);
        writer.WriteUInt32(SessionRandom);
        writer.WriteByte(Tail);
        return writer.ToArray();
    }

    public static EnterZoneDone DecodeBody(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var idx = reader.ReadUInt16();
        ulong lo = reader.ReadUInt32();
        ulong hi = reader.ReadUInt32();
        var zone = reader.ReadUInt16();
        var rnd = reader.ReadUInt32();
        var tail = reader.ReadByte();
        return new EnterZoneDone
        {
            EntryIndex = idx,
            CharUid = lo | (hi << 32),
            ZoneId = zone,
            SessionRandom = rnd,
            Tail = tail,
        };
    }
}
