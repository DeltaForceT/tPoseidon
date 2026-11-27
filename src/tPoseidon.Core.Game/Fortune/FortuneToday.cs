using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Fortune;

public readonly record struct FortuneCycle(int PeriodDays, bool Inverted);

public static class FortuneToday
{
    public const int FirstCharacterType = 1;
    public const int LastCharacterType = 9;
    public const int PolarBear = 9;
    public const double SecondsPerDay = 86400.0;
    public const float DefaultWeekdayValue = 1.0f;
    public const int WeekdayKeyBase = 1;
    public const int NoCard = 0;

    private static readonly double PiAsFloat = MathF.PI;

    private static readonly FortuneCycle[] CycleByCharacterType =
    [
        new(23, true),
        new(23, false),
        new(33, true),
        new(33, false),
        new(38, true),
        new(38, false),
        new(28, true),
        new(28, false),
        new(23, true),
    ];

    private static readonly FortuneCycle PolarBearExchangeCycle = new(131, false);

    public static FortuneCycle CycleOf(int characterType)
    {
        if (characterType is < FirstCharacterType or > LastCharacterType)
            throw new ArgumentOutOfRangeException(nameof(characterType), characterType, "No daily cycle for this character type.");
        return CycleByCharacterType[characterType - FirstCharacterType];
    }

    public static FortuneCycle ExchangeCycleOf(int characterType) =>
        characterType == PolarBear ? PolarBearExchangeCycle : CycleOf(characterType);

    public static int WeekdayKey(DateTimeOffset now) => (int)now.ToLocalTime().DayOfWeek + WeekdayKeyBase;

    public static bool TryCardId(FortuneTables tables, int characterType, DateTimeOffset createdAt, DateTimeOffset now, out int cardId)
    {
        ArgumentNullException.ThrowIfNull(tables);
        cardId = NoCard;
        if (characterType is < FirstCharacterType or > LastCharacterType) return false;
        cardId = Swing(tables, CycleOf(characterType), createdAt, now);
        return true;
    }

    public static int CardId(FortuneTables tables, int characterType, DateTimeOffset createdAt, DateTimeOffset now) =>
        TryCardId(tables, characterType, createdAt, now, out var cardId) ? cardId : NoCard;

    public static byte Exchange(FortuneTables tables, int characterType, DateTimeOffset createdAt, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (characterType is < FirstCharacterType or > LastCharacterType) return 0;
        var cardId = Swing(tables, ExchangeCycleOf(characterType), createdAt, now);
        return tables.TodayExchangeByCard.TryGetValue(cardId, out var exchange) ? (byte)Math.Clamp(exchange, 0, byte.MaxValue) : (byte)0;
    }

    private static int Swing(FortuneTables tables, FortuneCycle cycle, DateTimeOffset createdAt, DateTimeOffset now)
    {
        var weekday = tables.TodayWeekdayValue.TryGetValue(WeekdayKey(now), out var value) ? value : DefaultWeekdayValue;
        var days = (int)(AgeSeconds(createdAt, now) / SecondsPerDay);
        var sine = Math.Sin(days / (double)cycle.PeriodDays);
        var swing = (sine + sine) * PiAsFloat * weekday;
        return (int)(cycle.Inverted ? -swing : swing);
    }

    private static double AgeSeconds(DateTimeOffset createdAt, DateTimeOffset now)
    {
        var created = createdAt.ToUnixTimeSeconds();
        var current = now.ToUnixTimeSeconds();
        return created < 0 || current < 0 ? 0.0 : current - created;
    }
}
