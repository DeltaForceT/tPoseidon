namespace tPoseidon.Core.LibConfig;

public sealed record ItemStatCheckEntry(int ItemIndex, IReadOnlyList<int> Minimums)
{
    public const int NoRequirement = -32768;
    public const int MaxItems = 0x20;
    public const int Passed = 1;
    public const int TooWeak = 2;
    public const int ItemMissing = 3;
}
