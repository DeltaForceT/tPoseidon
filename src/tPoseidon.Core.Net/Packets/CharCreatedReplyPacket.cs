using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Packets;

public sealed class CharCreatedReplyPacket
{
        public const int PayloadSize = 156;

    private const int CharUidOffset = 0x00;
    private const int SlotOffset = 0x08;

    private const int NameRegionOffset = 0x09;
    private const int NameSlotShortBytes = 7;

    private const int FTypeOffset = 0x21;
    private const int SpeciesMirrorOffset = 0x22;
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

    public static CharCreatedReplyPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"CharCreatedReply payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
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

    private static string ReadFixedAscii(ReadOnlySpan<byte> buf, int offset, int fieldBytes)
    {
        var slice = buf.Slice(offset, fieldBytes);
        var nul = slice.IndexOf((byte)0);
        var len = nul < 0 ? fieldBytes : nul;
        return Encoding.ASCII.GetString(slice[..len]);
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
