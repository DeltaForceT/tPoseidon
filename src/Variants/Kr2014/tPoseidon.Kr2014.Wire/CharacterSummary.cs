using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Kr2014.Wire;

public sealed record CharacterSummary(ulong CharacterId, byte Slot, string Name, ushort Species, byte FType, byte JobTier, ushort Hair, ushort Level)
{
    public const byte FTypeWireOffset = 1;

    public static CharacterSummary Read(ReadOnlySpan<byte> entry)
    {
        if (entry.Length != CharacterListBuilder.EntryWidth)
        {
            throw new InvalidPacketException(
                $"Kr2014 character entry must be {CharacterListBuilder.EntryWidth} bytes, got {entry.Length}.");
        }
        var nameRegion = entry.Slice(CharacterListBuilder.NameRegionOffset, CharacterListBuilder.NameRegionBytes);
        var nameEnd = nameRegion.IndexOf((byte)0);
        return new CharacterSummary(
            BinaryPrimitives.ReadUInt64LittleEndian(entry[CharacterListBuilder.CharIdOffset..]),
            entry[CharacterListBuilder.SlotOffset],
            Encoding.ASCII.GetString(nameEnd < 0 ? nameRegion : nameRegion[..nameEnd]),
            BinaryPrimitives.ReadUInt16LittleEndian(entry[CharacterListBuilder.FirstJobSpeciesOffset..]),
            (byte)(entry[CharacterListBuilder.FTypeOffset] + FTypeWireOffset),
            entry[CharacterListBuilder.JobTierOffset],
            BinaryPrimitives.ReadUInt16LittleEndian(entry[CharacterListBuilder.HairOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(entry[CharacterListBuilder.LevelOffset..]));
    }
}
