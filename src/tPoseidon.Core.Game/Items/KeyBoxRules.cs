using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public enum KeyBoxRefusal : byte
{
    None = 0,
    NotUsable = 1,
    KeyRequired = 2,
    KeyRejected = 3,
    TooHeavy = 4,
    BagFull = 5,
    Failed = 6,
}

public readonly record struct KeyBoxItem(int ItemId, int Effect, int Stack);

public readonly record struct KeyBoxChoice(KeyBoxRefusal Refusal, int PresentId, bool UsesKey);

public static class KeyBoxRules
{
    public const int BoxEffect = 49;
    public const int KeyEffect = 50;
    public const int SafeZoneOption = 0x20000000;
    public const int MaxKeys = 3;
    public const int MaxResultOperations = 3;
    public const byte OpenRefusedCode = 1;
    public const int RollLow = 1;
    public const int RollHigh = 100;

    private static readonly int[] SafeZoneTypes = [1, 2, 5];

    public static bool CanOpen(KeyBoxItem? box, int options, int zoneType) =>
        box is { Effect: BoxEffect, Stack: >= 1 } && ((options & SafeZoneOption) == 0 || SafeZoneTypes.Contains(zoneType));

    public static int RollPercent(Func<int, int> randomBelow)
    {
        ArgumentNullException.ThrowIfNull(randomBelow);
        return RollLow + randomBelow(RollHigh - RollLow);
    }

    public static KeyBoxChoice Choose(KeyBoxItem? box, KeyboxEntry? row, bool keyGiven, KeyBoxItem? key, int? keyRate, Func<int> rollPercent)
    {
        ArgumentNullException.ThrowIfNull(rollPercent);
        if (box is not { Effect: BoxEffect, Stack: >= 1 }) return Refused(KeyBoxRefusal.NotUsable);
        if (row is null) return Refused(KeyBoxRefusal.Failed);
        if (!keyGiven)
        {
            return row.OnlyKey ? Refused(KeyBoxRefusal.KeyRequired) : new KeyBoxChoice(KeyBoxRefusal.None, row.NormalItemId, false);
        }
        if (key is not { Effect: KeyEffect, Stack: >= 1 } || !row.KeyItemIds.Take(MaxKeys).Contains(key.Value.ItemId)) return Refused(KeyBoxRefusal.KeyRejected);
        if (keyRate is not { } rate) return Refused(KeyBoxRefusal.Failed);
        var present = rollPercent() <= row.RareBRatePercent + rate ? row.RareItemIdB : row.RareItemIdA;
        return new KeyBoxChoice(KeyBoxRefusal.None, present, true);
    }

    public static KeyBoxRefusal Fits(long bagWeight, long rewardWeight, long maxWeight, int newStacks, int freeSlots, int resultOperations)
    {
        if (bagWeight + rewardWeight >= maxWeight) return KeyBoxRefusal.TooHeavy;
        if (newStacks > freeSlots) return KeyBoxRefusal.BagFull;
        if (resultOperations > MaxResultOperations) return KeyBoxRefusal.Failed;
        return KeyBoxRefusal.None;
    }

    private static KeyBoxChoice Refused(KeyBoxRefusal refusal) => new(refusal, 0, false);
}
