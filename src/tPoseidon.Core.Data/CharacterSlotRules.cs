namespace tPoseidon.Core.Data;

public static class CharacterSlotRules
{
    public const string Variable = "MT_CHARACTER_SLOTS";
    public const int Default = 3;
    public const int Min = 1;
    public const int Max = 8;

    public static bool IsValid(int slots) => slots is >= Min and <= Max;

    public static int FromEnvironment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Default;
        if (!int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var slots) || !IsValid(slots))
        {
            throw new InvalidOperationException($"{Variable} must be a whole number from {Min} to {Max} (got '{value}').");
        }
        return slots;
    }

    public static int Quota(int limit, IEnumerable<int> usedSlots)
    {
        ArgumentNullException.ThrowIfNull(usedSlots);
        var quota = limit;
        foreach (var slot in usedSlots)
        {
            quota = Math.Max(quota, slot + 1);
        }
        return Math.Min(quota, Max);
    }

    public static int FirstFreeSlot(int limit, IEnumerable<int> usedSlots)
    {
        ArgumentNullException.ThrowIfNull(usedSlots);
        var used = usedSlots.ToHashSet();
        for (var slot = 0; slot < limit; slot++)
        {
            if (!used.Contains(slot)) return slot;
        }
        return -1;
    }
}
