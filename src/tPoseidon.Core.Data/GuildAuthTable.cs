namespace tPoseidon.Core.Data;

public static class GuildAuthTable
{
    public const int Grades = 7;
    public const int Stride = 8;
    public const int Length = Grades * Stride;
    public const ushort MasterRights = 0xFFFF;

    private static readonly ushort[] BitOfIndex = { 0x01, 0x02, 0x04, 0x10, 0x08, 0x20, 0x40, 0x80 };

    public static byte[] Default()
    {
        var table = new byte[Length];
        Array.Fill(table, (byte)1, 0, Stride);
        for (var grade = 1; grade <= 2; grade++)
        {
            Array.Fill(table, (byte)1, grade * Stride, 6);
        }
        return table;
    }

    public static bool IsValid(ReadOnlySpan<byte> table)
    {
        if (table.Length != Length) return false;
        foreach (var flag in table)
        {
            if (flag > 1) return false;
        }
        return true;
    }

    public static byte[] WithMasterRow(ReadOnlySpan<byte> table)
    {
        var copy = table.ToArray();
        Array.Fill(copy, (byte)1, 0, Stride);
        return copy;
    }

    public static ushort RightsOf(ReadOnlySpan<byte> table, int gradeIndex)
    {
        if (table.Length != Length || gradeIndex < 0 || gradeIndex >= Grades) return 0;
        ushort rights = 0;
        for (var index = 0; index < Stride; index++)
        {
            if (table[gradeIndex * Stride + index] != 0) rights |= BitOfIndex[index];
        }
        return rights;
    }

    public static byte[] OwnRow(int gradeIndex, ushort rights)
    {
        var table = new byte[Length];
        if (gradeIndex < 0 || gradeIndex >= Grades) return table;
        for (var index = 0; index < Stride; index++)
        {
            if ((rights & BitOfIndex[index]) != 0) table[gradeIndex * Stride + index] = 1;
        }
        return table;
    }
}
