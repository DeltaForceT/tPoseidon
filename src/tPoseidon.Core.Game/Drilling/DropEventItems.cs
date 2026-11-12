using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Drilling;

public sealed class DropEventItems
{
    public const int RollScale = 10000;
    public const int MonthDays = 31;
    public const int WeekDays = 7;

    private readonly Row[] _rows;
    private readonly HashSet<int> _itemIds;

    public DropEventItems(IEnumerable<DropEventItemEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _rows = entries.Where(entry => entry.ItemId != 0).Select(entry => new Row(entry)).ToArray();
        _itemIds = _rows.Select(row => row.Entry.ItemId).ToHashSet();
    }

    public static DropEventItems None { get; } = new(Array.Empty<DropEventItemEntry>());

    public bool IsEventItem(int itemId) => _itemIds.Contains(itemId);

    public int? DrillReplacement(DateTime now, int roll)
    {
        var upper = 0;
        foreach (var row in _rows)
        {
            if (!row.IsActive(now)) continue;
            upper += (int)Math.Truncate(row.Entry.DrillRatio * RollScale);
            if (roll < upper) return row.Entry.ItemId;
        }
        return null;
    }

    private sealed class Row
    {
        private readonly DateTime? _start;
        private readonly DateTime? _end;
        private readonly bool[] _monthDays = new bool[MonthDays + 1];
        private readonly bool[] _weekDays = new bool[WeekDays + 1];

        public Row(DropEventItemEntry entry)
        {
            Entry = entry;
            _start = DateOf(entry.InYear, entry.InMonth, entry.InDay, entry.InHour, entry.InMinute);
            _end = DateOf(entry.OutYear, entry.OutMonth, entry.OutDay, entry.OutHour, entry.OutMinute);
            ReadWorkingDays(entry.WorkingDays);
        }

        public DropEventItemEntry Entry { get; }

        public bool IsActive(DateTime now) =>
            _start is { } start && _end is { } end && start < end && now >= start && now < end
            && (_monthDays[now.Day] || _weekDays[(int)now.DayOfWeek + 1]);

        private void ReadWorkingDays(string text)
        {
            var trimmed = text?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                Array.Fill(_monthDays, true);
                Array.Fill(_weekDays, true);
                return;
            }
            foreach (var raw in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.ToLowerInvariant();
                if (token.Length < 2 || !int.TryParse(token.AsSpan(1), out var number) || number == 0 || (token[0] != 'w' && token[0] != 'd')
                    || (token[0] == 'w' && number > WeekDays) || (token[0] == 'd' && number > MonthDays) || number < 0)
                {
                    Array.Fill(_monthDays, false);
                    Array.Fill(_weekDays, false);
                    return;
                }
                if (token[0] == 'w') _weekDays[number] = true;
                else _monthDays[number] = true;
            }
        }

        private static DateTime? DateOf(int year, int month, int day, int hour, int minute)
        {
            if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)
                || hour < 0 || hour > 23 || minute < 0 || minute > 59) return null;
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        }
    }
}
