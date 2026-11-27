namespace tPoseidon.Core.Game.CardBattle;

public sealed record CardBattlePoints(int Point0, int Point1, int Point2, int Point3, int Point4)
{
    public const string Key0 = "CardBattlePoint0";
    public const string Key1 = "CardBattlePoint1";
    public const string Key2 = "CardBattlePoint2";
    public const string Key3 = "CardBattlePoint3";
    public const string Key4 = "CardBattlePoint4";

    public static CardBattlePoints Default { get; } = new(2000, 1500, 1200, 800, 500);

    public int Grade(int winPoint)
    {
        if (winPoint >= Point0) return 5;
        if (winPoint >= Point1) return 4;
        if (winPoint >= Point2) return 3;
        if (winPoint >= Point3) return 2;
        if (winPoint >= Point4) return 1;
        return 0;
    }
}
