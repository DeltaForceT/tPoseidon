namespace tPoseidon.Core.Game.Quests;

public sealed class DailyCycle
{
    public const string TableName = "DailyTime";
    public const int CheckArgs = 4;

    private const double MillisecondsPerMinute = 60000.0;

    private readonly double[] _phaseEnds;
    private readonly double _cycleMinutes;

    public DailyCycle(IEnumerable<double> phaseMinutes)
    {
        ArgumentNullException.ThrowIfNull(phaseMinutes);
        var ends = new List<double>();
        var total = 0.0;
        foreach (var minutes in phaseMinutes)
        {
            total += Math.Max(0, minutes);
            ends.Add(total);
        }
        _phaseEnds = ends.ToArray();
        _cycleMinutes = total;
    }

    public static DailyCycle Empty { get; } = new(Array.Empty<double>());

    public int Phases => _phaseEnds.Length;

    public int? PhaseAt(DateTimeOffset now)
    {
        if (_cycleMinutes <= 0) return null;
        var cycleMs = (long)(_cycleMinutes * MillisecondsPerMinute);
        var minute = now.ToUnixTimeMilliseconds() % cycleMs / MillisecondsPerMinute;
        for (var phase = 0; phase < _phaseEnds.Length; phase++)
        {
            if (minute < _phaseEnds[phase]) return phase;
        }
        return _phaseEnds.Length - 1;
    }
}
