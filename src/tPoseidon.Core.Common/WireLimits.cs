namespace tPoseidon.Core.Common;

public static class WireLimits
{
    public const long Int32Maximum = int.MaxValue;
    public const long UInt16Maximum = ushort.MaxValue;
    public const long Int16Maximum = short.MaxValue;

    public static int Int32Field(long value) => (int)Math.Clamp(value, 0L, Int32Maximum);

    public static int Int32Field(double value) => double.IsNaN(value) ? 0 : (int)Math.Clamp(value, 0d, Int32Maximum);

    public static uint Int32FieldUnsigned(long value) => (uint)Int32Field(value);

    public static uint Int32FieldUnsigned(ulong value) => (uint)Math.Min(value, (ulong)Int32Maximum);

    public static ushort UInt16Field(long value) => (ushort)Math.Clamp(value, 0L, UInt16Maximum);

    public static short Int16Field(long value) => (short)Math.Clamp(value, 0L, Int16Maximum);

    public static int SaturateInt32(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

    public static int SaturateInt32(double value) =>
        double.IsNaN(value) ? 0 : (int)Math.Clamp(value, int.MinValue, int.MaxValue);

    public static long Saturate(double value) =>
        double.IsNaN(value) ? 0L : value >= long.MaxValue ? long.MaxValue : value <= long.MinValue ? long.MinValue : (long)value;
}
