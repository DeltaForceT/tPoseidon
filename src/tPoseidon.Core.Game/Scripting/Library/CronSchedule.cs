using System.Globalization;

namespace tPoseidon.Core.Game.Scripting.Library;

public sealed class CronSchedule
{
    private readonly bool[] _minutes;
    private readonly bool[] _hours;
    private readonly bool[] _daysOfMonth;
    private readonly bool[] _months;
    private readonly bool[] _daysOfWeek;
    private readonly bool _dayOfMonthRestricted;
    private readonly bool _dayOfWeekRestricted;

    private CronSchedule(bool[] minutes, bool[] hours, bool[] daysOfMonth, bool[] months, bool[] daysOfWeek, bool dayOfMonthRestricted, bool dayOfWeekRestricted)
    {
        _minutes = minutes;
        _hours = hours;
        _daysOfMonth = daysOfMonth;
        _months = months;
        _daysOfWeek = daysOfWeek;
        _dayOfMonthRestricted = dayOfMonthRestricted;
        _dayOfWeekRestricted = dayOfWeekRestricted;
    }

    public static CronSchedule Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var fields = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
        {
            throw new FormatException("A cron expression has five fields: minute hour day-of-month month day-of-week");
        }
        var minutes = Field(fields[0], 0, 59, "minute");
        var hours = Field(fields[1], 0, 23, "hour");
        var daysOfMonth = Field(fields[2], 1, 31, "day-of-month");
        var months = Field(fields[3], 1, 12, "month");
        var daysOfWeek = Field(fields[4], 0, 7, "day-of-week");
        if (daysOfWeek[7]) daysOfWeek[0] = true;
        return new CronSchedule(minutes, hours, daysOfMonth, months, daysOfWeek, fields[2] != "*", fields[4] != "*");
    }

    public static TimeSpan ParseTimeOfDay(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TimeSpan.TryParseExact(text, @"h\:mm", CultureInfo.InvariantCulture, out var time) &&
            !TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out time))
        {
            throw new FormatException("A time of day is written like \"20:30\"");
        }
        if (time.TotalHours >= 24) throw new FormatException("A time of day is between 00:00 and 23:59");
        return time;
    }

    public bool Matches(DateTimeOffset time)
    {
        if (!_minutes[time.Minute] || !_hours[time.Hour] || !_months[time.Month]) return false;
        var dayOfMonth = _daysOfMonth[time.Day];
        var dayOfWeek = _daysOfWeek[(int)time.DayOfWeek];
        if (_dayOfMonthRestricted && _dayOfWeekRestricted) return dayOfMonth || dayOfWeek;
        return dayOfMonth && dayOfWeek;
    }

    private static bool[] Field(string text, int minimum, int maximum, string name)
    {
        var allowed = new bool[Math.Max(maximum, 31) + 1];
        foreach (var part in text.Split(','))
        {
            var step = 1;
            var range = part;
            var slash = part.IndexOf('/', StringComparison.Ordinal);
            if (slash >= 0)
            {
                if (!int.TryParse(part[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out step) || step < 1)
                {
                    throw new FormatException($"The step in the {name} field is not a positive number");
                }
                range = part[..slash];
            }
            int from;
            int to;
            if (range == "*")
            {
                from = minimum;
                to = maximum;
            }
            else
            {
                var dash = range.IndexOf('-', StringComparison.Ordinal);
                if (dash >= 0)
                {
                    from = Number(range[..dash], name);
                    to = Number(range[(dash + 1)..], name);
                }
                else
                {
                    from = Number(range, name);
                    to = slash >= 0 ? maximum : from;
                }
            }
            if (from < minimum || to > maximum || from > to)
            {
                throw new FormatException($"The {name} field must stay between {minimum} and {maximum}");
            }
            for (var value = from; value <= to; value += step) allowed[value] = true;
        }
        return allowed;
    }

    private static int Number(string text, string name)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"'{text}' in the {name} field is not a number");
        }
        return value;
    }
}
