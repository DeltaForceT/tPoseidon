using System.Globalization;

namespace tPoseidon.Core.Game.Progression;

public sealed class LevelExpChart
{
    public const int FirstLevel = 1;
    public const int LastLevel = 400;

    private readonly int[] _nextExp;

    private LevelExpChart(int[] nextExp) => _nextExp = nextExp;

    public int NextExp(int level) =>
        level >= FirstLevel && level <= LastLevel ? _nextExp[level] : 0;

    public static LevelExpChart Load(string path) => Parse(File.ReadAllText(path));

    public static LevelExpChart Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var fields = new FieldReader(text);
        var nextExp = new int[LastLevel + 1];
        for (var level = FirstLevel; level <= LastLevel; level++)
        {
            if (!fields.TryReadInt(out _) || !fields.TryReadInt(out _)) break;
            if (!fields.TryReadInt(out var next)) break;
            nextExp[level] = next;
            if (!fields.TryReadLong(out _)) break;
        }
        return new LevelExpChart(nextExp);
    }
}

internal struct FieldReader
{
    private readonly string[] _fields;
    private int _next;

    public FieldReader(string text)
    {
        _fields = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        _next = 0;
    }

    public bool TryReadInt(out int value)
    {
        value = 0;
        if (_next >= _fields.Length
            || !int.TryParse(_fields[_next], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }
        _next++;
        return true;
    }

    public bool TryReadLong(out long value)
    {
        value = 0;
        if (_next >= _fields.Length
            || !long.TryParse(_fields[_next], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }
        _next++;
        return true;
    }

    public bool TryReadFloat(out float value)
    {
        value = 0f;
        if (_next >= _fields.Length
            || !float.TryParse(_fields[_next], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }
        _next++;
        return true;
    }
}
