using System.Buffers.Binary;

namespace tPoseidon.Hosting;

public static class ClientStorageWeightPatch
{
    public const int OriginalFormula = 0;

    private const int EntryBytes = 8;
    private const int ValueAt = 1;
    private const byte LoadValue = 0xB8;

    private static readonly byte[] OriginalEntry = { 0x8B, 0x44, 0x24, 0x04, 0x83, 0xF8, 0x05, 0x7F };
    private static readonly byte[] ReturnTail = { 0xC2, 0x08, 0x00 };

    private static readonly byte?[] Original = ClientCode.Pattern("8B 44 24 04 83 F8 05 7F 08 B8 C4 09 00 00 C2 08 00 69 C0 C8 00 00 00 05 DC 05 00 00");
    private static readonly byte?[] Fixed = ClientCode.Pattern("B8 ?? ?? ?? ?? C2 08 00 08 B8 C4 09 00 00 C2 08 00 69 C0 C8 00 00 00 05 DC 05 00 00");

    public static int FindOriginal(ReadOnlySpan<byte> executable) => ClientCode.FindUnique(executable, Original);

    public static int FindFixed(ReadOnlySpan<byte> executable) => ClientCode.FindUnique(executable, Fixed);

    public static int FixedWeight(ReadOnlySpan<byte> executable, int site) =>
        BinaryPrimitives.ReadInt32LittleEndian(executable.Slice(site + ValueAt, sizeof(int)));

    public static int FindPending(ReadOnlySpan<byte> executable, int weightLimit)
    {
        if (weightLimit <= OriginalFormula) return FindFixed(executable);
        var original = FindOriginal(executable);
        if (original >= 0) return original;
        var current = FindFixed(executable);
        return current >= 0 && FixedWeight(executable, current) != weightLimit ? current : -1;
    }

    public static void Write(Span<byte> executable, int site, int weightLimit)
    {
        var entry = executable.Slice(site, EntryBytes);
        var isOriginal = entry.SequenceEqual(OriginalEntry);
        var isFixed = entry[0] == LoadValue && entry[(ValueAt + sizeof(int))..].SequenceEqual(ReturnTail);
        if (!isOriginal && !isFixed)
        {
            throw new InvalidOperationException("The storage weight limit code is not the one this patch expects.");
        }

        if (weightLimit <= OriginalFormula)
        {
            OriginalEntry.CopyTo(entry);
            return;
        }

        entry[0] = LoadValue;
        BinaryPrimitives.WriteInt32LittleEndian(entry.Slice(ValueAt, sizeof(int)), weightLimit);
        ReturnTail.CopyTo(entry[(ValueAt + sizeof(int))..]);
    }
}
