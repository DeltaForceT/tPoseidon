namespace tPoseidon.Core.Net.Social;

public sealed record PartyShareEntry(float ExpBonus, float TmBonus, ulong CharacterId, ushort ExpPercent, ushort TmPercent);

public sealed record PartyShare(byte ShareFlag, byte ItemShare, IReadOnlyList<PartyShareEntry> Entries);

public static class PartyBonusTables
{
    public const int MostGroupSize = 6;

    private static readonly float[][] ExpRates =
    {
        new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f },
        new[] { 1f, 1f, 1.2f, 1.3f, 1.3f, 1.4f, 1.5f },
        new[] { 1f, 1f, 1.3f, 1.5f, 1.5f, 2.0f, 2.5f },
        new[] { 1f, 1f, 1.2f, 1.4f, 1.4f, 1.7f, 2.0f },
        new[] { 1f, 1f, 1.3f, 1.5f, 1.5f, 2.0f, 2.5f },
        new[] { 1f, 1f, 1.2f, 1.3f, 1.3f, 1.4f, 1.5f },
        new[] { 1f, 1f, 1.2f, 1.3f, 1.3f, 1.4f, 1.5f },
        new[] { 1f, 1f, 1.3f, 1.5f, 1.5f, 2.0f, 2.5f },
        new[] { 1f, 1f, 1.3f, 1.5f, 1.5f, 2.0f, 2.5f },
        new[] { 1f, 1f, 1.3f, 1.4f, 1.4f, 1.6f, 1.8f },
        new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f },
    };

    private static readonly float[][] TmRates =
    {
        new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f },
        new[] { 1f, 1f, 1.2f, 1.3f, 1.3f, 1.5f, 1.8f },
        new[] { 1f, 1f, 1.3f, 1.5f, 1.5f, 2.5f, 3.0f },
        new[] { 1f, 1f, 1.2f, 1.4f, 1.4f, 2.0f, 2.5f },
        new[] { 1f, 1f, 1.3f, 1.5f, 1.5f, 2.5f, 3.0f },
        new[] { 1f, 1f, 1.2f, 1.3f, 1.3f, 1.5f, 1.8f },
        new[] { 1f, 1f, 1.2f, 1.3f, 1.3f, 1.5f, 1.8f },
        new[] { 1f, 1f, 1.3f, 1.5f, 1.5f, 2.5f, 3.0f },
        new[] { 1f, 1f, 1.3f, 1.5f, 1.5f, 2.5f, 3.0f },
        new[] { 1f, 1f, 1.3f, 1.5f, 1.5f, 1.8f, 2.0f },
        new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f },
    };

    public static float ExpMultiplier(PartyType type, int groupSize) => ExpRates[(int)type][Math.Clamp(groupSize, 0, MostGroupSize)];

    public static float TmMultiplier(PartyType type, int groupSize) => TmRates[(int)type][Math.Clamp(groupSize, 0, MostGroupSize)];

    public static ushort Percent(byte division, int groupSize, int memberPercent, int sumPercent) => division switch
    {
        PartyDivision.Equal => 100,
        PartyDivision.EqualRounded => (ushort)Math.Floor(100.0 / groupSize + 0.5),
        _ => sumPercent <= 0 ? (ushort)0 : (ushort)Math.Floor(100.0 * memberPercent / sumPercent + 0.5),
    };

    public static byte[] EncodeShare(PartyShare share)
    {
        ArgumentNullException.ThrowIfNull(share);
        var w = new PacketWriter(3 + share.Entries.Count * 20);
        w.WriteByte(share.ShareFlag);
        w.WriteByte(share.ItemShare);
        w.WriteByte((byte)share.Entries.Count);
        foreach (var entry in share.Entries)
        {
            w.WriteUInt32(BitConverter.SingleToUInt32Bits(entry.ExpBonus));
            w.WriteUInt32(BitConverter.SingleToUInt32Bits(entry.TmBonus));
            w.WriteUInt64(entry.CharacterId);
            w.WriteUInt16(entry.ExpPercent);
            w.WriteUInt16(entry.TmPercent);
        }
        return w.ToArray();
    }

    public static PartyShare DecodeShare(ReadOnlySpan<byte> data)
    {
        var r = new PacketReader(data);
        var flag = r.ReadByte();
        var itemShare = r.ReadByte();
        var count = r.ReadByte();
        var entries = new List<PartyShareEntry>(count);
        for (var i = 0; i < count; i++)
        {
            entries.Add(new PartyShareEntry(
                BitConverter.UInt32BitsToSingle(r.ReadUInt32()), BitConverter.UInt32BitsToSingle(r.ReadUInt32()),
                r.ReadUInt64(), r.ReadUInt16(), r.ReadUInt16()));
        }
        return new PartyShare(flag, itemShare, entries);
    }

    public static byte[] BonusFor(PartyType type, float expBonus)
    {
        var w = new PacketWriter(5);
        w.WriteByte((byte)type);
        w.WriteUInt32(BitConverter.SingleToUInt32Bits(expBonus));
        return w.ToArray();
    }
}
