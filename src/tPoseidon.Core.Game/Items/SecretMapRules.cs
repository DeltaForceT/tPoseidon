namespace tPoseidon.Core.Game.Items;

public static class SecretMapRules
{
    public const int FirstCardId = 26500;
    public const int CardCount = 16;
    public const int MapItemId = 64130;

    public static bool IsCard(int itemId) => itemId >= FirstCardId && itemId < FirstCardId + CardCount;

    public static bool IsCompleteSet(IReadOnlyList<int> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        return itemIds.Count == CardCount && itemIds.All(IsCard) && itemIds.Distinct().Count() == CardCount;
    }
}
