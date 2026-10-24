namespace tPoseidon.Core.LibConfig;

public readonly record struct RebirthItemEntry(
    int ItemId,
    float HpRestoreRatio,
    float MpRestoreRatio,
    float ExpRestoreRatio,
    float TmExpRestoreRatio);
