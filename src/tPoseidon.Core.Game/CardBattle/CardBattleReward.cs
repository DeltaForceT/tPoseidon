namespace tPoseidon.Core.Game.CardBattle;

public readonly record struct CardBattleStatus(
    byte Wins,
    short Score,
    bool OverDistance,
    uint WonCardItemId,
    bool Winner,
    uint PresentItemId,
    bool Special,
    bool Perfect,
    short SpecialBonus,
    bool NoExp)
{
    public uint LostCardItemId { get; init; }

    public uint SecondWonCardItemId { get; init; }

    public uint SecondLostCardItemId { get; init; }

    public ulong OpponentCharacterId { get; init; }

    public bool OpponentHoldsSet { get; init; }

    public bool OpponentUsedChance { get; init; }
}

public readonly record struct CardBattleRewardResult(int Exp, int Money);

public sealed record CardBattleRewardBlock(
    int Level,
    bool Special,
    int Exp,
    uint ExpTotal,
    uint WonCardItemId,
    uint LostCardItemId,
    int Money,
    uint PresentItemId,
    uint SecondWonCardItemId,
    uint SecondLostCardItemId)
{
    public static CardBattleRewardBlock None { get; } = new(0, false, 0, 0, 0, 0, 0, 0, 0, 0);
}

public static class CardBattleReward
{
    public const double LevelSquareFactor = 0.25;
    public const double LoserShare = 0.25;
    public const double SpecialBonusStep = 0.025;
    public const int MoneyPerScore = 2;

    public static int WinnerExp(int level, byte wins, short score)
    {
        var scoreFactor = score > 0 ? (float)score : 1f;
        var winsPart = (int)((float)wins * scoreFactor * level);
        var levelPart = Math.Max(1, (int)((float)(level * level) * LevelSquareFactor));
        return Math.Max(1, winsPart + levelPart);
    }

    public static int LoserExp(int level, byte wins, short score) =>
        Math.Max(1, (int)((float)WinnerExp(level, wins, score) * LoserShare));

    public static float SpecialMultiplier(short bonus)
    {
        var multiplier = (float)(bonus * SpecialBonusStep + 1.0);
        return multiplier > 0f ? multiplier : 1f;
    }

    public static CardBattleRewardResult Compute(int level, CardBattleStatus status)
    {
        int exp;
        var multiplier = 1f;
        var money = 0;
        if (status.Winner)
        {
            exp = status.OverDistance ? 0 : WinnerExp(level, status.Wins, status.Score);
            if (status.Special && !status.OverDistance) multiplier = SpecialMultiplier(status.SpecialBonus);
            if (status.Perfect && !status.OverDistance) money = status.Score * MoneyPerScore;
        }
        else
        {
            exp = LoserExp(level, status.Wins, status.Score);
        }
        var final = (int)Math.Round(exp * multiplier, MidpointRounding.ToEven);
        if (status.NoExp) final = 0;
        return new CardBattleRewardResult(final, money);
    }
}
