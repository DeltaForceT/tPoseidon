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

    public static bool Shows(uint flags, uint part) => (flags & part) != 0;

    public static byte[] MessageBytes(string text, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var bytes = encoding.GetBytes(text);
        return bytes.Length > MaxMessageBytes ? [] : bytes;
    }

    public static ushort StatLevelOf(int points) => (ushort)StatusValueChart.StatLevel(points);

    public static byte RemainderOf(int points) => (byte)(points % StatusValueChart.PointsPerStatLevel);

    public static uint Total(long value) => tPoseidon.Core.Common.WireLimits.Int32FieldUnsigned(value);

    public static uint Bonus(long total, long table) => unchecked((uint)tPoseidon.Core.Common.WireLimits.SaturateInt32(total - table));
}
