using System.Buffers.Binary;

namespace tPoseidon.Core.Net.World;

public static class GuideHistoryFrames
{
    public const int SystemGuideSize = 256;
    public const int TutorialGuideSize = 8;
    public const int HistorySize = SystemGuideSize + TutorialGuideSize;

    public static byte[] History(ReadOnlySpan<byte> systemGuide, ulong tutorialGuide)
    {
        var payload = new byte[HistorySize];
        systemGuide[..Math.Min(systemGuide.Length, SystemGuideSize)].CopyTo(payload);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(SystemGuideSize), tutorialGuide);
        return payload;
    }

    public static byte[]? ReadSystemGuide(ReadOnlySpan<byte> payload) =>
        payload.Length == SystemGuideSize ? payload.ToArray() : null;

    public static ulong? ReadTutorialGuide(ReadOnlySpan<byte> payload) =>
        payload.Length == TutorialGuideSize ? BinaryPrimitives.ReadUInt64LittleEndian(payload) : null;

    public static string Stored(ReadOnlySpan<byte> systemGuide) => Convert.ToHexString(systemGuide);

    public static byte[] FromStored(string? stored)
    {
        if (stored is not { Length: SystemGuideSize * 2 } || !stored.All(Uri.IsHexDigit))
        {
            return new byte[SystemGuideSize];
        }
        return Convert.FromHexString(stored);
    }
}
