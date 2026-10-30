using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Progression;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.GAME_CHAR_DATA)]
public sealed class GameCharDataPacket
{
    public const int PayloadSize = 73;
    public const int NameFieldBytes = 16;

    public ulong CharacterId { get; init; }
    public ulong UserId { get; init; }
    public byte Slot { get; init; }
    public string Name { get; init; } = string.Empty;

        public ushort Species { get; init; }

    public byte? FTypeIndex { get; init; }

        public byte StatA { get; init; } = 4;
    public byte StatB { get; init; } = 1;
    public byte StatC { get; init; } = 4;
    public byte StatD { get; init; } = 1;

        public ulong Timestamp { get; init; }

    public static GameCharDataPacket For(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return new GameCharDataPacket
        {
            CharacterId = character.CharacterId,
            UserId = character.OwnerAccountId,
            Slot = character.Slot,
            Name = character.Name,
            Species = (ushort)character.Species,
            FTypeIndex = (byte)StatGrowth.TypeIndexOf(character.FType),
            StatA = character.PtsP,
            StatB = character.PtsM,
            StatC = character.PtsS,
            StatD = character.PtsC,
            Timestamp = (ulong)character.GenTime.ToUnixTimeSeconds(),
        };
    }

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(Name);
        var nameBytes = ClientText.Encoding.GetBytes(Name);
        if (nameBytes.Length >= NameFieldBytes)
        {
            throw new InvalidOperationException(
                $"Character name '{Name}' is {nameBytes.Length} bytes; max is {NameFieldBytes - 1} (plus NUL).");
        }

        var buf = new byte[PayloadSize];
        WriteUInt64Le(buf, 0x00, CharacterId);
        WriteUInt64Le(buf, 0x08, UserId);
        buf[0x10] = Slot;
        Array.Copy(nameBytes, 0, buf, 0x11, nameBytes.Length);

        WriteUInt16Le(buf, 0x25, Species);
        WriteUInt16Le(buf, 0x27, Species);
        if (FTypeIndex is { } typeIndex) buf[0x28] = typeIndex;
        buf[0x29] = StatA;
        buf[0x2A] = StatB;
        buf[0x2B] = StatC;
        buf[0x2C] = StatD;
        WriteUInt64Le(buf, 0x2D, Timestamp);

        return buf;
    }

    public static GameCharDataPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"GameCharData payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new GameCharDataPacket
        {
            CharacterId = ReadUInt64Le(payload, 0x00),
            UserId = ReadUInt64Le(payload, 0x08),
            Slot = payload[0x10],
            Name = ReadFixedAscii(payload, 0x11, NameFieldBytes),
            Species = ReadUInt16Le(payload, 0x25),
            FTypeIndex = payload[0x28],
            StatA = payload[0x29],
            StatB = payload[0x2A],
            StatC = payload[0x2B],
            StatD = payload[0x2C],
            Timestamp = ReadUInt64Le(payload, 0x2D),
        };
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> buf, int offset, int fieldBytes)
    {
        var slice = buf.Slice(offset, fieldBytes);
        var nul = slice.IndexOf((byte)0);
        var len = nul < 0 ? fieldBytes : nul;
        return len == 0 ? string.Empty : ClientText.Encoding.GetString(slice[..len]);
    }

    private static void WriteUInt16Le(byte[] buf, int offset, ushort value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteUInt64Le(byte[] buf, int offset, ulong value)
    {
        for (var i = 0; i < 8; i++)
        {
            buf[offset + i] = (byte)((value >> (i * 8)) & 0xFF);
        }
    }

    private static ushort ReadUInt16Le(ReadOnlySpan<byte> buf, int offset) =>
        (ushort)(buf[offset] | (buf[offset + 1] << 8));

    private static ulong ReadUInt64Le(ReadOnlySpan<byte> buf, int offset)
    {
        ulong v = 0;
        for (var i = 0; i < 8; i++) v |= (ulong)buf[offset + i] << (i * 8);
        return v;
    }
}
