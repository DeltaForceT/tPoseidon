using System.Text;

namespace tPoseidon.Core.Game.Progression;

public static class ProfileViewRules
{
    public const uint ShowInfo = 1;
    public const uint ShowEquipment = 2;
    public const uint ShowCollection = 4;
    public const uint DefaultFlags = ShowInfo | ShowEquipment;
    public const int MaxMessageBytes = 200;
    public const int StatCount = 12;
    public const int ResistSlots = 22;
    public const int GrowthGroups = 4;
    public const uint UnknownPlayerCode = 1;
    public const byte CoverLetterRefusedCode = 1;
    public const byte Apostrophe = 0x27;
    public const byte ApostropheReplacement = 0x60;
    public const int MostCollectionRecords = byte.MaxValue;

    public static bool Shows(uint flags, uint part) => (flags & part) != 0;

    public static byte[] MessageBytes(string text, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var bytes = encoding.GetBytes(text);
        return bytes.Length > MaxMessageBytes ? [] : bytes;
    }

    public static byte[] SanitizeCoverLetter(ReadOnlySpan<byte> letter)
    {
        var clean = letter.ToArray();
        for (var i = 0; i < clean.Length; i++)
        {
            if (clean[i] == Apostrophe) clean[i] = ApostropheReplacement;
        }
        return clean;
    }

    public static ushort StatLevelOf(int points) => (ushort)StatusValueChart.StatLevel(points);

    public static byte RemainderOf(int points) => (byte)(points % StatusValueChart.PointsPerStatLevel);

    public static uint Total(long value) => tPoseidon.Core.Common.WireLimits.Int32FieldUnsigned(value);

    public static uint Bonus(long total, long table, long abilityShare = 0) =>
        unchecked((uint)tPoseidon.Core.Common.WireLimits.SaturateInt32(total - table - abilityShare));
}
