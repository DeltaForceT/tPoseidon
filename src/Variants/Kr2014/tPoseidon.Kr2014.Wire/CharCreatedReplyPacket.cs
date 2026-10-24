using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Kr2014.Wire;

[WireOpcode((ushort)Opcode.CHAR_CREATED)]
public sealed class CharCreatedReplyPacket
{
    public const int PayloadSize = 156;

    private const int CharUidOffset = 0x00;
    private const int SlotOffset = 0x08;
    private const int NameRegionOffset = 0x09;
    private const int NameRegionBytes = 0x21 - 0x09;
    private const int NameSlotShortBytes = 7;
    public const int MaxCharacterNameChars = 17;
    private const int FirstJobSpeciesOffset = 0x1D;
    private const int SecondJobClassOffset = 0x1F;
    private const int FTypeOffset = 0x21;
    private const int SpeciesMirrorOffset = 0x22;
    private const int SecondJobMirrorOffset = 0x23;
    private const int JobTierOffset = 0x24;
    private const int HairOffset = 0x25;
    private const int MoneyOffset = 0x27;
    private const int LevelOffset = 0x2B;
    private const int EarItemOffset = 0x41;
    private const int TailItemOffset = 0x45;

    public ulong CharUid { get; init; }
    public byte Slot { get; init; }
    public string CharacterName { get; init; } = string.Empty;
    public Species Species { get; init; }
    public byte FType { get; init; }
    public ushort Hair { get; init; }
    public uint Money { get; init; }
    public ushort Level { get; init; } = 1;
    public byte JobTier { get; init; } = 0x01;
    public uint EarItemIndex { get; init; } = SpeciesDefaults.EarItemIndexForSpecies(Species.Bunny);
    public uint TailItemIndex { get; init; } = SpeciesDefaults.TailItemIndexForSpecies(Species.Bunny);
    public byte[]? RawBytes { get; init; }

    public static CharCreatedReplyPacket FromNewCharacter(Character character, string accountLogin)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(accountLogin);
        _ = accountLogin;

        return new CharCreatedReplyPacket
        {
            CharUid = character.CharacterId,
            Slot = character.Slot,
            CharacterName = character.Name,
            Species = character.Species,
            FType = (byte)((byte)character.FType - 1),
            Hair = character.Hair,
            Money = WireLimits.Int32FieldUnsigned(character.Money),
            Level = character.Level,
            JobTier = character.JobTier,
            EarItemIndex = character.StarterEarItemIndex,
            TailItemIndex = character.StarterTailItemIndex,
        };
    }

    public byte[] EncodePayload()
    {
        byte[] buf;
        if (RawBytes is not null)
        {
            if (RawBytes.Length != PayloadSize)
            {
                throw new InvalidOperationException(
                    $"Kr2014 CharCreatedReply RawBytes must be {PayloadSize} bytes when set, got {RawBytes.Length}.");
            }
            buf = new byte[PayloadSize];
            Array.Copy(RawBytes, buf, PayloadSize);
        }
        else
        {
            buf = new byte[PayloadSize];
        }

        WriteUInt64Le(buf, CharUidOffset, CharUid);
        buf[SlotOffset] = Slot;

        var nameBytes = Encoding.ASCII.GetBytes(CharacterName ?? string.Empty);
        if (nameBytes.Length > MaxCharacterNameChars)
        {
            throw new InvalidOperationException(
                $"CharacterName '{CharacterName}' is {nameBytes.Length} bytes; max is {MaxCharacterNameChars}.");
        }
        if (nameBytes.Length <= NameSlotShortBytes)
        {
            WriteNameField(buf, NameRegionOffset, NameSlotShortBytes, CharacterName ?? string.Empty);
        }
        else
        {
            Array.Clear(buf, NameRegionOffset, NameRegionBytes);
            Array.Copy(nameBytes, 0, buf, NameRegionOffset, nameBytes.Length);
        }

        WriteUInt16Le(buf, FirstJobSpeciesOffset, (ushort)Species);
        WriteUInt16Le(buf, SecondJobClassOffset, 0);
        buf[FTypeOffset] = FType;
        buf[SpeciesMirrorOffset] = (byte)Species;
        buf[SecondJobMirrorOffset] = 0;
        buf[JobTierOffset] = JobTier;
        WriteUInt16Le(buf, HairOffset, Hair);
        WriteUInt32Le(buf, MoneyOffset, Money);
        WriteUInt16Le(buf, LevelOffset, Level);
        WriteUInt32Le(buf, EarItemOffset, EarItemIndex);
        WriteUInt32Le(buf, TailItemOffset, TailItemIndex);

        return buf;
    }

    public static CharCreatedReplyPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Kr2014 CharCreatedReply payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }

        return new CharCreatedReplyPacket
        {
            CharUid = ReadUInt64Le(payload, CharUidOffset),
            Slot = payload[SlotOffset],
            CharacterName = ReadFixedAscii(payload, NameRegionOffset, NameSlotShortBytes),
            Species = (Species)payload[SpeciesMirrorOffset],
            FType = payload[FTypeOffset],
            Hair = (ushort)(payload[HairOffset] | (payload[HairOffset + 1] << 8)),
            Money = ReadUInt32Le(payload, MoneyOffset),
            Level = (ushort)(payload[LevelOffset] | (payload[LevelOffset + 1] << 8)),
            JobTier = payload[JobTierOffset],
            EarItemIndex = ReadUInt32Le(payload, EarItemOffset),
            TailItemIndex = ReadUInt32Le(payload, TailItemOffset),
            RawBytes = payload.ToArray(),
        };
    }

    private static void WriteNameField(byte[] buf, int offset, int fieldBytes, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length > fieldBytes)
        {
            throw new InvalidOperationException(
                $"CharacterName '{value}' is {bytes.Length} bytes; max is {fieldBytes}.");
        }
        Array.Clear(buf, offset, fieldBytes);
        Array.Copy(bytes, 0, buf, offset, bytes.Length);
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> buf, int offset, int fieldBytes)
    {
        var slice = buf.Slice(offset, fieldBytes);
        var nul = slice.IndexOf((byte)0);
        var len = nul < 0 ? fieldBytes : nul;
        return Encoding.ASCII.GetString(slice[..len]);
    }

    private static void WriteUInt16Le(byte[] buf, int offset, ushort value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteUInt32Le(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteUInt64Le(byte[] buf, int offset, ulong value)
    {
        for (var i = 0; i < 8; i++)
        {
            buf[offset + i] = (byte)((value >> (i * 8)) & 0xFF);
        }
    }

    private static uint ReadUInt32Le(ReadOnlySpan<byte> buf, int offset) =>
        (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));

    private static ulong ReadUInt64Le(ReadOnlySpan<byte> buf, int offset)
    {
        ulong v = 0;
        for (var i = 0; i < 8; i++)
        {
            v |= (ulong)buf[offset + i] << (i * 8);
        }
        return v;
    }
}
