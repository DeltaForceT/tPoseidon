namespace tPoseidon.Core.Game.Drilling;

public static class AutoDrillRules
{
    public const ushort NotWalking = 2;
    public const ushort AlreadyThere = 3;
    public const ushort NoDrill = 5;
    public const ushort NotNormalForm = 7;
    public const ushort ModeOff = 8;
    public const ushort MapRefuses = 9;
    public const ushort NoAutoDrillPet = 10;
    public const int MapOptionNoAutoDrill = 0x1;
    public const byte FormRefusalNotice = 0x10;

    public static ushort? ModeError(bool autoOn, int mapOption, int? petItemId, IReadOnlyDictionary<int, int> autoDrillPets)
    {
        ArgumentNullException.ThrowIfNull(autoDrillPets);
        if (!autoOn) return ModeOff;
        if ((mapOption & MapOptionNoAutoDrill) != 0) return MapRefuses;
        if (petItemId is not { } pet || !autoDrillPets.ContainsKey(pet)) return NoAutoDrillPet;
        return null;
    }

    public static bool IsBeforeCoolTime(int coolTimeMs, double elapsedMs) => coolTimeMs * 2 / 3 > elapsedMs;

    public static int FindTmExp(int tmExp, bool autoOn) => autoOn ? (int)(tmExp * 0.5) : tmExp;

    public static int PickableCount(int maxWeight, int bagWeight, int weight, int count)
    {
        var free = maxWeight - bagWeight;
        if (free < 1 || weight < 0 || free < weight) return 0;
        return weight > 0 ? Math.Min(free / weight, count) : count;
    }
}
