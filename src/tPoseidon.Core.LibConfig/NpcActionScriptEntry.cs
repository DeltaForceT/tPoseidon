namespace tPoseidon.Core.LibConfig;

public sealed record NpcActionScriptEntry
{
    public required int Type { get; init; }

    public required int MaxCnt { get; init; }

    public required IReadOnlyList<NpcActionScriptStep> Steps { get; init; }
}

public readonly record struct NpcActionScriptStep(int Action, int Loop, int AniTime, string Speech);
