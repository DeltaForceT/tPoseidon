namespace tPoseidon.Core.Game.Stats;

public readonly record struct RecoveryAmounts(int Hp, int Mp);

public readonly record struct RecoveryReport(long? Hp, long? Mp);

public sealed class NaturalRecovery
{
    public const int PeriodMs = 2000;
    public const int PauseAfterDamageMs = 6000;
    public const int ReportsWhileFull = 3;

    public const byte StandAction = 0x00;
    public const byte SitAction = 0x02;
    public const byte LieAction = 0x0D;

    private static readonly RecoveryAmounts Standing = new(5, 5);

    private static readonly RecoveryAmounts[] SittingByType =
    {
        new(15, 10),
        new(10, 15),
        new(15, 15),
        new(15, 10),
    };

    private static readonly RecoveryAmounts[] LyingByType =
    {
        new(30, 20),
        new(20, 30),
        new(30, 30),
        new(30, 20),
    };

    private readonly object _gate = new();
    private int _pauseMs;
    private int _hpCountdownMs;
    private int _mpCountdownMs;
    private int _hpReportsWhileFull;
    private int _mpReportsWhileFull;

    public static RecoveryAmounts? AmountsFor(int characterType, byte action)
    {
        if (characterType < 0 || characterType >= SittingByType.Length) return null;
        return action switch
        {
            StandAction => Standing,
            SitAction => SittingByType[characterType],
            LieAction => LyingByType[characterType],
            _ => null,
        };
    }

    public void PauseAfterDamage()
    {
        lock (_gate)
        {
            _pauseMs = PauseAfterDamageMs;
        }
    }

    public int Period { get; set; } = PeriodMs;

    public RecoveryReport Tick(int elapsedMs, long hp, long maxHp, long mp, long maxMp, RecoveryAmounts amounts)
    {
        lock (_gate)
        {
            if (_pauseMs >= 1)
            {
                _pauseMs -= elapsedMs;
                if (_pauseMs < 0) _pauseMs = 0;
                return default;
            }

            return new RecoveryReport(
                Recover(elapsedMs, hp, maxHp, amounts.Hp, Period, ref _hpCountdownMs, ref _hpReportsWhileFull),
                Recover(elapsedMs, mp, maxMp, amounts.Mp, Period, ref _mpCountdownMs, ref _mpReportsWhileFull));
        }
    }

    private static long? Recover(
        int elapsedMs, long value, long max, int amount, int periodMs, ref int countdownMs, ref int reportsWhileFull)
    {
        countdownMs -= elapsedMs;
        if (countdownMs >= 1) return null;
        countdownMs = periodMs;

        if (value < max)
        {
            var recovered = value + amount;
            reportsWhileFull = 0;
            return max < recovered ? max : recovered;
        }
        if (reportsWhileFull < ReportsWhileFull)
        {
            reportsWhileFull++;
            return value;
        }
        return null;
    }
}
