namespace tPoseidon.Hosting;

public static class ClientImePatch
{
    private const int CallOffset = 3;
    private const int CallLength = 5;
    private const byte Nop = 0x90;

    private static readonly byte?[] StartCompositionHandler =
    [
        0x56, 0x8B, 0xF1, 0xE8, null, null, null, null,
        0x83, 0xBE, 0x34, 0x08, 0x00, 0x00, 0x00, 0x75, 0x06, 0xB0, 0x01, 0x5E, 0xC2, 0x08, 0x00,
    ];

    public static int FindUnpatched(ReadOnlySpan<byte> executable) => Find(executable, patched: false);

    public static int FindPatched(ReadOnlySpan<byte> executable) => Find(executable, patched: true);

    public static void Write(Span<byte> executable, int site)
    {
        executable.Slice(site + CallOffset, CallLength).Fill(Nop);
    }

    private static int Find(ReadOnlySpan<byte> executable, bool patched)
    {
        var found = -1;
        for (var start = 0; start + StartCompositionHandler.Length <= executable.Length; start++)
        {
            if (Matches(executable.Slice(start, StartCompositionHandler.Length), patched))
            {
                if (found >= 0)
                {
                    return -1;
                }

                found = start;
            }
        }

        return found;
    }

    private static bool Matches(ReadOnlySpan<byte> window, bool patched)
    {
        for (var i = 0; i < StartCompositionHandler.Length; i++)
        {
            var inCall = i >= CallOffset && i < CallOffset + CallLength;
            if (inCall && patched)
            {
                if (window[i] != Nop)
                {
                    return false;
                }
            }
            else if (StartCompositionHandler[i] is { } expected && window[i] != expected)
            {
                return false;
            }
        }

        return true;
    }
}
