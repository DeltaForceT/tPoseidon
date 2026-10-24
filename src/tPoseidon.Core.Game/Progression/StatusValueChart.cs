using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Characters;

namespace tPoseidon.Core.Game.Progression;

public sealed class StatusValueChart
{
    public const int FirstStatLevel = 1;
    public const int LastStatLevel = 999;
    public const int StatCount = 12;
    public const int PointsPerStatLevel = 4;

    public const int Ap = 0;
    public const int Ac = 1;
    public const int Dx = 2;
    public const int Mp = 3;
    public const int Ma = 4;
    public const int Md = 5;
    public const int Wt = 6;
    public const int Da = 7;
    public const int Lk = 8;
    public const int Hp = 9;
    public const int Dp = 10;
    public const int Hv = 11;

    private readonly int[] _values;

    private StatusValueChart(int[] values) => _values = values;

    public static int StatLevel(int points) => points / PointsPerStatLevel + 1;

    public int Value(int statLevel, int stat) =>
        statLevel >= FirstStatLevel && statLevel <= LastStatLevel && stat >= 0 && stat < StatCount
            ? _values[statLevel * StatCount + stat]
            : 0;

    public int ForPoints(int points, int stat) => Value(StatLevel(points), stat);

    public static short[] PointsOf(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return new[]
        {
            (short)character.ApPoints, (short)character.AcPoints, (short)character.DxPoints,
            (short)character.MaxMpPoints, (short)character.MaPoints, (short)character.MdPoints,
            (short)character.MaxWtPoints, (short)character.DaPoints, (short)character.LkPoints,
            (short)character.MaxHpPoints, (short)character.DpPoints, (short)character.HvPoints,
        };
    }

    public EffectiveStats Compute(IReadOnlyList<short> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count != StatCount)
        {
            throw new ArgumentException($"A character has {StatCount} stats, got {points.Count}.", nameof(points));
        }
        return new EffectiveStats(
            ForPoints(points[Ap], Ap), ForPoints(points[Ac], Ac), ForPoints(points[Dx], Dx),
            ForPoints(points[Mp], Mp), ForPoints(points[Ma], Ma), ForPoints(points[Md], Md),
            ForPoints(points[Wt], Wt), ForPoints(points[Da], Da), ForPoints(points[Lk], Lk),
            ForPoints(points[Hp], Hp), ForPoints(points[Dp], Dp), ForPoints(points[Hv], Hv));
    }

    public static StatusValueChart Load(string path) => Parse(File.ReadAllText(path));

    public static StatusValueChart Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var fields = new FieldReader(text);
        var values = new int[(LastStatLevel + 1) * StatCount];
        for (var statLevel = FirstStatLevel; statLevel <= LastStatLevel; statLevel++)
        {
            for (var stat = 0; stat < StatCount; stat++)
            {
                if (!fields.TryReadInt(out _) || !fields.TryReadInt(out _) || !fields.TryReadFloat(out var value))
                {
                    return new StatusValueChart(values);
                }
                values[statLevel * StatCount + stat] = (int)value;
            }
        }
        return new StatusValueChart(values);
    }
}
