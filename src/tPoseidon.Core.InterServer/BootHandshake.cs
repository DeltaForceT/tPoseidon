using tPoseidon.Core.Common;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class ServerLoginRequest
{
    public required string ServerName { get; init; }
    public required ServerRole Role { get; init; }
    public required ushort ServerIndex { get; init; }
    public string Password { get; init; } = string.Empty;

    public byte[] EncodeBody()
    {
        var name = System.Text.Encoding.ASCII.GetBytes(ServerName);
        var password = System.Text.Encoding.ASCII.GetBytes(Password ?? string.Empty);
        var writer = new PacketWriter(32 + name.Length + password.Length);
        writer.WriteUInt16(ServerIndex);
        writer.WriteByte((byte)Role);
        writer.WriteByte((byte)name.Length);
        writer.WriteBytes(name);
        writer.WriteByte((byte)password.Length);
        writer.WriteBytes(password);
        return writer.ToArray();
    }

    public static ServerLoginRequest DecodeBody(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var index = reader.ReadUInt16();
        var role = (ServerRole)reader.ReadByte();
        var nameLen = reader.ReadByte();
        var name = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(nameLen));
        var password = string.Empty;
        if (reader.Remaining > 0)
        {
            var pwLen = reader.ReadByte();
            password = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(pwLen));
        }
        return new ServerLoginRequest
        {
            ServerName = name,
            Role = role,
            ServerIndex = index,
            Password = password,
        };
    }
}

public sealed class ServerLoginAckOk
{
    public byte Spare { get; init; }
    public required uint ServerTime { get; init; }
    public required uint ServerTimeMs { get; init; }
    public ushort[] ZoneIds { get; init; } = Array.Empty<ushort>();
    public bool SyncHourEnabled { get; init; }
    public byte SyncHour { get; init; }
    public byte SyncMinute { get; init; }
    public ushort WorldCoordConstX { get; init; }
    public ushort WorldCoordConstY { get; init; }

    public byte[] EncodeBody()
    {
        var writer = new PacketWriter(32 + (ZoneIds.Length * 2));
        writer.WriteByte(Spare);
        writer.WriteUInt32(ServerTime);
        writer.WriteUInt32(ServerTimeMs);
        if (ZoneIds.Length > ushort.MaxValue)
        {
            throw new InvalidPacketException(
                $"ServerLoginAckOk has {ZoneIds.Length} zones; exceeds u16 count max.");
        }
        writer.WriteUInt16((ushort)ZoneIds.Length);
        foreach (var z in ZoneIds)
        {
            writer.WriteUInt16(z);
        }
        writer.WriteByte((byte)(SyncHourEnabled ? 1 : 0));
        writer.WriteByte(SyncHour);
        writer.WriteByte(SyncMinute);
        writer.WriteUInt16(WorldCoordConstX);
        writer.WriteUInt16(WorldCoordConstY);
        return writer.ToArray();
    }

    public static ServerLoginAckOk DecodeBody(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var spare = reader.ReadByte();
        var time = reader.ReadUInt32();
        var timeMs = reader.ReadUInt32();
        var count = reader.ReadUInt16();
        var zones = new ushort[count];
        for (var i = 0; i < count; i++)
        {
            zones[i] = reader.ReadUInt16();
        }
        var syncFlag = reader.ReadByte() != 0;
        var syncHour = reader.ReadByte();
        var syncMinute = reader.ReadByte();
        var x = reader.ReadUInt16();
        var y = reader.ReadUInt16();
        return new ServerLoginAckOk
        {
            Spare = spare,
            ServerTime = time,
            ServerTimeMs = timeMs,
            ZoneIds = zones,
            SyncHourEnabled = syncFlag,
            SyncHour = syncHour,
            SyncMinute = syncMinute,
            WorldCoordConstX = x,
            WorldCoordConstY = y,
        };
    }
}
