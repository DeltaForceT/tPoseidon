using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class ThrowSkills
{
    public const int OnePair = 1194;
    public const int FanOfKnives = 1326;
    public const int FuriousGalderThrow = 1330;
    public const int FourCard = 1495;
    public const int FourCardDo = 1496;
    public const int LadyLuck = 4210;
    public const int PowerThrowSkill = 3101;

    public const int ThrowItemType = 16;
    public const int CardItemType = 11;
    public const int CardSubType = 60;
    public const int DefaultCards = 2;
    public const int FourCardVictims = 4;
    public const int FanOfKnivesCap = 7;
    public const int GalderToll = 500;
    public const int GalderElement = 0x200;
    public const int FourCardLifeMs = 1000;

    public const string RatioColumn = "Ratio";
    public const string ApplyRatioColumn = "ApplyRatio";
    public const string MinCardColumn = "MinCard";
    public const string MaxCardColumn = "MaxCard";
    public const string MonNumColumn = "MonNum";
    public const string RadiusColumn = "Radius";
    public const string AngleColumn = "Angle";

    private const double LuckCardScale = 15.0;
    private const int AvoidStep = 8;
    private const int DigStep = 16;
    private const int ThrowDigStep = 10;
    private const int RandomSpan = 32768;
    private const int RandomPercent = 100;
    private const int RandomMax = 0x7FFF;

    private static readonly (int Below, int Pay)[] GalderBands =
    {
        (15, 1), (25, 200), (35, 300), (45, 400), (65, 500), (80, 600), (90, 700), (95, 800), (100, 1000),
    };

    public static bool IsThrow(int actionId) => actionId is OnePair or FanOfKnives or FuriousGalderThrow;

    public static bool IsThrowable(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Type == ThrowItemType || (definition.Type == CardItemType && definition.SubType == CardSubType);
    }

    public static int CardsFor(SkillTableRow? ladyLuck, int random)
    {
        if (ladyLuck is null) return DefaultCards;
        var min = ladyLuck.Amount(MinCardColumn) ?? DefaultCards;
        var max = ladyLuck.Amount(MaxCardColumn) ?? DefaultCards;
        return min == max ? min : random * (max + 1 - min) / RandomSpan + min;
    }

    public static long OnePairPower(SkillTableRow action, long ap, long hv, long cards, bool ladyLuck)
    {
        ArgumentNullException.ThrowIfNull(action);
        var power = WireLimits.Saturate((ap + AvoidStep * hv) * (double)(action.Value(RatioColumn) ?? 0f));
        return ladyLuck ? WireLimits.Saturate(power * (double)((float)(cards / LuckCardScale) + 1.0f)) : power;
    }

    public static long FourCardPower(SkillTableRow action, long ap, long da)
    {
        ArgumentNullException.ThrowIfNull(action);
        return WireLimits.Saturate((ap + DigStep * da) * (double)(action.Value(RatioColumn) ?? 0f));
    }

    public static long ThrownPower(ItemParamEntry item, long da, float multiplier, float ratio)
    {
        ArgumentNullException.ThrowIfNull(item);
        var power = item.Type == ThrowItemType
            ? WireLimits.Saturate((item.Ap * multiplier + da * ThrowDigStep) * (double)ratio)
            : item.Weight / 2;
        return Math.Max(1, power);
    }

    public static int GalderPay(int random)
    {
        var band = random * RandomPercent / RandomMax;
        foreach (var (below, pay) in GalderBands)
        {
            if (band < below) return pay;
        }
        return GalderBands[^1].Pay;
    }

    public static long GalderPower(long pay, float ratio) => ratio < 0f ? 0 : WireLimits.Saturate(pay * (double)ratio);
}
