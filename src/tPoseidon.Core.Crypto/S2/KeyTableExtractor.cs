namespace tPoseidon.Core.Crypto.S2;

public sealed record KeyTableExtraction(byte[]? Table, string Outcome);

public static class KeyTableExtractor
{
    private const int RowCount = 256;
    private const int RowLength = 256;
    private const byte MovByteImmediateOpcode = 0xC6;
    private const int ModRmModeShift = 6;
    private const int ModRmRegMask = 0x38;
    private const int ModRmRmMask = 0x07;
    private const int ModeNoDisplacement = 0;
    private const int ModeDisp8 = 1;
    private const int ModeDisp32 = 2;
    private const int RmSib = 4;
    private const int RmDisp32Only = 5;
    private const int MaxGapBetweenStores = 16;

    public static KeyTableExtraction Extract(ReadOnlySpan<byte> image)
    {
        var rows = new byte[RowCount][];
        var runRow = -1;
        var runStart = 0;
        var runColumns = new bool[RowLength];
        var runValues = new byte[RowLength];
        var runCount = 0;
        var lastStoreEnd = -1;

        for (var i = 0; i < image.Length; i++)
        {
            if (!TryReadStore(image, i, out var displacement, out var value, out var storeLength))
            {
                continue;
            }

            var row = displacement >> 8;
            var column = displacement & 0xFF;
            var continues = row == runRow && i - lastStoreEnd <= MaxGapBetweenStores && !runColumns[column];
            if (!continues)
            {
                runRow = row;
                runStart = i;
                runCount = 0;
                Array.Clear(runColumns);
            }

            runColumns[column] = true;
            runValues[column] = value;
            runCount++;
            lastStoreEnd = i + storeLength;
            i += storeLength - 1;

            if (runCount == RowLength)
            {
                if (rows[row] is { } existing && !existing.AsSpan().SequenceEqual(runValues))
                {
                    return new KeyTableExtraction(null, $"row {row} is written twice with different values (second run at file offset 0x{runStart:X}).");
                }

                rows[row] = (byte[])runValues.Clone();
                runRow = -1;
            }
        }

        var missing = Enumerable.Range(0, RowCount).Where(row => rows[row] is null).ToList();
        if (missing.Count > 0)
        {
            return new KeyTableExtraction(null, $"{missing.Count} of {RowCount} rows not found (first missing row {missing[0]}).");
        }

        var table = new byte[RowCount * RowLength];
        for (var row = 0; row < RowCount; row++)
        {
            if (rows[row]!.Distinct().Count() != RowLength)
            {
                return new KeyTableExtraction(null, $"row {row} is not a permutation of 0..255.");
            }

            rows[row]!.CopyTo(table, row * RowLength);
        }

        return new KeyTableExtraction(table, "all 256 rows found, each a permutation.");
    }

    private static bool TryReadStore(ReadOnlySpan<byte> image, int at, out int displacement, out byte value, out int length)
    {
        displacement = 0;
        value = 0;
        length = 0;
        if (at + 2 >= image.Length || image[at] != MovByteImmediateOpcode)
        {
            return false;
        }

        var modRm = image[at + 1];
        var mode = modRm >> ModRmModeShift;
        var rm = modRm & ModRmRmMask;
        if ((modRm & ModRmRegMask) != 0 || rm == RmSib)
        {
            return false;
        }

        switch (mode)
        {
            case ModeNoDisplacement when rm != RmDisp32Only:
                length = 3;
                break;
            case ModeDisp8 when at + 3 < image.Length:
                displacement = (sbyte)image[at + 2];
                length = 4;
                break;
            case ModeDisp32 when at + 6 < image.Length:
                displacement = BitConverter.ToInt32(image.Slice(at + 2, 4));
                length = 7;
                break;
            default:
                return false;
        }

        if (displacement is < 0 or >= RowCount * RowLength)
        {
            return false;
        }

        value = image[at + length - 1];
        return true;
    }
}
