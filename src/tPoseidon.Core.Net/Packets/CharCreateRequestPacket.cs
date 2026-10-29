using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Packets;

public sealed class CharCreateRequestPacket
{
    public const int FixedTailBytes = 9;
    public const int MaxNameBytes = 16;

    public string Name { get; init; } = string.Empty;
    public ushort SpeciesSelect { get; init; }
    public byte Hair { get; init; }
    public byte SpeciesDerived { get; init; }
    public byte FType { get; init; }
    public byte StatA { get; init; }
    public byte StatB { get; init; }
    public byte StatC { get; init; }
    public byte StatD { get; init; }

        public Species SpeciesEnum => (Species)SpeciesSelect;

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(Name);
        var nameBytes = ClientText.Encoding.GetBytes(Name);
        if (nameBytes.Length >= MaxNameBytes)
        {
            throw new InvalidOperationException(
                $"Name '{Name}' is {nameBytes.Length} bytes; max is {MaxNameBytes - 1} (plus NUL).");
        }

        var totalSize = nameBytes.Length + 1 + FixedTailBytes;
        var w = new PacketWriter(totalSize);
        w.WriteBytes(nameBytes);
        w.WriteByte(0);
        w.WriteUInt16(SpeciesSelect);
        w.WriteByte(Hair);
        w.WriteByte(SpeciesDerived);
        w.WriteByte(FType);
        w.WriteByte(StatA);
        w.WriteByte(StatB);
        w.WriteByte(StatC);
        w.WriteByte(StatD);
        return w.ToArray();
    }

    public static CharCreateRequestPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 1 + FixedTailBytes)
        {
            throw new InvalidPacketException(
                $"CharCreateRequest payload must be at least {1 + FixedTailBytes} bytes, got {payload.Length}.");
        }

        var nul = payload.IndexOf((byte)0);
        if (nul < 0)
        {
            throw new InvalidPacketException("CharCreateRequest: no NUL terminator found for name.");
        }
        var afterName = payload[(nul + 1)..];
        if (afterName.Length != FixedTailBytes)
        {
            throw new InvalidPacketException(
                $"CharCreateRequest: expected exactly {FixedTailBytes} bytes after name NUL, got {afterName.Length}.");
        }

        var name = ClientText.Encoding.GetString(payload[..nul]);
        var species = (ushort)(afterName[0] | (afterName[1] << 8));
        return new CharCreateRequestPacket
        {
            Name = name,
            SpeciesSelect = species,
            Hair = afterName[2],
            SpeciesDerived = afterName[3],
            FType = afterName[4],
            StatA = afterName[5],
            StatB = afterName[6],
            StatC = afterName[7],
            StatD = afterName[8],
        };
    }

        public string? ValidateRules()
    {
        if (SpeciesSelect is < 1 or > 23) return "species_select out of range (1..23)";
        if (SpeciesDerived is < 1 or > 32) return "species_derived out of range (1..32)";
        if (FType > 3) return "ftype out of range (0..3)";

        var sum = StatA + StatB + StatC + StatD;
        if (sum != 10) return $"stat sum {sum} != 10";

        if (StatA is < 1 or > 4) return "stat_a out of range (1..4)";
        if (StatB is < 1 or > 4) return "stat_b out of range (1..4)";
        if (StatC is < 1 or > 4) return "stat_c out of range (1..4)";
        if (StatD is < 1 or > 4) return "stat_d out of range (1..4)";

        var matchedStat = FType switch
        {
            0 => StatA,
            1 => StatB,
            2 => StatC,
            3 => StatD,
            _ => (byte)0,
        };
        if (matchedStat != 4) return $"ftype-matched stat must be 4 (ftype={FType}, got {matchedStat})";

        return null;
    }
}
