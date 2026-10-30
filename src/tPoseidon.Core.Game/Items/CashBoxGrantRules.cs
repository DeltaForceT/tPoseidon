using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Items;

public static class CashBoxGrantRules
{
    public const int MaxRareRows = 200;
    public const int GameMasterSellCode = 0;

    public static bool Accepts(byte itemClass) => itemClass is ChargedItemRules.CommonClass or ChargedItemRules.RareClass;

    public static IReadOnlyList<ChargedItemGrant>? Build(uint accountId, int itemIndex, byte itemClass, ushort count, int? drillLife, ushort requestedHours, short? shopHours)
    {
        if (count == 0) return null;
        if (itemClass == ChargedItemRules.CommonClass)
        {
            return [new ChargedItemGrant(accountId, itemClass, itemIndex, (short)Math.Min(count, short.MaxValue), 0, 0, GameMasterSellCode, ChargedItemRules.OtherSellType)];
        }
        if (itemClass != ChargedItemRules.RareClass || count > MaxRareRows) return null;
        var hours = requestedHours > 0 ? (short)Math.Min(requestedHours, (ushort)short.MaxValue) : shopHours is > 0 ? shopHours.Value : (short)0;
        var flag = hours > 0 ? ChargedItemRules.TimedFlag : (byte)0;
        var attribute = (short)Math.Clamp(drillLife is > 0 ? drillLife.Value : 1, 1, short.MaxValue);
        return Enumerable.Range(0, count)
            .Select(_ => new ChargedItemGrant(accountId, itemClass, itemIndex, attribute, hours, flag, GameMasterSellCode, ChargedItemRules.OtherSellType))
            .ToArray();
    }
}
