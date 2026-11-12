using System.Globalization;

namespace tPoseidon.Core.Common;

public sealed class TestClock
{
    public const string FileVariable = "TPOSEIDON_TEST_CLOCK_FILE";

    private readonly Func<DateTimeOffset> _real;
    private readonly string? _file;
    private DateTime _seenWrite = DateTime.MinValue;
    private TimeSpan _offset = TimeSpan.Zero;

    public TestClock(Func<DateTimeOffset> real, string? file)
    {
        _real = real;
        _file = string.IsNullOrWhiteSpace(file) ? null : file;
    }

    public static TestClock FromEnvironment() =>
        new(() => DateTimeOffset.UtcNow, Environment.GetEnvironmentVariable(FileVariable));

    public bool Shifted => _file is not null;

    public string? File => _file;

    public TimeSpan Offset => _offset;

    public DateTimeOffset UtcNow()
    {
        var real = _real();
        if (_file is null) return real;
        try
        {
            if (System.IO.File.Exists(_file))
            {
                var write = System.IO.File.GetLastWriteTimeUtc(_file);
                if (write != _seenWrite)
                {
                    _seenWrite = write;
                    if (TryParseLocal(System.IO.File.ReadAllText(_file), out var target))
                    {
                        _offset = new DateTimeOffset(target, TimeZoneInfo.Local.GetUtcOffset(target)) - real;
                    }
                }
            }
        }
        catch (IOException)
        {
        }
        return real + _offset;
    }

    public static bool TryParseLocal(string text, out DateTime local) =>
        DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out local);
}
