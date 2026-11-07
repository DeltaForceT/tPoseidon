namespace tPoseidon.Hosting;

public static class ClientGmMailIconPatch
{
    private const int FrameAt = 8;
    private const byte OriginalFrame = 0x30;
    private const byte GmFrame = 0x38;

    private static readonly byte?[] Unpatched = ClientCode.Pattern("80 7C 24 10 00 74 07 BF 30 00 00 00 EB ?? 8B 44 24 0C 85 C0 75 14");
    private static readonly byte?[] Patched = ClientCode.Pattern("80 7C 24 10 00 74 07 BF 38 00 00 00 EB ?? 8B 44 24 0C 85 C0 75 14");

    public static int FindUnpatched(ReadOnlySpan<byte> executable)
    {
        var site = ClientCode.FindUnique(executable, Unpatched);
        return site < 0 ? -1 : site + FrameAt;
    }

    public static int FindPatched(ReadOnlySpan<byte> executable)
    {
        var site = ClientCode.FindUnique(executable, Patched);
        return site < 0 ? -1 : site + FrameAt;
    }

    public static void Write(Span<byte> executable, int frameByte)
    {
        if (executable[frameByte] != OriginalFrame)
        {
            throw new InvalidOperationException("The mail sender icon constant is not the one this patch expects.");
        }

        executable[frameByte] = GmFrame;
    }
}
