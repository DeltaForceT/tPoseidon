using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.S2;

public static class CharacterListBuilder
{
    public const int EntryWidth = CharacterListPacket.EntryWidth;

    private const int CharIdOffset = 0x00;
    private const int SlotOffset = 0x08;
    private const int NameRegionOffset = 0x09;
    private const int NameRegionBytes = 0x21 - 0x09;
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







    private const int EquipBlockOffset = 0x2D;
    private const int EquipBlockSlots = 5;

        public static CharacterListPacket Build(
        IReadOnlyList<Character> characters,
        IItemStore? itemStore = null)
    {
        ArgumentNullException.ThrowIfNull(characters);
        if (characters.Count > byte.MaxValue)
        {
            throw new ArgumentException(
                $"Char list supports max {byte.MaxValue} entries, got {characters.Count}.",
                nameof(characters));
        }

        var sorted = characters.OrderBy(c => c.Slot).ToArray();
        var entries = new CharacterListPacket.Entry[sorted.Length];
        for (var i = 0; i < sorted.Length; i++)
        {
            entries[i] = new CharacterListPacket.Entry
            {
                Raw = BuildEntry(sorted[i], itemStore),
                Name = sorted[i].Name,
            };
        }
        return new CharacterListPacket
        {
            Entries = entries,

        };
    }

    private static byte[] BuildEntry(Character character, IItemStore? itemStore)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (character.Name.Length == 0)
        {
            throw new InvalidOperationException("Character.Name must be non-empty.");
        }

        var entry = new byte[EntryWidth];

        WriteUInt64Le(entry, CharIdOffset, character.CharacterId);

        entry[SlotOffset] = character.Slot;



        var nameBytes = Encoding.ASCII.GetBytes(character.Name);
        if (nameBytes.Length >= NameRegionBytes)
        {
            throw new InvalidOperationException(
                $"Character name '{character.Name}' is {nameBytes.Length} bytes; max is {NameRegionBytes - 1} (plus NUL).");
        }
        Array.Copy(nameBytes, 0, entry, NameRegionOffset, nameBytes.Length);

        if (character.AccountLabel.Length > 0)
        {
            var labelBytes = Encoding.ASCII.GetBytes(character.AccountLabel);
            var labelStart = NameRegionOffset + nameBytes.Length + 1;
            var maxLabelBytes = NameRegionBytes - (nameBytes.Length + 1);
            if (labelBytes.Length >= maxLabelBytes)
            {
                throw new InvalidOperationException(
                    $"AccountLabel '{character.AccountLabel}' too long for remaining {maxLabelBytes - 1} bytes.");
            }
            Array.Copy(labelBytes, 0, entry, labelStart, labelBytes.Length);
        }

        WriteUInt16Le(entry, FirstJobSpeciesOffset, (ushort)character.Species);







        var secondJobClass = character.JobTier >= 2
            ? SpeciesDefaults.SecondJobClassIdForFirstJob(character.Species)
            : (ushort)0;
        WriteUInt16Le(entry, SecondJobClassOffset, secondJobClass);

        entry[FTypeOffset] = (byte)((byte)character.FType - 1);

        entry[SpeciesMirrorOffset] = (byte)character.Species;


        entry[SecondJobMirrorOffset] = (byte)(secondJobClass & 0xFF);

        entry[JobTierOffset] = character.JobTier;

        WriteUInt16Le(entry, HairOffset, character.Hair);

        WriteUInt32Le(entry, MoneyOffset, WireLimits.Int32FieldUnsigned(character.Money));

        WriteUInt16Le(entry, LevelOffset, character.Level);


        if (itemStore is not null)
        {
            var equipped = itemStore.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Equipment);
            foreach (var item in equipped)
            {
                if (item.SlotPosition < EquipBlockSlots)
                {
                    WriteUInt32Le(entry, EquipBlockOffset + item.SlotPosition * 4, (uint)item.ItemIndex);
                }
            }
        }

        WriteUInt32Le(entry, EarItemOffset, character.StarterEarItemIndex);

        WriteUInt32Le(entry, TailItemOffset, character.StarterTailItemIndex);

        return entry;
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
}
