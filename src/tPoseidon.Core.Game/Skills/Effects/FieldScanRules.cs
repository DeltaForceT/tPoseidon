namespace tPoseidon.Core.Game.Skills.Effects;

public static class FieldScanRules
{
    public static bool HasRoom(int trackedCount, int limit) => trackedCount < limit;

    public static IReadOnlyList<T> Candidates<T>(IEnumerable<T> inArea, int limit)
    {
        ArgumentNullException.ThrowIfNull(inArea);
        return inArea.Take(Math.Max(0, limit)).ToList();
    }
}
